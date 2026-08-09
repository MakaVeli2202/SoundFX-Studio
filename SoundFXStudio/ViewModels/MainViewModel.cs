using Microsoft.Win32;
using SoundFXStudio.Controls;
using SoundFXStudio.Infrastructure;
using SoundFXStudio.Models;
using SoundFXStudio.Services;
using SoundFXStudio.Views.Dialogs;
using NAudio.Wave;
using NAudio.CoreAudioApi;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using Key = System.Windows.Input.Key;

namespace SoundFXStudio.ViewModels;

public sealed class MainViewModel : ObservableObject
    , IDisposable
{
    private readonly ILogService? _logService;
    private readonly ConfigService _configService;
    private readonly KeyboardLayoutService _keyboardLayoutService = new();
    private readonly AudioPlayer _audioPlayer;
    private readonly ActionExecutor _actionExecutor;
    private readonly AudioDeviceService _audioDeviceService = new();
    private readonly WindowsAudioRoutingService _windowsAudioRoutingService = new();
    private readonly HttpClient _httpClient = new();
    private readonly HashSet<Key> _pressedKeys = new();
    private readonly HashSet<string> _unhighlightTokens = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CancellationTokenSource> _unhighlightTimers = new(); // Auto-unhighlight fallback
    private readonly TriggerService _triggerService;
    private readonly SoundLibraryViewModel _soundLibraryViewModel;
    private readonly KeyboardViewModel _keyboardViewModel;
    private AppConfig _config = new();

    public event Action? VoiceChangerToggleRequested;

    private Window? _window;
    private KeyboardKey? _selectedKey;
    private string? _selectedKeyId;
    private SoundEntry? _selectedSound;
    private Profile? _selectedProfile;
    private string _searchText = string.Empty;
    private string _selectedCategoryFilter = "All";
    private string _profileSearchText = string.Empty;
    private bool _favoritesOnly;
    private int _selectedTabIndex;
    private string _statusText = "Ready";
    private string _windowTitle = "SoundFX Studio";
    private string _currentOutputDevice = "System Default";
    private string _currentInputDevice = "System Default";
    private string _currentPreset = "Default";
    private string _routingStatus = "Not configured";
    private string _selectedSoundBindingStatusText = "Select a sound to see its binding.";
    private string _selectedSoundBindingPromptText = string.Empty;
    private string _bindingHintText = "Drag a sound onto a key to assign it.";
    private string _selectedSoundBindingKeyLabel = "None";
    private string _selectedSoundBindingShortcut = "None";
    private bool _disposed;
    private bool _isLoading;
    private bool _isAssignMode;
    private bool _isSoundboardActive = true;
    private string _currentPage = "Home";
    private string _settingsSection = "General";
    private KeyboardLayoutMode _detectedKeyboardLayout = KeyboardLayoutMode.EnglishUS;

    public MainViewModel(ILogService? logService = null)
    {
        _logService = logService;
        _configService = new ConfigService(_logService);
        _audioPlayer = new AudioPlayer(_logService);

        KeyboardKeys = new ObservableCollection<KeyboardKey>();
        Sounds = new ObservableCollection<SoundEntry>();
        Profiles = new ObservableCollection<Profile>();
        Categories = new ObservableCollection<Category>();
        OutputDevices = new ObservableCollection<AudioDeviceInfo>();
        InputDevices = new ObservableCollection<AudioDeviceInfo>();
        Keybindings = new ObservableCollection<KeybindingItem>();
        AvailableKeybindActions = new ObservableCollection<string>();

        _detectedKeyboardLayout = ResolveWindowsKeyboardLayout(System.Windows.Input.InputLanguageManager.Current.CurrentInputLanguage);

        _actionExecutor = new ActionExecutor(() => _config, _configService, _audioPlayer, ResolveOutputDeviceIndex);

        ConfigureViews();

        _triggerService = new TriggerService(
            new HotkeyService(),
            new KeyboardHookService(),
            _actionExecutor,
            _audioPlayer,
            () => _config,
            GetAssignmentForKey,
            GetAssignmentForKeyToken,
            ResolveSound,
            EnsureSoundAction,
            () => SelectedKey,
            UpdateKeyVisualState,
            value => StatusText = value,
            UpdateTitle,
            RaiseSoundCollectionStats,
            PlaySound,
            RunOnUiThread,
            _logService);

        _keyboardViewModel = new KeyboardViewModel(
            () => _config,
            () => Settings,
            ResolveKeyboardLayoutMode,
            _keyboardLayoutService,
            actionId => _actionExecutor.ExecuteAsync(actionId),
            _audioPlayer,
            KeyboardKeys,
            Sounds,
            Categories,
            Profiles,
            OutputDevices,
            () => SelectedKey,
            value => SelectedKey = value,
            value => StatusText = value,
            RunOnUiThread,
            UpdateTitle,
            RaiseSoundCollectionStats);

        _soundLibraryViewModel = new SoundLibraryViewModel(
            _configService,
            _audioPlayer,
            _httpClient,
            Sounds,
            Profiles,
            KeyboardKeys,
            OutputDevices,
            SoundsView,
            () => _config,
            () => Settings,
            () => _window,
            () => SelectedSound,
            value => SelectedSound = value,
            () => SelectedKey,
            value => SelectedKey = value,
            value => StatusText = value ?? string.Empty,
            Save,
            UpdateStatus,
            UpdateTitle,
            RaiseSoundCollectionStats,
            () => _keyboardViewModel.RefreshAssignments(),
            RunOnUiThread,
            ImportImage);

        _keyboardViewModel.AttachChordRuntimeService(_triggerService.ChordRuntimeService);

        AddSoundCommand = new RelayCommand(_ => _soundLibraryViewModel.AddSound());
        AddMultipleSoundsCommand = new RelayCommand(_ => _soundLibraryViewModel.AddMultipleSounds());
        AddSoundFromUrlCommand = new AsyncRelayCommand(_ => _soundLibraryViewModel.AddSoundFromUrlAsync());
        DeleteMarkedSoundsCommand = new RelayCommand(_ => _soundLibraryViewModel.DeleteMarkedSounds());
        StopAllCommand = new RelayCommand(_ => StopAll());
        KeyClickedCommand = new RelayCommand(parameter => HandleKeyClicked(parameter));
        DuplicateSoundCommand = new RelayCommand(_ => _soundLibraryViewModel.DuplicateSelectedSound(), _ => SelectedSound is not null);
        DeleteSoundCommand = new RelayCommand(_ => _soundLibraryViewModel.DeleteSelectedSound(), _ => SelectedSound is not null);
        PlaySelectedSoundCommand = new RelayCommand(_ => _soundLibraryViewModel.PlaySelectedSound(), _ => SelectedSound is not null);
        PlaySoundCommand = new RelayCommand(parameter => _soundLibraryViewModel.PlaySoundFromLibrary(parameter), parameter => ResolveSound(parameter) is not null);
        EditSoundCommand = new RelayCommand(parameter => EditSound(parameter), parameter => ResolveSound(parameter) is not null);
        AssignSelectedSoundToKeyCommand = new RelayCommand(parameter => AssignSelectedSoundToSelectedKey(parameter), parameter => ResolveKey(parameter) is not null);
        RemoveSoundFromKeyCommand = new RelayCommand(parameter => RemoveSoundFromKey(parameter), parameter => ResolveKey(parameter) is not null);
        RemoveKeyImageCommand = new RelayCommand(parameter => RemoveKeyImage(parameter), parameter => ResolveKey(parameter) is not null);
        ChangeKeyVolumeCommand = new RelayCommand(parameter => ChangeKeyVolume(parameter), parameter => ResolveKey(parameter) is not null);
        StopKeyPlaybackCommand = new RelayCommand(parameter => StopKeyPlayback(parameter), parameter => ResolveKey(parameter) is not null);
        DuplicateBindingCommand = new RelayCommand(parameter => DuplicateBinding(parameter), parameter => ResolveKey(parameter) is not null);
        ChooseKeyImageCommand = new RelayCommand(parameter => ChooseKeyImage(parameter), parameter => ResolveKey(parameter) is not null);
        RenameBindingCommand = new RelayCommand(parameter => RenameBinding(parameter), parameter => ResolveKey(parameter) is not null);
        ClearKeyAssignmentCommand = new RelayCommand(_ => ClearSelectedKeyAssignment(), _ => SelectedKey is not null);
        ToggleFavoriteCommand = new RelayCommand(_ => ToggleFavorite(), _ => SelectedSound is not null);
        SaveCommand = new RelayCommand(_ => Save());
        RefreshCommand = new RelayCommand(_ => Refresh());
        AutoConfigureAudioCommand = new RelayCommand(_ => AutoConfigureAudio());
        TestRoutingCommand = new RelayCommand(_ => TestRouting());
        OpenSetupWizardCommand = new RelayCommand(_ => OpenSetupWizard());
        CreateProfileCommand = new RelayCommand(_ => CreateProfile());
        DeleteProfileCommand = new RelayCommand(_ => DeleteSelectedProfile(), _ => SelectedProfile is not null && Profiles.Count > 1);
        SetGlobalHotkeyCommand = new RelayCommand(_ => SetSelectedSoundHotkey());
        RenameSoundCommand = new RelayCommand(parameter => RenameSelectedSound(parameter), parameter => ResolveSound(parameter) is not null);
        ChooseSoundImageCommand = new RelayCommand(parameter => ChooseSelectedSoundImage(parameter), parameter => ResolveSound(parameter) is not null);
        RemoveSoundImageCommand = new RelayCommand(parameter => RemoveSelectedSoundImage(parameter), parameter => ResolveSound(parameter) is not null);
        ToggleAssignModeCommand = new RelayCommand(_ => ToggleAssignMode(), _ => SelectedSound is not null);
        ClearSelectedSoundBindingCommand = new RelayCommand(_ => ClearSelectedSoundBinding(), _ => SelectedSound is not null);
        SetSoundHotkeyCommand = new RelayCommand(parameter => SetSoundHotkey(parameter), parameter => ResolveSound(parameter) is not null);
        Load();
        InitializeKeybindings();
        UpdateTitle();

        _keyboardViewModel.RebuildKeyboard();
        NotifyKeyboardLampProperties();
        InitializeSoundboardState();
    }

    private void InitializeSoundboardState()
    {
        _isSoundboardActive = !Settings.HotkeyHoldMode;
    }
    public ObservableCollection<KeyboardKey> KeyboardKeys { get; }

    public ObservableCollection<SoundEntry> Sounds { get; }

    public ObservableCollection<Profile> Profiles { get; }

    public ObservableCollection<Category> Categories { get; }

    public ObservableCollection<AudioDeviceInfo> OutputDevices { get; }

    public ObservableCollection<AudioDeviceInfo> InputDevices { get; }

    public ICollectionView SoundsView { get; private set; } = default!;

    public ICollectionView ProfilesView { get; private set; } = default!;

    public ConfigService ConfigService => _configService;

    internal AppConfig Config => _config;

    internal Window? HostWindow => _window;

    internal KeyboardLayoutService KeyboardLayoutService => _keyboardLayoutService;

    internal AudioPlayer AudioPlayer => _audioPlayer;

    internal HttpClient HttpClient => _httpClient;

    public ICommand AddSoundCommand { get; }

    public ICommand AddMultipleSoundsCommand { get; }

    public ICommand AddSoundFromUrlCommand { get; }

    public ICommand DeleteMarkedSoundsCommand { get; }

    public ICommand StopAllCommand { get; }

    public ICommand KeyClickedCommand { get; }

    public ICommand DuplicateSoundCommand { get; }

    public ICommand DeleteSoundCommand { get; }

    public ICommand PlaySelectedSoundCommand { get; }

    public ICommand PlaySoundCommand { get; }

    public ICommand EditSoundCommand { get; }

    public ICommand AssignSelectedSoundToKeyCommand { get; }

    public ICommand RemoveSoundFromKeyCommand { get; }

    public ICommand RemoveKeyImageCommand { get; }

    public ICommand ChangeKeyVolumeCommand { get; }

    public ICommand StopKeyPlaybackCommand { get; }

    public ICommand DuplicateBindingCommand { get; }

    public ICommand ChooseKeyImageCommand { get; }

    public ICommand RenameBindingCommand { get; }

    public ICommand ClearKeyAssignmentCommand { get; }

    public ICommand ToggleFavoriteCommand { get; }

    public ICommand SaveCommand { get; }

    public ICommand RefreshCommand { get; }

    public ICommand AutoConfigureAudioCommand { get; }
    public ICommand TestRoutingCommand { get; }

    public ICommand OpenSetupWizardCommand { get; }

    public ICommand CreateProfileCommand { get; }

    public ICommand DeleteProfileCommand { get; }

    public ICommand SetGlobalHotkeyCommand { get; }

    public ICommand RenameSoundCommand { get; }

    public ICommand ChooseSoundImageCommand { get; }

    public ICommand RemoveSoundImageCommand { get; }

    public ICommand SetSoundHotkeyCommand { get; }

    public ICommand ToggleAssignModeCommand { get; }

    public ICommand ClearSelectedSoundBindingCommand { get; }

    public IReadOnlyList<KeyboardLayoutMode> KeyboardLayoutOptions { get; } = new[]
    {
        KeyboardLayoutMode.Automatic,
        KeyboardLayoutMode.EnglishUK,
        KeyboardLayoutMode.EnglishUS,
        KeyboardLayoutMode.German
    };

    public KeyboardLayoutMode EffectiveKeyboardLayout => ResolveKeyboardLayoutMode();

    public KeyboardLayoutMode KeyboardLayout
    {
        get => Settings.KeyboardLayout;
        set
        {
            if (Settings.KeyboardLayout == value)
            {
                return;
            }

            Settings.KeyboardLayout = value;
            OnPropertyChanged(nameof(EffectiveKeyboardLayout));
            _keyboardViewModel.RebuildKeyboard();
            NotifyKeyboardLampProperties();
            Save();
            OnPropertyChanged(nameof(KeyboardLayout));
        }
    }

    public double CapsLockIndicatorOffsetX => GetKeyboardCalibration().CapsLockIndicatorOffsetX;

    public double CapsLockIndicatorOffsetY => NormalizeLampY(GetKeyboardCalibration().CapsLockIndicatorOffsetY);

    public double NumLockIndicatorOffsetX => GetKeyboardCalibration().NumLockIndicatorOffsetX;

    public double NumLockIndicatorOffsetY => NormalizeLampY(GetKeyboardCalibration().NumLockIndicatorOffsetY);

    public double ScrollLockIndicatorOffsetX => GetKeyboardCalibration().ScrollLockIndicatorOffsetX;

    public double ScrollLockIndicatorOffsetY => NormalizeLampY(GetKeyboardCalibration().ScrollLockIndicatorOffsetY);

    public KeyboardKey? SelectedKey
    {
        get => _selectedKey;
        set
        {
            if (ReferenceEquals(_selectedKey, value))
            {
                return;
            }

            var previous = _selectedKey;
            if (SetProperty(ref _selectedKey, value))
            {
                _selectedKeyId = _selectedKey?.Id;

                if (previous is not null)
                {
                    previous.IsSelected = false;
                    _keyboardViewModel.UpdateKeyVisualState(previous);
                }

                if (_selectedKey is not null)
                {
                    _selectedKey.IsSelected = true;
                    _keyboardViewModel.UpdateKeyVisualState(_selectedKey);
                }

                UpdateStatus();
                UpdateBindingPanelState();
                RaiseCommandState();
            }
        }
    }

    public SoundEntry? SelectedSound
    {
        get => _selectedSound;
        set
        {
            if (SetProperty(ref _selectedSound, value))
            {
                UpdateStatus();
                UpdateBindingPanelState();
                RaiseCommandState();
            }
        }
    }

    public Profile? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (_isLoading)
            {
                SetProperty(ref _selectedProfile, value);
                return;
            }

            if (SetProperty(ref _selectedProfile, value) && value is not null)
            {
                _config.ActiveProfileId = value.Id;
                _keyboardViewModel.RefreshAssignments();
                Save();
                UpdateTitle();
                UpdateBindingPanelState();
                RaiseCommandState();
                _logService?.Info($"Profile Switched: {value.Name}");
            }
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                SoundsView.Refresh();
                RaiseSoundCollectionStats();
            }
        }
    }

    public string SelectedCategoryFilter
    {
        get => _selectedCategoryFilter;
        set
        {
            if (SetProperty(ref _selectedCategoryFilter, value))
            {
                SoundsView.Refresh();
                RaiseSoundCollectionStats();
            }
        }
    }

    public bool FavoritesOnly
    {
        get => _favoritesOnly;
        set
        {
            if (SetProperty(ref _favoritesOnly, value))
            {
                SoundsView.Refresh();
                RaiseSoundCollectionStats();
            }
        }
    }

    public string ProfileSearchText
    {
        get => _profileSearchText;
        set
        {
            if (SetProperty(ref _profileSearchText, value))
            {
                ProfilesView.Refresh();
            }
        }
    }

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => SetProperty(ref _selectedTabIndex, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public string WindowTitle
    {
        get => _windowTitle;
        set => SetProperty(ref _windowTitle, value);
    }

    public string CurrentOutputDevice
    {
        get => _currentOutputDevice;
        set => SetProperty(ref _currentOutputDevice, value);
    }

    public string CurrentInputDevice
    {
        get => _currentInputDevice;
        set => SetProperty(ref _currentInputDevice, value);
    }

    public string CurrentPreset
    {
        get => _currentPreset;
        set => SetProperty(ref _currentPreset, value);
    }

    public string RoutingStatus
    {
        get => _routingStatus;
        set => SetProperty(ref _routingStatus, value);
    }

    public string SelectedSoundBindingStatusText
    {
        get => _selectedSoundBindingStatusText;
        private set => SetProperty(ref _selectedSoundBindingStatusText, value);
    }

    public string SelectedSoundBindingPromptText
    {
        get => _selectedSoundBindingPromptText;
        private set => SetProperty(ref _selectedSoundBindingPromptText, value);
    }

    public string BindingHintText
    {
        get => _bindingHintText;
        private set => SetProperty(ref _bindingHintText, value);
    }

    public string SelectedSoundBindingKeyLabel
    {
        get => _selectedSoundBindingKeyLabel;
        private set => SetProperty(ref _selectedSoundBindingKeyLabel, value);
    }

    public string SelectedSoundBindingShortcut
    {
        get => _selectedSoundBindingShortcut;
        private set => SetProperty(ref _selectedSoundBindingShortcut, value);
    }

    public bool IsAssignMode
    {
        get => _isAssignMode;
        private set
        {
            if (SetProperty(ref _isAssignMode, value))
            {
                UpdateBindingPanelState();
            }
        }
    }

    public bool IsSoundboardActive
    {
        get => _isSoundboardActive;
        set
        {
            if (SetProperty(ref _isSoundboardActive, value))
            {
                OnPropertyChanged(nameof(IsSoundboardActiveText));
                OnPropertyChanged(nameof(IsSoundboardActiveColor));
                StatusText = value ? "Soundboard ON" : "Soundboard OFF";
            }
        }
    }

    public string IsSoundboardActiveText => IsSoundboardActive ? "SOUNDBOARD ON" : "SOUNDBOARD OFF";
    public string IsSoundboardActiveColor => IsSoundboardActive ? "#10B981" : "#F43F5E";

    public string CurrentPage
    {
        get => _currentPage;
        set => SetProperty(ref _currentPage, value);
    }

    public string SettingsSection
    {
        get => _settingsSection;
        set => SetProperty(ref _settingsSection, value);
    }

    public ObservableCollection<KeybindingItem> Keybindings { get; }

    public ObservableCollection<string> AvailableKeybindActions { get; }

    public void InitializeKeybindings()
    {
        Keybindings.Clear();

        AddKeybindingItemIfSet(HotkeyAction.SoundboardToggle, Settings.SoundboardToggleKey);
        AddKeybindingItemIfSet(HotkeyAction.VoiceChangerToggle, Settings.VoiceChangerToggleKey);
        AddKeybindingItemIfSet(HotkeyAction.MuteAll, Settings.MuteAllKey);
        AddKeybindingItemIfSet(HotkeyAction.MuteHear, Settings.MuteHearKey);
        AddKeybindingItemIfSet(HotkeyAction.MuteTeam, Settings.MuteTeamKey);
        AddKeybindingItemIfSet(HotkeyAction.StopAll, Settings.StopAllKey);

        RefreshAvailableKeybindActions();
    }

    private void AddKeybindingItemIfSet(HotkeyAction action, string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        Keybindings.Add(new KeybindingItem(action, key.Trim()));
    }

    public void RefreshAvailableKeybindActions()
    {
        AvailableKeybindActions.Clear();

        foreach (var action in new[] { HotkeyAction.MuteAll, HotkeyAction.MuteHear, HotkeyAction.MuteTeam, HotkeyAction.StopAll, HotkeyAction.SoundboardToggle, HotkeyAction.VoiceChangerToggle })
        {
            if (Keybindings.All(item => item.Action != action))
            {
                AvailableKeybindActions.Add(KeybindingItem.GetActionName(action));
            }
        }
    }

    public void AddKeybinding(HotkeyAction action, string key)
    {
        if (Keybindings.Any(item => item.Action == action))
        {
            return;
        }

        Keybindings.Add(new KeybindingItem(action, key.Trim()));
        SetKeybindingSettings(action, key);
        Save();
        RegisterMuteHotkeys();
        RefreshAvailableKeybindActions();
    }

    public void RemoveKeybinding(KeybindingItem item)
    {
        Keybindings.Remove(item);
        SetKeybindingSettings(item.Action, string.Empty);
        Save();
        RegisterMuteHotkeys();
        RefreshAvailableKeybindActions();
    }

    public void UpdateKeybindingKey(KeybindingItem item, string key)
    {
        item.Key = key.Trim();
        SetKeybindingSettings(item.Action, item.Key);
        Save();
        RegisterMuteHotkeys();
    }

    private void SetKeybindingSettings(HotkeyAction action, string key)
    {
        switch (action)
        {
            case HotkeyAction.SoundboardToggle:
                Settings.SoundboardToggleKey = key;
                break;
            case HotkeyAction.VoiceChangerToggle:
                Settings.VoiceChangerToggleKey = key;
                break;
            case HotkeyAction.MuteAll:
                Settings.MuteAllKey = key;
                break;
            case HotkeyAction.MuteHear:
                Settings.MuteHearKey = key;
                break;
            case HotkeyAction.MuteTeam:
                Settings.MuteTeamKey = key;
                break;
            case HotkeyAction.StopAll:
                Settings.StopAllKey = key;
                break;
        }
    }

    public AppSettings Settings => _config.Settings;

    internal Profile? ActiveProfile => Profiles.FirstOrDefault(item => string.Equals(item.Id, _config.ActiveProfileId, StringComparison.OrdinalIgnoreCase)) ?? Profiles.FirstOrDefault();

    internal string? SelectedKeyIdCache => _selectedKeyId;

    public void AttachWindow(Window window)
    {
        _window = window;
        _triggerService.AttachWindow(window, HandleGlobalKey);
        RegisterMuteHotkeys();
    }

    private bool _toggleKeyDown;
    private bool _voiceChangerToggleKeyDown;

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    private void HandleGlobalKey(Key key, bool isKeyDown)
    {
        if (HandleVoiceChangerToggleKey(key, isKeyDown))
        {
            return;
        }

        if (HandleSoundboardToggleKey(key, isKeyDown))
        {
            return;
        }

        if (!IsSoundboardActive)
        {
            return;
        }

        _keyboardViewModel.HandlePhysicalKey(key, isKeyDown);
    }

    private bool HandleVoiceChangerToggleKey(Key key, bool isKeyDown)
    {
        if (HotkeyService.TryParseChord(Settings.VoiceChangerToggleKey, out var firstChordKey, out var secondChordKey))
        {
            if (key != firstChordKey && key != secondChordKey)
            {
                return false;
            }

            if (isKeyDown)
            {
                if (!_voiceChangerToggleKeyDown
                    && ((key == firstChordKey && IsKeyDown(secondChordKey))
                        || (key == secondChordKey && IsKeyDown(firstChordKey))))
                {
                    _voiceChangerToggleKeyDown = true;
                    VoiceChangerToggleRequested?.Invoke();
                    return true;
                }

                return false;
            }

            var wasActive = _voiceChangerToggleKeyDown;
            _voiceChangerToggleKeyDown = false;
            return wasActive;
        }

        if (!TryParseToggleHotkey(Settings.VoiceChangerToggleKey, out var toggleKey, out var requiredModifiers))
        {
            return false;
        }

        if (key != toggleKey)
        {
            return false;
        }

        if (isKeyDown)
        {
            if (!_voiceChangerToggleKeyDown && ModifiersMatch(requiredModifiers))
            {
                _voiceChangerToggleKeyDown = true;
                VoiceChangerToggleRequested?.Invoke();
                return true;
            }

            return false;
        }

        var active = _voiceChangerToggleKeyDown;
        _voiceChangerToggleKeyDown = false;
        return active;
    }

    private static bool TryParseToggleHotkey(string? keyName, out Key key, out ModifierKeys modifiers)
        => HotkeyService.TryParseHotkey(keyName, out key, out modifiers);

    private static bool ModifiersMatch(ModifierKeys required)
    {
        if (required == ModifierKeys.None)
        {
            return !IsModifierDown(Key.LeftCtrl) && !IsModifierDown(Key.RightCtrl)
                && !IsModifierDown(Key.LeftAlt) && !IsModifierDown(Key.RightAlt)
                && !IsModifierDown(Key.LeftShift) && !IsModifierDown(Key.RightShift)
                && !IsModifierDown(Key.LWin) && !IsModifierDown(Key.RWin);
        }

        return (IsModifierDown(Key.LeftCtrl) || IsModifierDown(Key.RightCtrl)) == required.HasFlag(ModifierKeys.Control)
            && (IsModifierDown(Key.LeftAlt) || IsModifierDown(Key.RightAlt)) == required.HasFlag(ModifierKeys.Alt)
            && (IsModifierDown(Key.LeftShift) || IsModifierDown(Key.RightShift)) == required.HasFlag(ModifierKeys.Shift)
            && (IsModifierDown(Key.LWin) || IsModifierDown(Key.RWin)) == required.HasFlag(ModifierKeys.Windows);
    }

    private static bool IsModifierDown(Key key)
        => (GetAsyncKeyState(KeyInterop.VirtualKeyFromKey(key)) & 0x8000) != 0;

    private static bool IsKeyDown(Key key)
        => (GetAsyncKeyState(KeyInterop.VirtualKeyFromKey(key)) & 0x8000) != 0;

    private bool HandleSoundboardToggleKey(Key key, bool isKeyDown)
    {
        if (!HotkeyService.TryParseHotkey(Settings.SoundboardToggleKey, out var toggleKey, out var requiredModifiers))
        {
            return false;
        }

        if (key != toggleKey)
        {
            return false;
        }

        if (isKeyDown)
        {
            if (_toggleKeyDown)
            {
                return true;
            }

            if (requiredModifiers != ModifierKeys.None && !ModifiersMatch(requiredModifiers))
            {
                return true;
            }

            _toggleKeyDown = true;
            OnToggleKeyPressed();
            return true;
        }

        _toggleKeyDown = false;
        if (Settings.HotkeyHoldMode)
        {
            IsSoundboardActive = false;
        }
        return true;
    }

    private void OnToggleKeyPressed()
    {
        IsSoundboardActive = Settings.HotkeyHoldMode || !IsSoundboardActive;
        ShowSoundboardNotification();
    }

    public void ToggleSoundboardMode()
    {
        IsSoundboardActive = !IsSoundboardActive;
        ShowSoundboardNotification();
    }

    public void RegisterMuteHotkeys()
    {
        _triggerService.RegisterMuteHotkeys(Settings.MuteAllKey, Settings.MuteHearKey, Settings.MuteTeamKey, Settings.StopAllKey);
    }

    public string? GetVoiceChangerMicId()
    {
        var physical = InputDevices.ToList();
        var micId = Settings.MicrophoneDeviceId;
        if (!string.IsNullOrWhiteSpace(micId)
            && physical.Any(d => string.Equals(d.Id, micId, StringComparison.OrdinalIgnoreCase)))
        {
            return micId;
        }
        if (!string.IsNullOrWhiteSpace(Settings.TalkDeviceName))
        {
            var byName = physical.FirstOrDefault(d =>
                string.Equals(d.Name, Settings.TalkDeviceName, StringComparison.OrdinalIgnoreCase)
                || d.Name.StartsWith(Settings.TalkDeviceName, StringComparison.OrdinalIgnoreCase));
            if (byName is not null)
            {
                return byName.Id;
            }
        }
        var def = physical.FirstOrDefault(d => d.IsDefaultCommunication || d.IsDefault)
            ?? physical.FirstOrDefault();
        return def?.Id;
    }

    private sealed class VoiceChangerDryMicSnapshot
    {
        public required int StripIndex { get; init; }
        public required float PreviousB1 { get; init; }
        public required int ProcessedStripIndex { get; init; }
        public required float PreviousVirtualA1 { get; init; }
        public required string MicDeviceId { get; init; }
        public required string MicDeviceName { get; init; }
    }

    private VoiceChangerDryMicSnapshot? _voiceChangerMicSnapshot;
    private Dictionary<string, float>? _voiceChangerMonitorSnapshot;

    public bool TryPrepareVoiceChangerRouting(string micDeviceId, out int outputDeviceIndex, out string outputDeviceId, out string outputDeviceName, out string error)
    {
        outputDeviceIndex = -1;
        outputDeviceId = string.Empty;
        outputDeviceName = string.Empty;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(micDeviceId))
        {
            error = "Voice changer routing failed: microphone device is not selected.";
            _logService?.Warning(error);
            return false;
        }

        outputDeviceId = _audioDeviceService.GetVoicemeeterInputId() ?? string.Empty;
        outputDeviceName = _audioDeviceService.GetVoicemeeterInputDeviceName() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(outputDeviceId) || string.IsNullOrWhiteSpace(outputDeviceName))
        {
            error = "Voice changer routing failed: Voicemeeter Input (VAIO) endpoint was not found.";
            _logService?.Warning(error);
            return false;
        }

        outputDeviceIndex = ResolveWaveOutIndex(outputDeviceName);
        if (outputDeviceIndex < 0)
        {
            error = $"Voice changer routing failed: Voicemeeter Input endpoint '{outputDeviceName}' is not available to WaveOut.";
            _logService?.Warning(error);
            return false;
        }

        var micDeviceName = _audioDeviceService.GetCaptureDevice(micDeviceId)?.FriendlyName
            ?? Settings.TalkDeviceName
            ?? micDeviceId;

        try
        {
            using var vm = new VoicemeeterRemote();
            if (!vm.TryConnect())
            {
                error = "Voice changer routing failed: could not connect to Voicemeeter.";
                _logService?.Warning(error);
                return false;
            }

            var stripCount = vm.StripCount();
            var firstVirtual = vm.FirstVirtualStrip(stripCount);
            if (stripCount <= 0 || firstVirtual <= 0)
            {
                error = $"Voice changer routing failed: invalid Voicemeeter strip topology (count={stripCount}, firstVirtual={firstVirtual}).";
                _logService?.Warning(error);
                return false;
            }

            if (!TryResolvePhysicalMicStripIndex(vm, firstVirtual, micDeviceName, Settings.TalkDeviceName, out var micStripIndex, out var resolvedStripDevice))
            {
                error = $"Voice changer routing failed: could not map mic '{micDeviceName}' to a physical Voicemeeter strip.";
                _logService?.Warning(error);
                return false;
            }

            var micB1Param = $"Strip[{micStripIndex}].B1";
            var previousB1 = vm.GetFloat(micB1Param);
            var setMuteResult = TrySetFloatVerified(vm, micB1Param, 0f, out var mutedB1);
            if (!setMuteResult)
            {
                _ = TrySetFloatVerified(vm, micB1Param, previousB1, out _);
                error = $"Voice changer routing failed: unable to force dry mic strip B1 OFF (strip={micStripIndex}, target=0, readBack={mutedB1:0.##}).";
                _logService?.Warning(error);
                return false;
            }

            var processedStripIndex = firstVirtual;
            var processedB1Param = $"Strip[{processedStripIndex}].B1";
            var processedB1Before = vm.GetFloat(processedB1Param);
            if (processedB1Before < 0.5f)
            {
                var processedSetOk = TrySetFloatVerified(vm, processedB1Param, 1f, out var processedB1After);
                if (!processedSetOk)
                {
                    _ = TrySetFloatVerified(vm, micB1Param, previousB1, out _);
                    error = $"Voice changer routing failed: unable to ensure processed strip B1 ON (strip={processedStripIndex}, target=1, readBack={processedB1After:0.##}).";
                    _logService?.Warning(error);
                    return false;
                }
            }

            var virtualA1Param = $"Strip[{processedStripIndex}].A1";
            var previousVirtualA1 = vm.GetFloat(virtualA1Param);
            if (previousVirtualA1 >= 0.5f)
            {
                var setA1Result = TrySetFloatVerified(vm, virtualA1Param, 0f, out var disabledA1);
                if (!setA1Result)
                {
                    _ = TrySetFloatVerified(vm, micB1Param, previousB1, out _);
                    error = $"Voice changer routing failed: unable to disable virtual input A1 (strip={processedStripIndex}, target=0, readBack={disabledA1:0.##}).";
                    _logService?.Warning(error);
                    return false;
                }
            }

            _voiceChangerMicSnapshot = new VoiceChangerDryMicSnapshot
            {
                StripIndex = micStripIndex,
                PreviousB1 = previousB1,
                ProcessedStripIndex = processedStripIndex,
                PreviousVirtualA1 = previousVirtualA1,
                MicDeviceId = micDeviceId,
                MicDeviceName = micDeviceName
            };

            _logService?.Info(
                $"VC START routing: mic='{micDeviceName}' id='{micDeviceId}', strip={micStripIndex} stripDevice='{resolvedStripDevice}', B1 {previousB1:0.##}->0, " +
                $"vmInputId='{outputDeviceId}', vmInputName='{outputDeviceName}', outputIndex={outputDeviceIndex}, outputConfirmed=true, processedStrip={processedStripIndex} B1={vm.GetFloat($"Strip[{processedStripIndex}].B1"):0.##}");

            return true;
        }
        catch (Exception ex)
        {
            error = $"Voice changer routing failed: {ex.Message}";
            _logService?.Error(error, ex);
            return false;
        }
    }

    public bool TryRestoreVoiceChangerRouting(out string error)
    {
        error = string.Empty;
        var snapshot = _voiceChangerMicSnapshot;
        if (snapshot is null)
        {
            return true;
        }

        try
        {
            using var vm = new VoicemeeterRemote();
            if (!vm.TryConnect())
            {
                error = "Voice changer routing restore failed: could not connect to Voicemeeter.";
                _logService?.Warning(error);
                return false;
            }

            var micB1Param = $"Strip[{snapshot.StripIndex}].B1";
            var currentB1 = vm.GetFloat(micB1Param);
            var restoreResult = TrySetFloatVerified(vm, micB1Param, snapshot.PreviousB1, out var restoredB1);

            if (!restoreResult)
            {
                error = $"Voice changer routing restore failed: strip {snapshot.StripIndex} B1 restore mismatch (target={snapshot.PreviousB1:0.##}, readBack={restoredB1:0.##}).";
                _logService?.Warning(error);
                return false;
            }

            var virtualA1Param = $"Strip[{snapshot.ProcessedStripIndex}].A1";
            var restoreA1Result = TrySetFloatVerified(vm, virtualA1Param, 1f, out var restoredA1);
            if (!restoreA1Result)
            {
                error = $"Voice changer routing restore failed: virtual strip {snapshot.ProcessedStripIndex} A1 enable mismatch (target=1, readBack={restoredA1:0.##}).";
                _logService?.Warning(error);
                return false;
            }

            _voiceChangerMicSnapshot = null;
            _logService?.Info($"VC STOP routing: micStrip={snapshot.StripIndex}, B1 current={currentB1:0.##}, restored={restoredB1:0.##}, mic='{snapshot.MicDeviceName}' id='{snapshot.MicDeviceId}'");
            return true;
        }
        catch (Exception ex)
        {
            error = $"Voice changer routing restore failed: {ex.Message}";
            _logService?.Error(error, ex);
            return false;
        }
    }

    private static bool TryResolvePhysicalMicStripIndex(VoicemeeterRemote vm, int firstVirtualStrip, string? micDeviceName, string? talkDeviceName, out int stripIndex, out string resolvedStripDevice)
    {
        stripIndex = -1;
        resolvedStripDevice = string.Empty;

        static string NormalizeName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return string.Empty;
            }

            return name.Trim();
        }

        static bool IsNameMatch(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            {
                return false;
            }

            return left.Equals(right, StringComparison.OrdinalIgnoreCase)
                || left.StartsWith(right, StringComparison.OrdinalIgnoreCase)
                || right.StartsWith(left, StringComparison.OrdinalIgnoreCase);
        }

        var micName = NormalizeName(micDeviceName);
        var talkName = NormalizeName(talkDeviceName);

        for (int i = 0; i < firstVirtualStrip; i++)
        {
            var device = NormalizeName(vm.GetString($"Strip[{i}].device.name"));
            if (IsNameMatch(device, micName) || IsNameMatch(device, talkName))
            {
                stripIndex = i;
                resolvedStripDevice = device;
                return true;
            }
        }

        if (firstVirtualStrip == 1)
        {
            stripIndex = 0;
            resolvedStripDevice = NormalizeName(vm.GetString("Strip[0].device.name"));
            return true;
        }

        return false;
    }

    private static bool TrySetFloatVerified(VoicemeeterRemote vm, string param, float target, out float readBack, int retries = 10, int delayMs = 40)
    {
        readBack = float.NaN;
        for (int i = 0; i < retries; i++)
        {
            int rc = vm.SetFloat(param, target);
            if (rc == 0)
            {
                vm.IsDirty();
            }
            System.Threading.Thread.Sleep(delayMs);
            readBack = vm.GetFloat(param);
            if (rc == 0 && Math.Abs(readBack - target) <= 0.1f)
            {
                return true;
            }
        }

        return false;
    }

    public void BeginVoiceChangerMonitorMute()
    {
        _voiceChangerMonitorSnapshot = null;
        _ = Task.Run(() =>
        {
            try
            {
                using var vm = new VoicemeeterRemote();
                if (!vm.TryConnect())
                {
                    return;
                }

                int firstVirtual = vm.FirstVirtualStrip(vm.StripCount());
                var snapshot = new Dictionary<string, float>
                {
                    [$"Strip[{firstVirtual}].A1"] = vm.GetFloat($"Strip[{firstVirtual}].A1")
                };
                vm.SetFloat($"Strip[{firstVirtual}].A1", 0);
                _voiceChangerMonitorSnapshot = snapshot;
            }
            catch
            {
                _voiceChangerMonitorSnapshot = null;
            }
        });
    }

    public void EndVoiceChangerMonitorMute()
    {
        var snapshot = _voiceChangerMonitorSnapshot;
        _voiceChangerMonitorSnapshot = null;
        if (snapshot is null || snapshot.Count == 0)
        {
            return;
        }

        _ = Task.Run(() =>
        {
            try
            {
                using var vm = new VoicemeeterRemote();
                if (!vm.TryConnect())
                {
                    return;
                }

                foreach (var kv in snapshot)
                {
                    vm.SetFloat($"Strip[{kv.Key}].A1", kv.Value);
                }
            }
            catch
            {
            }
        });
    }

    private void ShowSoundboardNotification()
    {
        var status = IsSoundboardActive ? "ON" : "OFF";
        var mode = Settings.HotkeyHoldMode ? "HOLD" : "TOGGLE";
        RunOnUiThread(() =>
        {
            StatusText = $"Soundboard {status} [{mode}]";
            ToastWindow.Show("Soundboard Keybind", $"Listening {status} · {mode} mode", IsSoundboardActive);
        });
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _logService?.Info("Disposing MainViewModel");
        _triggerService.Dispose();
        _logService?.Info("Disposing AudioPlayer");
        _logService?.Info("Disposing HttpClient");
        _audioPlayer.Dispose();
        _httpClient.Dispose();
        GC.SuppressFinalize(this);
    }

    public void HandlePreviewKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        if (IsAssignMode)
        {
            if (TryResolveKeyboardKeyFromPhysicalKey(e.Key) is { } assignKey)
            {
                AssignSoundToKey(SelectedSound, assignKey);
                IsAssignMode = false;
            }

            e.Handled = true;
            return;
        }

        if (HandleSoundboardToggleKey(e.Key, isKeyDown: true))
        {
            e.Handled = true;
            return;
        }

        if (HandleVoiceChangerToggleKey(e.Key, isKeyDown: true))
        {
            e.Handled = true;
            return;
        }

        if (_triggerService.IsBareMuteKey(e.Key))
        {
            if (!e.IsRepeat)
            {
                _triggerService.TryHandleBareMuteKey(e.Key);
            }
            e.Handled = true;
            return;
        }

        if (!IsSoundboardActive) return;

        HandlePhysicalKey(e.Key, isKeyDown: true);
    }

    public void HandlePreviewKeyUp(System.Windows.Input.KeyEventArgs e)
    {
        if (HandleSoundboardToggleKey(e.Key, isKeyDown: false))
        {
            e.Handled = true;
            return;
        }

        if (HandleVoiceChangerToggleKey(e.Key, isKeyDown: false))
        {
            return;
        }

        if (!IsSoundboardActive)
        {
            _keyboardViewModel.ReleaseVisualKey(e.Key);
            return;
        }

        HandlePhysicalKey(e.Key, isKeyDown: false);
    }

    public void HandleDropFiles(IEnumerable<string> files)
    {
        foreach (var file in files)
        {
            if (Directory.Exists(file))
            {
                continue;
            }

            if (IsAudioFile(file))
            {
                ImportSound(file);
            }
            else if (IsImageFile(file) && SelectedSound is not null)
            {
                SelectedSound.ImagePath = ImportImage(file);
            }
        }

        RefreshAssignments();
        Save();
    }

    public void ApplyKeyboardCalibrationFromSettings()
    {
        ApplyKeyboardCalibration(Settings.KeyboardCalibration);
        _keyboardViewModel.RefreshAssignments();
        NotifyKeyboardLampProperties();
    }

    public void SaveKeyboardCalibrationSettings()
    {
        ApplyKeyboardCalibrationFromSettings();
        Save();
    }

    private void Load()
    {
        _isLoading = true;
        try
        {
            _config.PropertyChanged -= Config_PropertyChanged;
            _config.Settings.PropertyChanged -= Settings_PropertyChanged;
            _config = _configService.Load();

            Sounds.Clear();
            foreach (var sound in _config.Sounds)
            {
                Sounds.Add(sound);
            }

            Profiles.Clear();
            foreach (var profile in _config.Profiles)
            {
                Profiles.Add(profile);
            }

            MigrateLegacyChords();

            Categories.Clear();
            foreach (var category in _config.Categories)
            {
                Categories.Add(category);
            }

            OutputDevices.Clear();
            foreach (var device in _audioDeviceService.GetOutputDevices())
            {
                OutputDevices.Add(device);
            }

            InputDevices.Clear();
            foreach (var device in _audioDeviceService.GetInputDevices())
            {
                InputDevices.Add(device);
            }

            if (Profiles.Count == 0)
            {
                Profiles.Add(new Profile { Name = "Default", Description = "Fallback profile", IsDefault = true });
            }

            var active = ActiveProfile ?? Profiles.First();
            SelectedProfile = active;
            _config.PropertyChanged += Config_PropertyChanged;
            _config.Settings.PropertyChanged += Settings_PropertyChanged;
            ApplyKeyboardCalibration(Settings.KeyboardCalibration);
            _keyboardViewModel.RefreshAssignments();
            NotifyKeyboardLampProperties();
            UpdateRoutingStatus();
            UpdateStatus();
            RaiseSoundCollectionStats();
            _logService?.Info($"Profile Loaded: {active.Name}");
        }
        finally
        {
            _isLoading = false;
        }
    }

    private static void ApplyKeyboardCalibration(KeyboardCalibrationSettings calibration)
    {
        calibration ??= new KeyboardCalibrationSettings();

        var gapX = Math.Abs(calibration.GapX) > double.Epsilon ? calibration.GapX : calibration.Gap;
        var gapY = Math.Abs(calibration.GapY) > double.Epsilon ? calibration.GapY : calibration.Gap;

        KeyboardLayoutPanel.SetLayoutCalibration(
            calibration.KeyUnit,
            gapX,
            gapY,
            calibration.OffsetX,
            calibration.OffsetY);

        KeyboardLayoutPanel.ButtonScale = calibration.ButtonScale;
        KeyboardLayoutPanel.DebugKeyboardCalibration = calibration.DebugCalibration;

        KeyboardClusterLayout.ApplyPreset(
            calibration.EscOffsetX,
            calibration.EscOffsetY,
            calibration.F1ToF4OffsetX,
            calibration.F1ToF4OffsetY,
            calibration.F5ToF8OffsetX,
            calibration.F5ToF8OffsetY,
            calibration.F9ToF12OffsetX,
            calibration.F9ToF12OffsetY,
            calibration.PrintScrollPauseOffsetX,
            calibration.PrintScrollPauseOffsetY,
            calibration.MainTypingOffsetX,
            calibration.MainTypingOffsetY,
            calibration.NavigationOffsetX,
            calibration.NavigationOffsetY,
            calibration.ArrowOffsetX,
            calibration.ArrowOffsetY,
            calibration.NumpadOffsetX,
            calibration.NumpadOffsetY);

        KeyboardLayoutPanel.ClearAllSpecialKeyOverrides();
        KeyboardLayoutPanel.SetSpecialKeyOverride("SPACE", calibration.SpacebarWidthAdjustment);
        KeyboardLayoutPanel.SetSpecialKeyOverride("BACKSPACE", calibration.BackspaceWidthAdjustment);
        KeyboardLayoutPanel.SetSpecialKeyOverride("ENTER", calibration.EnterWidthAdjustment);
        KeyboardLayoutPanel.SetSpecialKeyOverride("OEM102", calibration.IsoEnterWidthAdjustment);
        KeyboardLayoutPanel.SetSpecialKeyOverride("SHIFT-L", calibration.LeftShiftWidthAdjustment);
        KeyboardLayoutPanel.SetSpecialKeyOverride("SHIFT-R", calibration.RightShiftWidthAdjustment);
        KeyboardLayoutPanel.SetSpecialKeyOverride("ENTER-NUMPAD", calibration.NumpadEnterWidthAdjustment);
        KeyboardLayoutPanel.SetSpecialKeyOverride("TAB", calibration.TabWidthAdjustment);
        KeyboardLayoutPanel.SetSpecialKeyOverride("CAPS LOCK", calibration.CapsLockWidthAdjustment);

        KeyboardLayoutPanel.ClearAllPerKeyOverrides();
        foreach (var entry in calibration.KeyOverrides)
        {
            KeyboardLayoutPanel.SetPerKeyOverride(
                entry.Key,
                entry.Value.OffsetX,
                entry.Value.OffsetY,
                entry.Value.WidthAdjustment,
                entry.Value.HeightAdjustment);
        }
    }

    private void Config_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(AppConfig.ActiveProfileId))
        {
            return;
        }

        var activeProfile = ActiveProfile;
        if (activeProfile is null)
        {
            return;
        }

        if (SelectedProfile?.Id == activeProfile.Id)
        {
            return;
        }

        SelectedProfile = activeProfile;
        UpdateTitle();
    }

    private void Settings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AppSettings.OutputDeviceId)
            or nameof(AppSettings.InputDeviceId)
            or nameof(AppSettings.VirtualCableDeviceId)
            or nameof(AppSettings.VBCableDetected)
            or nameof(AppSettings.KeyboardPressedTextColor))
        {
            UpdateRoutingStatus();
            Save();
        }
    }

    private void ConfigureViews()
    {
        SoundsView = CollectionViewSource.GetDefaultView(Sounds);
        SoundsView.Filter = item =>
        {
            if (item is not SoundEntry sound)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(SearchText))
            {
                return true;
            }

            var search = SearchText.Trim();
                 var matchesSearch = sound.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                            || sound.Category.Contains(search, StringComparison.OrdinalIgnoreCase)
                            || sound.Hotkey.Contains(search, StringComparison.OrdinalIgnoreCase);

                 var matchesCategory = string.IsNullOrWhiteSpace(SelectedCategoryFilter)
                              || SelectedCategoryFilter == "All"
                              || string.Equals(sound.Category, SelectedCategoryFilter, StringComparison.OrdinalIgnoreCase);

                 var matchesFavorites = !FavoritesOnly || sound.IsFavorite;

                return matchesSearch && matchesCategory && matchesFavorites;
        };

        ProfilesView = CollectionViewSource.GetDefaultView(Profiles);
            ProfilesView.Filter = item =>
            {
            if (item is not Profile profile)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(ProfileSearchText))
            {
                return true;
            }

            var search = ProfileSearchText.Trim();
            return profile.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                   || profile.Description.Contains(search, StringComparison.OrdinalIgnoreCase);
        };
    }

    internal void RefreshAssignments()
    {
        _keyboardViewModel.RefreshAssignments();
        NotifyKeyboardLampProperties();
    }

    private void HandlePhysicalKey(Key key, bool isKeyDown = true)
    {
        _keyboardViewModel.HandlePhysicalKey(key, isKeyDown);
    }

    private static ModifierKeys GetModifierState()
        => (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl) ? ModifierKeys.Control : ModifierKeys.None)
           | (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt) ? ModifierKeys.Alt : ModifierKeys.None)
           | (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift) ? ModifierKeys.Shift : ModifierKeys.None)
           | (Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin) ? ModifierKeys.Windows : ModifierKeys.None);

    private void PlayKey(KeyboardKey key)
    {
        var assignment = GetAssignmentForKey(key);
        if (assignment is null)
        {
            return;
        }

        _triggerService.ExecuteAssignmentOnce(assignment);
    }

    internal void PlaySound(SoundEntry sound, KeyAssignment? assignment = null)
    {
        if (!File.Exists(sound.FilePath))
        {
            RunOnUiThread(() => StatusText = $"Missing file: {sound.Name}");
            return;
        }

        var deviceIndex = _audioDeviceService.ResolveVoicemeeterInputWaveOutIndex();
        if (deviceIndex < 0)
        {
            deviceIndex = ResolveOutputDeviceIndex(Settings.OutputDeviceId);
        }

        _audioPlayer.Play(
            sound.Id,
            sound.FilePath,
            assignment?.VolumeOverride ?? sound.Volume,
            PlaybackMode.Restart,
            deviceIndex);

        RunOnUiThread(() =>
        {
            sound.PlayCount++;
            sound.LastPlayedUtc = DateTime.UtcNow;
            RaiseSoundCollectionStats();

            if (assignment is not null && SelectedKey is not null && string.Equals(SelectedKey.Id, assignment.KeyId, StringComparison.OrdinalIgnoreCase))
            {
                SelectedKey.State = KeyState.Playing;
            }

            StatusText = $"Playing {sound.Name}";
            UpdateTitle();
        });

        _ = TrackPlaybackAsync(sound.Id, assignment?.KeyId);
    }

    private void HandleKeyClicked(object? parameter)
    {
        if (IsAssignMode && parameter is KeyboardKey key)
        {
            AssignSoundToKey(SelectedSound, key);
            IsAssignMode = false;
            return;
        }

        _keyboardViewModel.HandleKeyClicked(parameter);
    }

    private void FlashKey(KeyboardKey key) => _keyboardViewModel.FlashKey(key);

    private void AddSound() => _soundLibraryViewModel.AddSound();

    private void AddMultipleSounds() => _soundLibraryViewModel.AddMultipleSounds();

    private Task AddSoundFromUrlAsync() => _soundLibraryViewModel.AddSoundFromUrlAsync();

    private void DeleteMarkedSounds() => _soundLibraryViewModel.DeleteMarkedSounds();

    private SoundEntry? ResolveSound(object? parameter) => parameter as SoundEntry ?? SelectedSound;

    internal bool TryGetSoundDetails(string? initialFilePath, SoundEntry? existingSound, out SoundAssignmentViewModel details)
    {
        details = new SoundAssignmentViewModel();

        if (existingSound is not null)
        {
            details.FilePath = existingSound.FilePath;
            details.ImagePath = existingSound.ImagePath ?? string.Empty;
            details.Name = existingSound.Name;
            details.VolumePercent = existingSound.Volume * 100;
            details.IsFavorite = existingSound.IsFavorite;
            details.SelectedKey = GetAssignedKeyIdForSound(existingSound) ?? string.Empty;

            details.ChordKeys = GetChordKeysForSound(existingSound);
        }

        if (!string.IsNullOrWhiteSpace(initialFilePath) && string.IsNullOrWhiteSpace(details.FilePath))
        {
            details.FilePath = initialFilePath;
            details.Name = Path.GetFileNameWithoutExtension(initialFilePath);
            details.VolumePercent = 100;
        }

        var editor = new SoundAssignmentWindow
        {
            Owner = _window,
            DataContext = details
        };

        var result = editor.ShowDialog() == true;
        return result;
    }

    internal string? GetAssignedKeyIdForSound(SoundEntry sound)
    {
        var profile = ActiveProfile;
        if (profile is null)
        {
            return null;
        }

        return profile.Assignments.FirstOrDefault(item => string.Equals(item.SoundId, sound.Id, StringComparison.OrdinalIgnoreCase))?.KeyId;
    }

    internal void AddSoundFromDetails(SoundAssignmentViewModel details)
    {
        if (string.IsNullOrWhiteSpace(details.FilePath) || !File.Exists(details.FilePath))
        {
            return;
        }

        var sourceFile = details.FilePath;
        var destinationFolder = _configService.GetSoundsFolder();
        var destinationFile = Path.Combine(destinationFolder, GetUniqueFileName(destinationFolder, Path.GetFileName(sourceFile)));
        if (!string.Equals(Path.GetFullPath(sourceFile), Path.GetFullPath(destinationFile), StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(sourceFile, destinationFile, true);
        }

        var sound = new SoundEntry
        {
            Name = string.IsNullOrWhiteSpace(details.Name) ? Path.GetFileNameWithoutExtension(destinationFile) : details.Name.Trim(),
            FilePath = destinationFile,
            Category = "Custom",
            Volume = (float)Math.Clamp(details.VolumePercent / 100.0, 0.0, 1.0),
            IsFavorite = details.IsFavorite
        };

        if (!string.IsNullOrWhiteSpace(details.ImagePath) && File.Exists(details.ImagePath))
        {
            sound.ImagePath = ImportImage(details.ImagePath);
        }

        Sounds.Add(sound);
        _config.Sounds.Add(sound);

        AssignSoundToKeyIfSelected(sound, details.SelectedKey);
        UpdateSoundChordAssignment(sound, details.ChordKeys);

        RefreshAssignments();
        SoundsView.Refresh();
        SelectedSound = sound;
        Save();
        StatusText = $"Added {sound.Name}";
        UpdateBindingPanelState();
        RaiseSoundCollectionStats();
    }

    internal void AssignSoundToKeyIfSelected(SoundEntry sound, string? keyId)
    {
        if (string.IsNullOrWhiteSpace(keyId))
        {
            return;
        }

        var key = KeyboardKeys.FirstOrDefault(item => string.Equals(item.Id, keyId, StringComparison.OrdinalIgnoreCase));
        if (key is null)
        {
            return;
        }

        var profile = ActiveProfile;
        if (profile is null)
        {
            return;
        }

        var assignment = profile.Assignments.FirstOrDefault(item => string.Equals(item.KeyId, key.Id, StringComparison.OrdinalIgnoreCase));
        if (assignment is null)
        {
            assignment = new KeyAssignment { KeyId = key.Id, SoundId = sound.Id, ActionId = EnsureSoundAction(sound).Id, HotkeyText = key.KeyName };
            profile.Assignments.Add(assignment);
        }
        else
        {
            ClearChordStateForSingleKeyAssignment(profile, assignment);
            assignment.SoundId = sound.Id;
            assignment.ActionId = EnsureSoundAction(sound).Id;
            assignment.HotkeyText = key.KeyName;
        }

        sound.AssignedKeyId = key.Id;
        sound.AssignedKeyLabel = key.DisplayLabel;
    }

    private void EditSound(object? parameter)
    {
        var sound = ResolveSound(parameter);
        if (sound is null)
        {
            return;
        }

        if (!TryGetSoundDetails(sound.FilePath, sound, out var details))
        {
            return;
        }

        if (File.Exists(details.FilePath) && !string.Equals(Path.GetFullPath(details.FilePath), Path.GetFullPath(sound.FilePath), StringComparison.OrdinalIgnoreCase))
        {
            var destinationFolder = _configService.GetSoundsFolder();
            var destinationFile = Path.Combine(destinationFolder, GetUniqueFileName(destinationFolder, Path.GetFileName(details.FilePath)));
            if (!string.Equals(Path.GetFullPath(details.FilePath), Path.GetFullPath(destinationFile), StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(details.FilePath, destinationFile, true);
            }

            sound.FilePath = destinationFile;
        }

        sound.Name = string.IsNullOrWhiteSpace(details.Name) ? sound.Name : details.Name.Trim();
        sound.Volume = (float)Math.Clamp(details.VolumePercent / 100.0, 0.0, 1.0);
        sound.IsFavorite = details.IsFavorite;

        if (!string.IsNullOrWhiteSpace(details.ImagePath) && File.Exists(details.ImagePath))
        {
            sound.ImagePath = ImportImage(details.ImagePath);
        }
        else if (string.IsNullOrWhiteSpace(details.ImagePath))
        {
            sound.ImagePath = null;
        }

        UpdateSoundKeyAssignment(sound, details.SelectedKey);
        UpdateSoundChordAssignment(sound, details.ChordKeys);

        RefreshAssignments();
        Save();
        SelectedSound = sound;
        StatusText = $"Updated {sound.Name}";
        UpdateBindingPanelState();
        RaiseSoundCollectionStats();
    }

    internal void UpdateSoundKeyAssignment(SoundEntry sound, string? keyId)
    {
        var profile = ActiveProfile;
        if (profile is null)
        {
            return;
        }

        var existingAssignment = profile.Assignments.FirstOrDefault(item =>
            string.Equals(item.SoundId, sound.Id, StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(item.ChordKey));

        if (string.IsNullOrWhiteSpace(keyId))
        {
            if (existingAssignment is not null)
            {
                profile.Assignments.Remove(existingAssignment);
            }

            sound.AssignedKeyId = null;
            sound.AssignedKeyLabel = null;
            return;
        }

        var key = KeyboardKeys.FirstOrDefault(item => string.Equals(item.Id, keyId, StringComparison.OrdinalIgnoreCase));
        if (key is null)
        {
            return;
        }

        var assignment = profile.Assignments.FirstOrDefault(item =>
            string.IsNullOrWhiteSpace(item.ChordKey)
            && string.Equals(item.KeyId, key.Id, StringComparison.OrdinalIgnoreCase));
        if (assignment is null)
        {
            assignment = new KeyAssignment { KeyId = key.Id, SoundId = sound.Id, ActionId = EnsureSoundAction(sound).Id, HotkeyText = key.KeyName };
            profile.Assignments.Add(assignment);
        }
        else
        {
            ClearChordStateForSingleKeyAssignment(profile, assignment);
            assignment.SoundId = sound.Id;
            assignment.ActionId = EnsureSoundAction(sound).Id;
            assignment.HotkeyText = key.KeyName;
        }

        if (existingAssignment is not null && !string.Equals(existingAssignment.KeyId, key.Id, StringComparison.OrdinalIgnoreCase))
        {
            profile.Assignments.Remove(existingAssignment);
        }

        RemoveConflictingChordForSingleKey(profile, sound.Id, key.KeyName);

        sound.AssignedKeyId = key.Id;
        sound.AssignedKeyLabel = key.DisplayLabel;
    }

    private static void RemoveConflictingChordForSingleKey(Profile profile, string soundId, string keyName)
    {
        var singleToken = NormalizeChordToken(keyName);
        if (string.IsNullOrWhiteSpace(singleToken))
        {
            return;
        }

        var chordForSound = profile.Assignments.FirstOrDefault(item =>
            string.Equals(item.SoundId, soundId, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(item.ChordKey));
        if (chordForSound is null)
        {
            return;
        }

        var chordKeys = new List<string>();
        if (!string.IsNullOrWhiteSpace(chordForSound.HotkeyText))
        {
            chordKeys.Add(NormalizeChordToken(chordForSound.HotkeyText));
        }

        if (!string.IsNullOrWhiteSpace(chordForSound.ChordKey))
        {
            chordKeys.Add(NormalizeChordToken(chordForSound.ChordKey));
        }

        if (!chordKeys.Contains(singleToken, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        var chordActionId = chordForSound.ActionId;
        profile.Assignments.Remove(chordForSound);
        if (chordActionId is Guid id)
        {
            RemoveSoundChord(profile, id);
        }
    }

    internal void UpdateSoundChordAssignment(SoundEntry sound, IReadOnlyList<string> chordKeys)
    {
        var profile = ActiveProfile;
        if (profile is null)
        {
            return;
        }

        var tokens = (chordKeys ?? Array.Empty<string>())
            .Select(NormalizeChordToken)
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();

        var soundAction = EnsureSoundAction(sound);

        var existingChord = profile.Assignments.FirstOrDefault(item =>
            string.Equals(item.SoundId, sound.Id, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(item.ChordKey));

        if (tokens.Count < 2)
        {
            if (existingChord is not null)
            {
                profile.Assignments.Remove(existingChord);
            }

            RemoveSoundChord(profile, soundAction.Id);
            return;
        }

        var firstToken = tokens[0];
        var secondToken = tokens[1];
        var firstKey = KeyboardKeys.FirstOrDefault(item => string.Equals(item.KeyName, firstToken, StringComparison.OrdinalIgnoreCase));
        if (existingChord is not null)
        {
            existingChord.HotkeyText = firstToken;
            existingChord.ChordKey = secondToken;
            existingChord.KeyId = firstKey?.Id ?? existingChord.KeyId;
            existingChord.ActionId = soundAction.Id;
        }
        else
        {
            var assignment = new KeyAssignment
            {
                SoundId = sound.Id,
                ActionId = soundAction.Id,
                HotkeyText = firstToken,
                ChordKey = secondToken,
                KeyId = firstKey?.Id ?? string.Empty
            };
            profile.Assignments.Add(assignment);
        }

        RemoveConflictingSingleKeysForChord(profile, sound, tokens, firstKey);

        SyncSoundChord(profile, sound, soundAction, tokens);
    }

    private static void RemoveConflictingSingleKeysForChord(Profile profile, SoundEntry sound, IReadOnlyList<string> chordTokens, KeyboardKey? firstKey)
    {
        var tokenSet = new HashSet<string>(chordTokens, StringComparer.OrdinalIgnoreCase);

        foreach (var single in profile.Assignments
            .Where(item =>
                string.Equals(item.SoundId, sound.Id, StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(item.ChordKey)
                && tokenSet.Contains(NormalizeChordToken(item.HotkeyText)))
            .ToList())
        {
            profile.Assignments.Remove(single);
        }

        if (firstKey is not null)
        {
            sound.AssignedKeyId = firstKey.Id;
            sound.AssignedKeyLabel = firstKey.DisplayLabel;
        }
    }

    private static void SyncSoundChord(Profile profile, SoundEntry sound, ActionDefinition soundAction, IReadOnlyList<string> keys)
    {
        var chord = profile.KeyChords.FirstOrDefault(item => item.ActionId == soundAction.Id);
        if (chord is null)
        {
            chord = new KeyChord { Name = sound.Name, ActionId = soundAction.Id };
            profile.KeyChords.Add(chord);
        }

        chord.Name = sound.Name;
        chord.Keys.Clear();
        foreach (var key in keys)
        {
            chord.Keys.Add(key);
        }
    }

    private static void RemoveSoundChord(Profile profile, Guid soundActionId)
    {
        var chord = profile.KeyChords.FirstOrDefault(item => item.ActionId == soundActionId);
        if (chord is not null)
        {
            profile.KeyChords.Remove(chord);
        }
    }

    private static void ClearChordStateForSingleKeyAssignment(Profile profile, KeyAssignment assignment)
    {
        if (string.IsNullOrWhiteSpace(assignment.ChordKey))
        {
            return;
        }

        var priorActionId = assignment.ActionId;
        assignment.ChordKey = string.Empty;
        if (priorActionId is Guid actionId)
        {
            RemoveSoundChord(profile, actionId);
        }
    }

    private void MigrateLegacyChords()
    {
        foreach (var profile in _config.Profiles)
        {
            foreach (var assignment in profile.Assignments.Where(item => !string.IsNullOrWhiteSpace(item.ChordKey)))
            {
                var actionId = assignment.ActionId;
                if (!actionId.HasValue)
                {
                    continue;
                }

                var first = NormalizeChordToken(assignment.HotkeyText);
                var second = NormalizeChordToken(assignment.ChordKey);
                if (string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(second))
                {
                    continue;
                }

                var existingChord = profile.KeyChords.FirstOrDefault(item => item.ActionId == actionId.Value);
                if (existingChord is not null)
                {
                    continue;
                }

                var chord = new KeyChord
                {
                    Name = assignment.BindingName ?? assignment.HotkeyText,
                    ActionId = actionId.Value
                };
                chord.Keys.Add(first);
                chord.Keys.Add(second);
                profile.KeyChords.Add(chord);
            }

            RemoveConflictingSingleKeysFromLegacy(profile);
        }
    }

    private static void RemoveConflictingSingleKeysFromLegacy(Profile profile)
    {
        foreach (var chordAssignment in profile.Assignments.Where(item => !string.IsNullOrWhiteSpace(item.ChordKey)).ToList())
        {
            var chordKeys = new List<string>();
            if (!string.IsNullOrWhiteSpace(chordAssignment.HotkeyText))
            {
                chordKeys.Add(NormalizeChordToken(chordAssignment.HotkeyText));
            }

            if (!string.IsNullOrWhiteSpace(chordAssignment.ChordKey))
            {
                chordKeys.Add(NormalizeChordToken(chordAssignment.ChordKey));
            }

            if (chordKeys.Count == 0)
            {
                continue;
            }

            foreach (var single in profile.Assignments
                .Where(item =>
                    string.IsNullOrWhiteSpace(item.ChordKey)
                    && string.Equals(item.SoundId, chordAssignment.SoundId, StringComparison.OrdinalIgnoreCase)
                    && chordKeys.Contains(NormalizeChordToken(item.HotkeyText), StringComparer.OrdinalIgnoreCase))
                .ToList())
            {
                profile.Assignments.Remove(single);
            }
        }
    }

    internal KeyAssignment? GetChordForSound(SoundEntry sound)
        => ActiveProfile?.Assignments.FirstOrDefault(item =>
            string.Equals(item.SoundId, sound.Id, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(item.ChordKey));

    internal IReadOnlyList<string> GetChordKeysForSound(SoundEntry sound)
    {
        var action = _config.Actions.FirstOrDefault(item =>
            item.Type == ActionType.Sound && string.Equals(item.Payload, sound.Id, StringComparison.OrdinalIgnoreCase));
        if (action is not null)
        {
            var chord = ActiveProfile?.KeyChords.FirstOrDefault(item => item.ActionId == action.Id);
            if (chord is not null && chord.Keys.Count > 0)
            {
                return chord.Keys.ToList();
            }
        }

        var legacy = GetChordForSound(sound);
        if (legacy is null)
        {
            return Array.Empty<string>();
        }

        var keys = new List<string>();
        if (!string.IsNullOrWhiteSpace(legacy.HotkeyText)) keys.Add(NormalizeChordToken(legacy.HotkeyText));
        if (!string.IsNullOrWhiteSpace(legacy.ChordKey)) keys.Add(NormalizeChordToken(legacy.ChordKey));
        return keys;
    }

    private static string NormalizeChordToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return string.Empty;
        }

        var parts = token.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 0 ? string.Empty : parts[^1].Trim().ToUpperInvariant();
    }

    private string? ResolveKeyIdFromHotkey(string? hotkeyText)
    {
        var keyToken = NormalizeChordToken(hotkeyText);
        if (string.IsNullOrWhiteSpace(keyToken))
        {
            return null;
        }

        return KeyboardKeys.FirstOrDefault(item =>
            string.Equals(item.KeyName, keyToken, StringComparison.OrdinalIgnoreCase))?.Id;
    }

    internal void AssignSoundToKeyFromUi(SoundEntry sound, KeyboardKey key) => AssignSoundToKey(sound, key);

    internal void AssignSoundChordFromUi(SoundEntry sound, IReadOnlyList<string> keyNames)
    {
        UpdateSoundChordAssignment(sound, keyNames);
        RefreshAssignments();
        Save();
        var tokens = (keyNames ?? Array.Empty<string>())
            .Select(NormalizeChordToken)
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .ToList();
        StatusText = tokens.Count < 2
            ? $"Cleared chord for {sound.Name}"
            : $"Assigned {sound.Name} to {string.Join(" + ", tokens)}";
    }

    private void AssignSoundToKey(SoundEntry? sound, KeyboardKey key)
    {
        if (sound is null)
        {
            StatusText = $"Select a sound first for {key.DisplayLabel}";
            return;
        }

        var profile = ActiveProfile;
        if (profile is null)
        {
            return;
        }

        var assignment = profile.Assignments.FirstOrDefault(item => string.Equals(item.KeyId, key.Id, StringComparison.OrdinalIgnoreCase));
        if (assignment is null)
        {
            assignment = new KeyAssignment { KeyId = key.Id, SoundId = sound.Id, ActionId = EnsureSoundAction(sound).Id, HotkeyText = key.KeyName };
            profile.Assignments.Add(assignment);
        }
        else
        {
            ClearChordStateForSingleKeyAssignment(profile, assignment);
            assignment.SoundId = sound.Id;
            assignment.ActionId = EnsureSoundAction(sound).Id;
            assignment.HotkeyText = key.KeyName;
        }

        sound.AssignedKeyId = key.Id;
        RefreshAssignments();
        Save();
        SelectedKey = key;
        StatusText = $"Assigned {sound.Name} to {key.DisplayLabel}";
    }

    private void ToggleAssignMode()
    {
        IsAssignMode = !IsAssignMode;
        StatusText = IsAssignMode
            ? SelectedSound is null
                ? "Assign mode: select a sound first"
                : $"Assign mode: choose a key for {SelectedSound.Name}"
            : "Assign mode off";
    }

    internal KeyboardKey? TryResolveKeyboardKeyFromPhysicalKey(Key key)
    {
        var token = NormalizeTokenForLayout(ToKeyToken(key, GetModifierState()));
        return KeyboardKeys.FirstOrDefault(item => string.Equals(item.KeyName, token, StringComparison.OrdinalIgnoreCase));
    }

    public KeyboardKey? ResolveKeyFromName(string keyName) =>
        KeyboardKeys.FirstOrDefault(item => string.Equals(item.KeyName, keyName, StringComparison.OrdinalIgnoreCase));

    private void RenameSelectedSound(object? parameter)
    {
        var sound = ResolveSound(parameter);
        if (sound is null)
        {
            return;
        }

        var input = Microsoft.VisualBasic.Interaction.InputBox("Sound name", "Rename Sound", sound.Name);
        if (string.IsNullOrWhiteSpace(input))
        {
            return;
        }

        Save();
        SelectedSound = sound;
        StatusText = $"Renamed sound to {sound.Name}";
        UpdateBindingPanelState();
    }

    private void ChooseSelectedSoundImage(object? parameter) => _soundLibraryViewModel.ChooseSelectedSoundImage(parameter);

    private void RemoveSelectedSoundImage(object? parameter)
    {
        var sound = ResolveSound(parameter);
        if (sound is null)
        {
            return;
        }

        sound.ImagePath = null;
        RefreshAssignments();
        Save();
        StatusText = $"Removed image from {sound.Name}";
    }

    private void SetSoundHotkey(object? parameter)
    {
        var sound = ResolveSound(parameter);
        if (sound is null)
        {
            return;
        }

        var dialog = new HotkeyCaptureDialog(sound.Hotkey)
        {
            Owner = _window
        };

        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.CapturedHotkey))
        {
            return;
        }

        sound.Hotkey = dialog.CapturedHotkey;
        var assignment = ActiveProfile?.Assignments.FirstOrDefault(item =>
            string.Equals(item.SoundId, sound.Id, StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(item.ChordKey));
        if (assignment is null)
        {
            var keyId = ResolveKeyIdFromHotkey(dialog.CapturedHotkey);
            assignment = new KeyAssignment { KeyId = keyId ?? string.Empty, SoundId = sound.Id, ActionId = EnsureSoundAction(sound).Id };
            ActiveProfile?.Assignments.Add(assignment);
        }

        if (assignment is not null)
        {
            assignment.HotkeyText = dialog.CapturedHotkey;
            assignment.KeyId = ResolveKeyIdFromHotkey(dialog.CapturedHotkey) ?? assignment.KeyId;
            assignment.ActionId = EnsureSoundAction(sound).Id;
        }

        if (SelectedSound is not null && string.Equals(SelectedSound.Id, sound.Id, StringComparison.OrdinalIgnoreCase))
        {
            SelectedSound = sound;
        }

        _triggerService.RegisterGlobalHotkeys();
        Save();
        StatusText = $"Shortcut set for {sound.Name}";
        UpdateBindingPanelState();
    }

    private void ImportSound(string fileName) => _soundLibraryViewModel.ImportSound(fileName);

    internal static string ResolveFileNameFromUrl(Uri uri, HttpResponseMessage response)
    {
        var contentDisposition = response.Content.Headers.ContentDisposition;
        var fromHeader = contentDisposition?.FileNameStar ?? contentDisposition?.FileName;
        var fileName = string.IsNullOrWhiteSpace(fromHeader)
            ? Path.GetFileName(uri.LocalPath)
            : fromHeader.Trim('"');

        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = "AudioFile.mp3";
        }

        fileName = string.Concat(fileName.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));

        if (string.IsNullOrWhiteSpace(Path.GetExtension(fileName)))
        {
            fileName += ".mp3";
        }

        return fileName;
    }

    internal string ImportImage(string fileName)
    {
        var destinationFolder = _configService.GetImagesFolder();
        var destinationFile = Path.Combine(destinationFolder, GetUniqueFileName(destinationFolder, Path.GetFileName(fileName)));
        if (!string.Equals(Path.GetFullPath(fileName), Path.GetFullPath(destinationFile), StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(fileName, destinationFile, true);
        }

        return destinationFile;
    }

    private void DuplicateSelectedSound() => _soundLibraryViewModel.DuplicateSelectedSound();

    private void DeleteSelectedSound() => _soundLibraryViewModel.DeleteSelectedSound();

    private void PlaySelectedSound() => _soundLibraryViewModel.PlaySelectedSound();

    private void PlaySoundFromLibrary(object? parameter) => _soundLibraryViewModel.PlaySoundFromLibrary(parameter);

    private void AssignSelectedSoundToSelectedKey(object? parameter)
    {
        var key = ResolveKey(parameter);
        if (key is null)
        {
            return;
        }

        var soundToAssign = SelectedSound;
        if (soundToAssign is null)
        {
            var currentlyBoundSoundId = ActiveProfile?.Assignments
                .FirstOrDefault(item => string.Equals(item.KeyId, key.Id, StringComparison.OrdinalIgnoreCase))
                ?.SoundId ?? key.AssignedSoundId;

            var picker = new AssignSoundToKeyDialog(Sounds, currentlyBoundSoundId)
            {
                Owner = GetDialogOwner()
            };

            if (picker.ShowDialog() != true || picker.SelectedSound is null)
            {
                return;
            }

            soundToAssign = picker.SelectedSound;
            SelectedSound = soundToAssign;
        }

        var profile = ActiveProfile;
        if (profile is null)
        {
            return;
        }

        var assignment = profile.Assignments.FirstOrDefault(item => string.Equals(item.KeyId, key.Id, StringComparison.OrdinalIgnoreCase));
        if (assignment is null)
        {
            assignment = new KeyAssignment { KeyId = key.Id, SoundId = soundToAssign.Id, ActionId = EnsureSoundAction(soundToAssign).Id, HotkeyText = key.KeyName };
            profile.Assignments.Add(assignment);
        }
        else
        {
            ClearChordStateForSingleKeyAssignment(profile, assignment);
            assignment.SoundId = soundToAssign.Id;
            assignment.ActionId = EnsureSoundAction(soundToAssign).Id;
            assignment.HotkeyText = key.KeyName;
        }

        soundToAssign.AssignedKeyId = key.Id;
        soundToAssign.AssignedKeyLabel = key.DisplayLabel;
        RefreshAssignments();
        Save();
        StatusText = $"Assigned {soundToAssign.Name} to {key.DisplayLabel}";
        SelectedKey = key;
        UpdateBindingPanelState();
    }

    private void ClearSelectedSoundBinding()
    {
        if (SelectedSound is null)
        {
            return;
        }

        var profile = ActiveProfile;
        if (profile is not null)
        {
            var assignment = profile.Assignments.FirstOrDefault(item => string.Equals(item.SoundId, SelectedSound.Id, StringComparison.OrdinalIgnoreCase));
            if (assignment is not null)
            {
                profile.Assignments.Remove(assignment);
            }
        }

        SelectedSound.AssignedKeyId = null;
        SelectedSound.AssignedKeyLabel = null;
        SelectedSound.Hotkey = string.Empty;
        RefreshAssignments();
        Save();
        StatusText = $"Cleared binding for {SelectedSound.Name}";
        UpdateBindingPanelState();
    }

    private void RemoveSoundFromKey(object? parameter)
    {
        var key = ResolveKey(parameter);
        if (key is null)
        {
            return;
        }

        var profile = ActiveProfile;
        if (profile is null)
        {
            return;
        }

        var assignment = profile.Assignments.FirstOrDefault(item => string.Equals(item.KeyId, key.Id, StringComparison.OrdinalIgnoreCase));
        if (assignment is null)
        {
            return;
        }

        assignment.SoundId = string.Empty;
        assignment.ActionId = null;
        if (string.IsNullOrWhiteSpace(assignment.BindingName) && string.IsNullOrWhiteSpace(assignment.ImagePath))
        {
            profile.Assignments.Remove(assignment);
        }

        RefreshAssignments();
        Save();
        SelectedKey = key;
        StatusText = $"Removed sound from {key.DisplayLabel}";
    }

    private void RemoveKeyImage(object? parameter)
    {
        var key = ResolveKey(parameter);
        if (key is null)
        {
            return;
        }

        var assignment = GetAssignmentForKey(key);
        if (assignment is null)
        {
            return;
        }

        assignment.ImagePath = null;
        RefreshAssignments();
        Save();
        SelectedKey = key;
        StatusText = $"Image removed from {key.DisplayLabel}";
    }

    private void ChangeKeyVolume(object? parameter)
    {
        var key = ResolveKey(parameter);
        if (key is null)
        {
            return;
        }

        var assignment = GetAssignmentForKey(key);
        if (assignment is null)
        {
            return;
        }

        var dialog = new VolumeDialog(Math.Clamp(assignment.VolumeOverride, 0f, 1f) * 100)
        {
            Owner = GetDialogOwner()
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        assignment.VolumeOverride = (float)Math.Clamp(dialog.VolumePercent / 100.0, 0.0, 1.0);
        Save();
        SelectedKey = key;
        StatusText = $"Volume set for {key.DisplayLabel}";
    }

    private void StopKeyPlayback(object? parameter)
    {
        var key = ResolveKey(parameter);
        if (key is null)
        {
            return;
        }

        var assignment = GetAssignmentForKey(key);
        if (assignment is null || string.IsNullOrWhiteSpace(assignment.SoundId))
        {
            return;
        }

        _triggerService.StopAssignmentPlayback(assignment);
        UpdateKeyVisualState(key);
        StatusText = $"Stopped {key.DisplayLabel}";
    }

    private void DuplicateBinding(object? parameter)
    {
        var key = ResolveKey(parameter);
        if (key is null)
        {
            return;
        }

        var assignment = GetAssignmentForKey(key);
        if (assignment is null)
        {
            return;
        }

        var target = Microsoft.VisualBasic.Interaction.InputBox("Target key id", "Duplicate Binding", key.KeyName);
        if (string.IsNullOrWhiteSpace(target))
        {
            return;
        }

        var targetKey = KeyboardKeys.FirstOrDefault(item => string.Equals(item.KeyName, target.Trim(), StringComparison.OrdinalIgnoreCase));
        if (targetKey is null || targetKey.Id == key.Id)
        {
            return;
        }

        var profile = ActiveProfile;
        if (profile is null)
        {
            return;
        }

        var clone = new KeyAssignment
        {
            KeyId = targetKey.Id,
            SoundId = assignment.SoundId,
            ActionId = assignment.ActionId,
            BindingName = assignment.BindingName,
            ImagePath = assignment.ImagePath,
            HotkeyText = assignment.HotkeyText,
            IsGlobal = assignment.IsGlobal,
            VolumeOverride = assignment.VolumeOverride,
            FadeOutMs = assignment.FadeOutMs,
            StopOnReplay = assignment.StopOnReplay
        };

        profile.Assignments.Add(clone);
        Save();
        SelectedKey = targetKey;
        StatusText = $"Duplicated binding to {targetKey.DisplayLabel}";
    }

    private void ChooseKeyImage(object? parameter)
    {
        var key = ResolveKey(parameter);
        if (key is null)
        {
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "Choose Key Image",
            Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp|All Files|*.*"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var profile = ActiveProfile;
        if (profile is null)
        {
            return;
        }

        var assignment = profile.Assignments.FirstOrDefault(item => string.Equals(item.KeyId, key.Id, StringComparison.OrdinalIgnoreCase));
        if (assignment is null)
        {
            assignment = new KeyAssignment { KeyId = key.Id, SoundId = string.Empty };
            profile.Assignments.Add(assignment);
        }

        assignment.ImagePath = ImportImage(dialog.FileName);
        RefreshAssignments();
        Save();
        SelectedKey = key;
        StatusText = $"Image set for {key.DisplayLabel}";
    }

    private void RenameBinding(object? parameter)
    {
        var key = ResolveKey(parameter);
        if (key is null)
        {
            return;
        }

        var profile = ActiveProfile;
        if (profile is null)
        {
            return;
        }

        var current = profile.Assignments.FirstOrDefault(item => string.Equals(item.KeyId, key.Id, StringComparison.OrdinalIgnoreCase))?.BindingName ?? key.DisplayLabel;
        var dialog = new TextEntryDialog("Rename Binding", "Set a custom label for this key. Use Clear to remove the custom name and fall back to the default key label.", current, allowClear: true)
        {
            Owner = GetDialogOwner()
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var assignment = profile.Assignments.FirstOrDefault(item => string.Equals(item.KeyId, key.Id, StringComparison.OrdinalIgnoreCase));
        if (assignment is null)
        {
            assignment = new KeyAssignment { KeyId = key.Id, SoundId = string.Empty };
            profile.Assignments.Add(assignment);
        }

        assignment.BindingName = dialog.ClearRequested ? null : dialog.Value.Trim();
        RemoveAssignmentIfEmpty(profile, assignment);
        RefreshAssignments();
        Save();
        SelectedKey = key;
        StatusText = dialog.ClearRequested
            ? $"Removed custom binding name from {key.DisplayLabel}"
            : $"Renamed binding on {key.DisplayLabel}";
    }

    private KeyboardKey? ResolveKey(object? parameter) => parameter as KeyboardKey ?? SelectedKey;

    private Window? GetDialogOwner()
    {
        return Application.Current.Windows.OfType<Window>().FirstOrDefault(window => window.IsActive)
            ?? _window;
    }

    private static void RemoveAssignmentIfEmpty(Profile profile, KeyAssignment assignment)
    {
        if (!string.IsNullOrWhiteSpace(assignment.SoundId)
            || !string.IsNullOrWhiteSpace(assignment.ImagePath)
            || !string.IsNullOrWhiteSpace(assignment.BindingName)
            || Math.Abs(assignment.VolumeOverride - 1f) > float.Epsilon
            || !string.IsNullOrWhiteSpace(assignment.HotkeyText)
            || assignment.ActionId is not null)
        {
            return;
        }

        profile.Assignments.Remove(assignment);
    }

    internal KeyAssignment? GetAssignmentForKey(KeyboardKey key)
    {
        var profile = ActiveProfile;
        if (profile is null)
        {
            return null;
        }

        return profile.Assignments.FirstOrDefault(item => string.Equals(item.KeyId, key.Id, StringComparison.OrdinalIgnoreCase))
            ?? profile.Assignments.FirstOrDefault(item =>
                string.IsNullOrWhiteSpace(item.ChordKey)
                && string.Equals(NormalizeChordToken(item.HotkeyText), key.KeyName, StringComparison.OrdinalIgnoreCase));
    }

    internal KeyAssignment? GetAssignmentForKeyToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var profile = ActiveProfile;
        if (profile is null)
        {
            return null;
        }

        var normalizedToken = NormalizeChordToken(token);

        var direct = profile.Assignments.FirstOrDefault(item =>
            string.IsNullOrWhiteSpace(item.ChordKey)
            && (string.Equals(item.KeyId, normalizedToken, StringComparison.OrdinalIgnoreCase)
                || string.Equals(NormalizeChordToken(item.HotkeyText), normalizedToken, StringComparison.OrdinalIgnoreCase)));
        if (direct is not null)
        {
            return direct;
        }

        var key = KeyboardKeys.FirstOrDefault(item => string.Equals(item.KeyName, normalizedToken, StringComparison.OrdinalIgnoreCase));
        if (key is null)
        {
            return null;
        }

        return profile.Assignments.FirstOrDefault(item =>
            string.IsNullOrWhiteSpace(item.ChordKey)
            && string.Equals(item.KeyId, key.Id, StringComparison.OrdinalIgnoreCase));
    }

    private void ClearSelectedKeyAssignment()
    {
        if (SelectedKey is null)
        {
            return;
        }

        var profile = ActiveProfile;
        if (profile is null)
        {
            return;
        }

        var assignment = profile.Assignments.FirstOrDefault(item => string.Equals(item.KeyId, SelectedKey.Id, StringComparison.OrdinalIgnoreCase));
        if (assignment is not null)
        {
            profile.Assignments.Remove(assignment);
        }

        SelectedKey.AssignedSoundId = null;
        SelectedKey.AssignedSoundName = null;
        SelectedKey.State = KeyState.Empty;
        Save();
        RefreshAssignments();
    }

    private void ToggleFavorite()
    {
        if (SelectedSound is null)
        {
            return;
        }

        SelectedSound.IsFavorite = !SelectedSound.IsFavorite;
        Save();
    }

    private void SetSelectedSoundHotkey()
    {
        if (SelectedSound is null)
        {
            return;
        }

        var dialog = new HotkeyCaptureDialog(SelectedSound.Hotkey)
        {
            Owner = _window
        };

        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.CapturedHotkey))
        {
            return;
        }

        SelectedSound.Hotkey = dialog.CapturedHotkey;
        var assignment = ActiveProfile?.Assignments.FirstOrDefault(item => string.Equals(item.SoundId, SelectedSound.Id, StringComparison.OrdinalIgnoreCase));
        if (assignment is null && SelectedKey is not null)
        {
            assignment = new KeyAssignment { KeyId = SelectedKey.Id, SoundId = SelectedSound.Id, ActionId = EnsureSoundAction(SelectedSound).Id };
            ActiveProfile?.Assignments.Add(assignment);
        }

        if (assignment is not null)
        {
            assignment.HotkeyText = SelectedSound.Hotkey;
            assignment.KeyId = ResolveKeyIdFromHotkey(SelectedSound.Hotkey) ?? assignment.KeyId;
            assignment.ActionId = EnsureSoundAction(SelectedSound).Id;
        }

        _triggerService.RegisterGlobalHotkeys();
        Save();
        UpdateBindingPanelState();
    }

    private void CreateProfile()
    {
        var name = Microsoft.VisualBasic.Interaction.InputBox("Profile name", "New Profile", "Custom");
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var profile = new Profile
        {
            Name = name.Trim(),
            Description = "Custom profile",
            AccentColor = "#00D4FF"
        };

        Profiles.Add(profile);
        _config.Profiles.Add(profile);
        SelectedProfile = profile;

        Save();
        _logService?.Info($"Profile Created: {profile.Name}");
    }

    private void DeleteSelectedProfile()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        if (Profiles.Count <= 1)
        {
            return;
        }

        if (System.Windows.MessageBox.Show($"Delete profile '{SelectedProfile.Name}'?", "Delete Profile", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question) != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        var profile = SelectedProfile;
        Profiles.Remove(profile);
        _config.Profiles.Remove(profile);
        SelectedProfile = Profiles.FirstOrDefault();
        RefreshAssignments();
        Save();
        _logService?.Info($"Profile Deleted: {profile.Name}");
    }

    public void StopAllSounds()
    {
        _audioPlayer.StopAll();
        StatusText = "Stopped all sounds";
    }

    private void StopAll()
    {
        StopAllSounds();
    }

    internal void Refresh()
    {
        Load();
        InitializeKeybindings();
        _triggerService.RegisterGlobalHotkeys();
    }

    private void OpenSetupWizard()
    {
        var wizard = new SetupWizardWindow
        {
            Owner = _window
        };

        wizard.ShowDialog();
        Refresh();
    }

    private void AutoConfigureAudio()
    {
        var output = PickBestDevice(OutputDevices, preferVirtual: false);
        var input = PickBestDevice(InputDevices, preferVirtual: false);
        if (output is not null)
        {
            Settings.OutputDeviceId = output.Id;
            Settings.PlaybackDeviceId = output.Id;

        }

        if (input is not null)
        {
            Settings.InputDeviceId = input.Id;
            Settings.MicrophoneDeviceId = input.Id;
        }

        Settings.VirtualCableDeviceId = string.Empty;
        Settings.VBCableDetected = false;

        Settings.LastConfigurationDate = DateTime.UtcNow;
        Save();
        UpdateRoutingStatus();

        var outputName = output?.Name ?? "no output device";
        var inputName = input?.Name ?? "no input device";
        StatusText = $"Auto-configured audio: {outputName} / {inputName}";
    }

    private void TestRouting()
    {
        var selectedOutput = OutputDevices.FirstOrDefault(device => string.Equals(device.Id, Settings.OutputDeviceId, StringComparison.OrdinalIgnoreCase))
            ?? OutputDevices.FirstOrDefault(device => device.IsDefaultCommunication)
            ?? OutputDevices.FirstOrDefault(device => device.IsDefault)
            ?? OutputDevices.FirstOrDefault();

        if (selectedOutput is null)
        {
            StatusText = "No output device available for a routing test.";
            return;
        }

        var deviceIndex = ResolveWaveOutIndex(selectedOutput.Name);
        var testTonePath = EnsureRoutingTestTone();

        _audioPlayer.Play("routing-test", testTonePath, 0.8f, PlaybackMode.Restart, deviceIndex);
        StatusText = $"Routing test playing through {selectedOutput.Name}";
        UpdateRoutingStatus();
    }

    // ─── Voicemeeter / audio setup methods ───────────────────────────────

    internal async Task<string> AutoSetupVoicemeeterAsync(AudioDeviceInfo? hearDevice, AudioDeviceInfo? talkDevice)
    {
        if (hearDevice is null || talkDevice is null)
            return "Select both Hear and Talk devices first.";

        return await Task.Run(() =>
        {
            if (!VoicemeeterService.IsVoicemeeterInstalled())
                return "✗  Voicemeeter isn't installed yet.";

            try
            {
                using var vm = new VoicemeeterRemote();
                if (!vm.Login())
                    return "✗  Could not start Voicemeeter.";

                bool applied = vm.ApplyRouting(hearDevice.Name, talkDevice.Name);
                var diagnostics = string.IsNullOrWhiteSpace(vm.LastDiagnostics) ? null : vm.LastDiagnostics;
                vm.Dispose();

                if (!applied)
                {
                    var msg = $"✗  Could not select devices in Voicemeeter:\n   Hear: {hearDevice.Name}\n   Talk: {talkDevice.Name}";
                    if (diagnostics is not null)
                        msg += $"\n\n{diagnostics}";
                    return msg;
                }

                Settings.HearDeviceName = hearDevice.Name;
                Settings.TalkDeviceName = talkDevice.Name;
                Settings.SpeakersDeviceName = hearDevice.Name;
                Settings.VoicemeeterDetected = true;

                var vmInputId = _audioDeviceService.GetVoicemeeterInputId();
                var vmOutputId = _audioDeviceService.GetVoicemeeterOutputId();
                var haveRender = !string.IsNullOrWhiteSpace(vmInputId);
                var haveCapture = !string.IsNullOrWhiteSpace(vmOutputId);

                if (string.IsNullOrWhiteSpace(Settings.SavedDefaultRenderId))
                    Settings.SavedDefaultRenderId = _audioDeviceService.GetDefaultDeviceId(DataFlow.Render) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(Settings.SavedDefaultCaptureId))
                    Settings.SavedDefaultCaptureId = _audioDeviceService.GetDefaultDeviceId(DataFlow.Capture) ?? string.Empty;

                bool outputApplied = false;
                bool inputApplied = false;
                string? renderError = null;
                string? captureError = null;
                if (haveRender || haveCapture)
                {
                    if (haveCapture)
                    {
                        inputApplied = _windowsAudioRoutingService.TrySetDefaultInput(vmOutputId!);
                        captureError = _windowsAudioRoutingService.LastError;
                    }
                    if (haveRender)
                    {
                        outputApplied = TrySetDefaultOutputVerified(vmInputId!);
                        renderError = _windowsAudioRoutingService.LastError;
                    }
                }

                if (haveRender)
                {
                    Settings.OutputDeviceId = vmInputId!;
                    Settings.PlaybackDeviceId = vmInputId!;
                }

                if (haveCapture)
                {
                    Settings.InputDeviceId = vmOutputId!;
                    Settings.MicrophoneDeviceId = vmOutputId!;
                }

                Save();

                var result = $"✓  Configured for Voicemeeter:\n   Hear: {hearDevice.Name}\n   Talk: {talkDevice.Name}";

                if (haveRender)
                {
                    if (outputApplied)
                        result += "\n   ✓ App output + Windows playback → VoiceMeeter Input";
                    else
                        result += $"\n   ⚠  Could not set Windows playback → VoiceMeeter Input ({renderError ?? "set failed"})";
                }
                else
                {
                    result += "\n   ⚠  VoiceMeeter Input device not found — playback not routed";
                }

                if (haveCapture)
                {
                    var rb = inputApplied ? _audioDeviceService.GetDefaultDeviceId(DataFlow.Capture) : null;
                    if (inputApplied && string.Equals(rb, vmOutputId, StringComparison.OrdinalIgnoreCase))
                        result += "\n   ✓ Mic + Windows input → VoiceMeeter Output (B1)";
                    else if (inputApplied)
                        result += $"\n   ⚠  Input default is '{rb}' — VoiceMeeter Output (B1) not applied ({captureError ?? "none"})";
                    else
                        result += $"\n   ⚠  Could not set Windows input → VoiceMeeter Output (B1) ({captureError ?? "set failed"})";
                }
                else
                {
                    result += "\n   ⚠  VoiceMeeter Output (B1) device not found — input not routed";
                }

                return result;
            }
            catch (Exception ex)
            {
                return $"✗  Setup failed: {ex.Message}";
            }
        });
    }

    private bool TrySetDefaultOutputVerified(string deviceId)
    {
        for (int i = 0; i < 3; i++)
        {
            bool applied = _windowsAudioRoutingService.TrySetDefaultOutput(deviceId);
            var rb = _audioDeviceService.GetDefaultDeviceId(DataFlow.Render);
            if (applied && string.Equals(rb, deviceId, StringComparison.OrdinalIgnoreCase))
                return true;
            System.Threading.Thread.Sleep(500);
        }
        return false;
    }

    internal async Task<string> ResetVoicemeeterAsync()
    {
        return await Task.Run(() =>
        {
            string vmResult;
            if (!VoicemeeterService.IsVoicemeeterInstalled())
            {
                vmResult = "⚠  Voicemeeter not installed — devices were NOT cleared.";
            }
            else
            {
                using var vm = new VoicemeeterRemote();
                if (vm.Login())
                {
                    vmResult = vm.ResetRouting();
                    vm.Dispose();
                }
                else
                {
                    vmResult = "⚠  Could not log in to Voicemeeter — devices were NOT cleared.";
                }
            }

            var previousRender = Settings.SavedDefaultRenderId;
            var previousCapture = Settings.SavedDefaultCaptureId;
            var restoredDefaults = false;

            if (!string.IsNullOrWhiteSpace(previousRender) || !string.IsNullOrWhiteSpace(previousCapture))
            {
                restoredDefaults = _windowsAudioRoutingService.TrySetDefaultDevices(previousRender ?? string.Empty, previousCapture ?? string.Empty);
                Settings.SavedDefaultRenderId = string.Empty;
                Settings.SavedDefaultCaptureId = string.Empty;
            }

            if (restoredDefaults)
            {
                var render = _audioDeviceService.GetDefaultDeviceId(DataFlow.Render);
                var capture = _audioDeviceService.GetDefaultDeviceId(DataFlow.Capture);
                Settings.OutputDeviceId = render ?? string.Empty;
                Settings.PlaybackDeviceId = render ?? string.Empty;
                Settings.InputDeviceId = capture ?? string.Empty;
                Settings.MicrophoneDeviceId = capture ?? string.Empty;
            }

            Settings.HearDeviceName = string.Empty;
            Settings.TalkDeviceName = string.Empty;
            Settings.VoicemeeterDetected = false;
            Save();

            var line2 = restoredDefaults
                ? "✓  Windows defaults + app I/O restored."
                : "⚠  No saved Windows defaults to restore (or restore failed).";
            return $"{vmResult}\n{line2}";
        });
    }

    internal void OpenMixer(Window owner)
    {
        try
        {
            var panel = new Views.Dialogs.VoicemeeterPanel { Owner = owner };
            panel.ShowDialog();
        }
        catch (Exception ex)
        {
            StatusText = $"Mixer: {ex.Message}";
        }
    }

    internal void OpenSetupWizard(Window owner)
    {
        try
        {
            var wizard = new SetupWizardWindow { Owner = owner };
            wizard.ShowDialog();
            Refresh();
        }
        catch (Exception ex)
        {
            StatusText = $"Setup wizard: {ex.Message}";
        }
    }

    internal List<AudioDeviceInfo> GetPhysicalOutputDevices()
    {
        return OutputDevices.Where(d => !d.IsVirtual).ToList();
    }

    internal int GetVoiceChangerOutputDeviceIndex()
    {
        return ResolveOutputDeviceIndex(Settings.OutputDeviceId);
    }

    internal string GetVoiceChangerOutputDeviceName()
    {
        return ResolveDeviceName(OutputDevices, Settings.OutputDeviceId);
    }

    internal List<AudioDeviceInfo> GetPhysicalInputDevices()
    {
        return InputDevices.Where(d => !d.IsVirtual).ToList();
    }

    internal void Save()
    {
        _config.Sounds = new ObservableCollection<SoundEntry>(Sounds);
        _config.Profiles = new ObservableCollection<Profile>(Profiles);
        _config.Categories = new ObservableCollection<Category>(Categories);
        _config.Settings = Settings;
        _config.ActiveProfileId = SelectedProfile?.Id ?? _config.ActiveProfileId;
        _configService.Save(_config);
        UpdateTitle();
        UpdateRoutingStatus();
    }

    private void RaiseCommandState()
    {
        (DuplicateSoundCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (DeleteSoundCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (PlaySelectedSoundCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (AssignSelectedSoundToKeyCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (RemoveSoundFromKeyCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ChooseKeyImageCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (RenameBindingCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ClearKeyAssignmentCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ClearSelectedSoundBindingCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ChooseSoundImageCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (RemoveSoundImageCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ToggleFavoriteCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (DeleteProfileCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    internal void UpdateStatus()
    {
        StatusText = $"{Sounds.Count} sounds · {Profiles.Count} profiles · {KeyboardKeys.Count} keys";

        UpdateRoutingStatus();
        UpdateBindingPanelState();
    }

    internal void UpdateTitle()
    {
        var presetName = SelectedProfile?.Name ?? ActiveProfile?.Name ?? "Default";
        WindowTitle = $"SoundFX Studio · {presetName}";
        CurrentPreset = presetName;
    }

    internal void UpdateRoutingStatus()
    {
        CurrentOutputDevice = ResolveDeviceName(OutputDevices, Settings.OutputDeviceId);
        CurrentInputDevice = ResolveDeviceName(InputDevices, Settings.InputDeviceId);

        var routingParts = new List<string>
        {
            $"Output: {CurrentOutputDevice}",
            $"Input: {CurrentInputDevice}"
        };

        RoutingStatus = Settings.VBCableDetected
            ? $"Ready · {string.Join(" · ", routingParts)}"
            : $"Needs setup · {string.Join(" · ", routingParts)}";
    }

    private static string ResolveDeviceName(IEnumerable<AudioDeviceInfo> devices, string deviceId, string fallback = "System Default")
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return fallback;
        }

        var device = devices.FirstOrDefault(item => string.Equals(item.Id, deviceId, StringComparison.OrdinalIgnoreCase));
        return device?.Name ?? fallback;
    }

    internal void RaiseSoundCollectionStats()
    {
    }

    private void UpdateBindingPanelState()
    {
        if (SelectedSound is null)
        {
            SelectedSoundBindingStatusText = "Select a sound to see its binding.";
            SelectedSoundBindingPromptText = string.Empty;
            SelectedSoundBindingKeyLabel = "None";
            SelectedSoundBindingShortcut = "None";
            BindingHintText = "Drag a sound onto a key to assign it.";
            return;
        }

        var assignedKey = string.IsNullOrWhiteSpace(SelectedSound.AssignedKeyId)
            ? null
            : KeyboardKeys.FirstOrDefault(item => string.Equals(item.Id, SelectedSound.AssignedKeyId, StringComparison.OrdinalIgnoreCase));

        SelectedSoundBindingKeyLabel = assignedKey?.DisplayLabel ?? "None";
        SelectedSoundBindingShortcut = string.IsNullOrWhiteSpace(SelectedSound.Hotkey) ? "None" : SelectedSound.Hotkey;

        if (IsAssignMode)
        {
            SelectedSoundBindingStatusText = "Assign Mode Active";
            SelectedSoundBindingPromptText = $"Press a key to bind: {SelectedSound.Name}";
            BindingHintText = $"Assign Mode Active - Press a key to bind: {SelectedSound.Name}";
            return;
        }

        BindingHintText = "Drag a sound onto a key to assign it.";

        if (assignedKey is null)
        {
            SelectedSoundBindingStatusText = "No key assigned";
            SelectedSoundBindingPromptText = string.Empty;
            return;
        }

        SelectedSoundBindingStatusText = $"Assigned: {SelectedSound.Name} → {assignedKey.DisplayLabel}";
        SelectedSoundBindingPromptText = string.Empty;
    }

    internal static string GetUniqueFileName(string folder, string fileName)
    {
        var candidate = fileName;
        var baseName = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        var counter = 1;

        while (File.Exists(Path.Combine(folder, candidate)))
        {
            candidate = $"{baseName} ({counter++}){extension}";
        }

        return candidate;
    }

    private static bool IsAudioFile(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return extension is ".mp3" or ".wav" or ".flac" or ".ogg" or ".m4a" or ".aac";
    }

    private static bool IsImageFile(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return extension is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".webp";
    }

    private string EnsureRoutingTestTone()
    {
        var path = Path.Combine(_configService.GetAppFolder(), "routing-test-tone.wav");
        if (File.Exists(path))
        {
            return path;
        }

        const int sampleRate = 44100;
        const int durationMs = 900;
        const double frequency = 440.0;
        const float amplitude = 0.25f;

        using var writer = new WaveFileWriter(path, WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1));
        var sampleCount = sampleRate * durationMs / 1000;

        for (var n = 0; n < sampleCount; n++)
        {
            var sample = (float)(amplitude * Math.Sin(2.0 * Math.PI * frequency * n / sampleRate));
            writer.WriteSample(sample);
        }

        return path;
    }

    private static AudioDeviceInfo? PickBestDevice(IEnumerable<AudioDeviceInfo> devices, bool preferVirtual)
    {
        var list = devices.ToList();
        if (list.Count == 0)
        {
            return null;
        }

        var byDefault = list.FirstOrDefault(device => device.IsDefaultCommunication) ?? list.FirstOrDefault(device => device.IsDefault);
        if (byDefault is not null)
        {
            return byDefault;
        }

        var preferred = preferVirtual
            ? list.FirstOrDefault(device => device.IsVirtual)
            : list.FirstOrDefault(device => !device.IsVirtual);

        return preferred ?? list.First();
    }

    private static string ToKeyToken(Key key, ModifierKeys modifiers)
    {
        var parts = new List<string>();

        if (modifiers.HasFlag(ModifierKeys.Control))
        {
            parts.Add("CTRL");
        }

        if (modifiers.HasFlag(ModifierKeys.Shift))
        {
            parts.Add("SHIFT");
        }

        if (modifiers.HasFlag(ModifierKeys.Alt))
        {
            parts.Add("ALT");
        }

        if (modifiers.HasFlag(ModifierKeys.Windows))
        {
            parts.Add("WIN");
        }

        var token = key switch
        {
            Key.Escape => "ESC",
            Key.Back => "BACKSPACE",
            Key.Tab => "TAB",
            Key.CapsLock => "CAPS LOCK",
            Key.LeftShift or Key.RightShift => "SHIFT",
            Key.LeftCtrl or Key.RightCtrl => "CTRL",
            Key.LeftAlt or Key.RightAlt => "ALT",
            Key.LWin or Key.RWin => "WIN",
            Key.Apps => "MENU",
            Key.PrintScreen => "PRINT SCREEN",
            Key.Scroll => "SCROLL LOCK",
            Key.Pause => "PAUSE",
            Key.NumLock => "NUM LOCK",
            Key.PageUp => "PAGE UP",
            Key.PageDown => "PAGE DOWN",
            Key.D0 => "0",
            Key.D1 => "1",
            Key.D2 => "2",
            Key.D3 => "3",
            Key.D4 => "4",
            Key.D5 => "5",
            Key.D6 => "6",
            Key.D7 => "7",
            Key.D8 => "8",
            Key.D9 => "9",
            Key.NumPad0 => "NUMPAD0",
            Key.NumPad1 => "NUMPAD1",
            Key.NumPad2 => "NUMPAD2",
            Key.NumPad3 => "NUMPAD3",
            Key.NumPad4 => "NUMPAD4",
            Key.NumPad5 => "NUMPAD5",
            Key.NumPad6 => "NUMPAD6",
            Key.NumPad7 => "NUMPAD7",
            Key.NumPad8 => "NUMPAD8",
            Key.NumPad9 => "NUMPAD9",
            Key.Add => "+",
            Key.Subtract => "-",
            Key.Multiply => "*",
            Key.Divide => "/",
            Key.Decimal => ".",
            Key.Space => "SPACE",
            Key.Return => "ENTER",
            Key.OemTilde => "`",
            Key.OemMinus => "-",
            Key.OemPlus => "=",
            Key.OemOpenBrackets => "[",
            Key.Oem6 => "]",
            Key.Oem5 => "\\",
            Key.Oem1 => ";",
            Key.Oem7 => "'",
            Key.OemComma => ",",
            Key.OemPeriod => ".",
            Key.OemQuestion => "/",
            Key.Oem102 => "OEM102",
            Key.Left => "LEFT",
            Key.Right => "RIGHT",
            Key.Up => "UP",
            Key.Down => "DOWN",
            _ => key.ToString().ToUpperInvariant()
        };

        parts.Add(token);
        return string.Join("+", parts);
    }

    private string NormalizeTokenForLayout(string token)
    {
        if (ResolveKeyboardLayoutMode() != KeyboardLayoutMode.German)
        {
            return token;
        }

        var parts = token.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return token;
        }

        var lastIndex = parts.Length - 1;
        parts[lastIndex] = parts[lastIndex].ToUpperInvariant() switch
        {
            "Y" => "Z",
            "Z" => "Y",
            _ => parts[lastIndex]
        };

        return string.Join("+", parts);
    }

    private KeyboardLayoutMode ResolveKeyboardLayoutMode()
        => KeyboardLayoutMode.EnglishUS;

    private static KeyboardLayoutMode ResolveWindowsKeyboardLayout(CultureInfo? culture)
    {
        if (culture is null)
        {
            return KeyboardLayoutMode.EnglishUS;
        }

        if (string.Equals(culture.Name, "de-DE", StringComparison.OrdinalIgnoreCase)
            || string.Equals(culture.TwoLetterISOLanguageName, "de", StringComparison.OrdinalIgnoreCase))
        {
            return KeyboardLayoutMode.German;
        }

        if (string.Equals(culture.Name, "en-GB", StringComparison.OrdinalIgnoreCase))
        {
            return KeyboardLayoutMode.EnglishUK;
        }

        if (string.Equals(culture.Name, "en-US", StringComparison.OrdinalIgnoreCase))
        {
            return KeyboardLayoutMode.EnglishUS;
        }

        return KeyboardLayoutMode.EnglishUS;
    }

    internal void UpdateKeyVisualState(KeyboardKey key) => _keyboardViewModel.UpdateKeyVisualState(key);

    private KeyboardCalibrationSettings GetKeyboardCalibration()
        => Settings.KeyboardCalibration ?? new KeyboardCalibrationSettings();

    private static double NormalizeLampY(double value)
        => value < 220 ? value + 70 : value;

    private void NotifyKeyboardLampProperties()
    {
        OnPropertyChanged(nameof(CapsLockIndicatorOffsetX));
        OnPropertyChanged(nameof(CapsLockIndicatorOffsetY));
        OnPropertyChanged(nameof(NumLockIndicatorOffsetX));
        OnPropertyChanged(nameof(NumLockIndicatorOffsetY));
        OnPropertyChanged(nameof(ScrollLockIndicatorOffsetX));
        OnPropertyChanged(nameof(ScrollLockIndicatorOffsetY));
    }

    private async Task TrackPlaybackAsync(string soundId, string? keyId)
    {
        while (_audioPlayer.IsPlaying(soundId))
        {
            await Task.Delay(80).ConfigureAwait(false);
        }

        if (string.IsNullOrWhiteSpace(keyId))
        {
            return;
        }

        RunOnUiThread(() =>
        {
            var key = KeyboardKeys.FirstOrDefault(item => string.Equals(item.Id, keyId, StringComparison.OrdinalIgnoreCase));
            if (key is not null)
            {
                UpdateKeyVisualState(key);
            }
        });
    }

    internal void RunOnUiThread(Action action)
    {
        if (_window?.Dispatcher.CheckAccess() == true)
        {
            action();
            return;
        }

        if (_window?.Dispatcher is not null)
        {
            _window.Dispatcher.BeginInvoke(action);
            return;
        }

        action();
    }

    private ActionDefinition EnsureSoundAction(SoundEntry sound)
    {
        var existing = _config.Actions.FirstOrDefault(action => action.Type == ActionType.Sound && string.Equals(action.Payload, sound.Id, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            return existing;
        }

        var action = new ActionDefinition
        {
            Name = sound.Name,
            Description = $"Play {sound.Name}",
            Type = ActionType.Sound,
            IconPath = sound.ImagePath ?? string.Empty,
            Category = sound.Category,
            Payload = sound.Id
        };

        _config.Actions.Add(action);
        return action;
    }

    private int ResolveOutputDeviceIndex(string deviceId)
    {
        var device = OutputDevices.FirstOrDefault(item => string.Equals(item.Id, deviceId, StringComparison.OrdinalIgnoreCase));
        if (device is null)
        {
            return -1;
        }

        return ResolveWaveOutIndex(device.Name);
    }

    private int ResolveWaveOutIndex(string deviceName)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
        {
            return -1;
        }

        try
        {
            var trimmed = deviceName.Trim();
            for (var i = 0; i < WaveOut.DeviceCount; i++)
            {
                var productName = WaveOut.GetCapabilities(i).ProductName;
                var truncatedFriendly = trimmed.Length > 31 ? trimmed[..31] : trimmed;
                if (productName.StartsWith(truncatedFriendly, StringComparison.OrdinalIgnoreCase) ||
                    truncatedFriendly.StartsWith(productName, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
        }
        catch
        {
            // fall back to the default device
        }

        return -1;
    }

    private void RaiseCommandStateIfNeeded() => RaiseCommandState();
}