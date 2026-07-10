using Microsoft.Win32;
using SoundFXStudio.Infrastructure;
using SoundFXStudio.Models;
using SoundFXStudio.Services;
using SoundFXStudio.Views.Dialogs;
using NAudio.Wave;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using Key = System.Windows.Input.Key;

namespace SoundFXStudio.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly ConfigService _configService = new();
    private readonly KeyboardLayoutService _keyboardLayoutService = new();
    private readonly AudioPlayer _audioPlayer = new();
    private readonly HotkeyService _hotkeyService = new();
    private readonly KeyboardHookService _keyboardHookService = new();
    private readonly AudioDeviceService _audioDeviceService = new();
    private readonly HttpClient _httpClient = new();
    private readonly HashSet<string> _pressedKeys = new(); // Track multi-key presses
    private readonly Dictionary<string, CancellationTokenSource> _unhighlightTimers = new(); // Auto-unhighlight fallback
    private AppConfig _config = new();
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

    public MainViewModel()
    {
        KeyboardKeys = new ObservableCollection<KeyboardKey>();
        Sounds = new ObservableCollection<SoundEntry>();
        Profiles = new ObservableCollection<Profile>();
        Categories = new ObservableCollection<Category>();
        OutputDevices = new ObservableCollection<AudioDeviceInfo>();
        InputDevices = new ObservableCollection<AudioDeviceInfo>();

        AddSoundCommand = new RelayCommand(_ => AddSound());
        AddMultipleSoundsCommand = new RelayCommand(_ => AddMultipleSounds());
        AddSoundFromUrlCommand = new AsyncRelayCommand(_ => AddSoundFromUrlAsync());
        DeleteMarkedSoundsCommand = new RelayCommand(_ => DeleteMarkedSounds());
        StopAllCommand = new RelayCommand(_ => StopAll());
        KeyClickedCommand = new RelayCommand(parameter => HandleKeyClicked(parameter));
        DuplicateSoundCommand = new RelayCommand(_ => DuplicateSelectedSound(), _ => SelectedSound is not null);
        DeleteSoundCommand = new RelayCommand(_ => DeleteSelectedSound(), _ => SelectedSound is not null);
        PlaySelectedSoundCommand = new RelayCommand(_ => PlaySelectedSound(), _ => SelectedSound is not null);
        PlaySoundCommand = new RelayCommand(parameter => PlaySoundFromLibrary(parameter), parameter => ResolveSound(parameter) is not null);
        EditSoundCommand = new RelayCommand(parameter => EditSound(parameter), parameter => ResolveSound(parameter) is not null);
        AssignSelectedSoundToKeyCommand = new RelayCommand(parameter => AssignSelectedSoundToSelectedKey(parameter), parameter => SelectedSound is not null && (parameter is KeyboardKey || SelectedKey is not null));
        RemoveSoundFromKeyCommand = new RelayCommand(parameter => RemoveSoundFromKey(parameter), parameter => ResolveKey(parameter) is not null);
        RemoveKeyImageCommand = new RelayCommand(parameter => RemoveKeyImage(parameter), parameter => ResolveKey(parameter) is not null);
        ChangeKeyVolumeCommand = new RelayCommand(parameter => ChangeKeyVolume(parameter), parameter => ResolveKey(parameter) is not null);
        ToggleKeyLoopCommand = new RelayCommand(parameter => ToggleKeyLoop(parameter), parameter => ResolveKey(parameter) is not null);
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
        SetSoundHotkeyCommand = new RelayCommand(parameter => SetSoundHotkey(parameter), parameter => ResolveSound(parameter) is not null);
        HotkeyService = _hotkeyService;

        Load();
        RebuildKeyboard();
        ConfigureViews();
        UpdateTitle();
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

    public HotkeyService HotkeyService { get; }

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

    public ICommand ToggleKeyLoopCommand { get; }

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

    public ICommand SetSoundHotkeyCommand { get; }

    public IReadOnlyList<KeyboardLayoutMode> KeyboardLayoutOptions { get; } = Enum.GetValues<KeyboardLayoutMode>();

    public IReadOnlyList<string> ThemeOptions { get; } = new[] { "Dark", "Light" };

    public IReadOnlyList<SoundEntry> MostPlayedSounds => Sounds.OrderByDescending(item => item.PlayCount).Take(8).ToList();

    public IReadOnlyList<SoundEntry> RecentSounds => Sounds.Where(item => item.LastPlayedUtc is not null).OrderByDescending(item => item.LastPlayedUtc).Take(8).ToList();

    public IReadOnlyList<SoundEntry> FavoriteSounds => Sounds.Where(item => item.IsFavorite).Take(8).ToList();

    public string SelectedTheme
    {
        get => Settings.Theme;
        set
        {
            if (Settings.Theme == value)
            {
                return;
            }

            Settings.Theme = value;
            Save();
            OnPropertyChanged(nameof(SelectedTheme));
        }
    }

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
            RebuildKeyboard();
            Save();
            OnPropertyChanged(nameof(KeyboardLayout));
        }
    }

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
                    UpdateKeyVisualState(previous);
                }

                if (_selectedKey is not null)
                {
                    _selectedKey.IsSelected = true;
                    UpdateKeyVisualState(_selectedKey);
                }

                UpdateStatus();
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
                RaiseCommandState();
            }
        }
    }

    public Profile? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (SetProperty(ref _selectedProfile, value) && value is not null)
            {
                _config.ActiveProfileId = value.Id;
                RefreshAssignments();
                Save();
                UpdateTitle();
                RaiseCommandState();
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

    public AppSettings Settings => _config.Settings;

    public void AttachWindow(Window window)
    {
        _window = window;
        _hotkeyService.Attach(window);
        _hotkeyService.HotkeyPressed += (_, args) =>
        {
            var assignment = ActiveProfile?.Assignments.FirstOrDefault(item => string.Equals(item.Id, args.OwnerId, StringComparison.OrdinalIgnoreCase));
            if (assignment is null)
            {
                return;
            }

            var sound = Sounds.FirstOrDefault(item => string.Equals(item.Id, assignment.SoundId, StringComparison.OrdinalIgnoreCase));
            if (sound is not null)
            {
                PlaySound(sound, assignment);
            }
        };

        _keyboardHookService.KeyDown += (_, args) =>
        {
            if (_window?.IsActive == true)
            {
                return;
            }

            HandlePhysicalKey(args.Key);
        };
        _keyboardHookService.Attach();

        RegisterGlobalHotkeys();
    }

    public void HandlePreviewKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        HandlePhysicalKey(e.Key, isKeyDown: true);
    }

    public void HandlePreviewKeyUp(System.Windows.Input.KeyEventArgs e)
    {
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

        Save();
    }

    private Profile? ActiveProfile => Profiles.FirstOrDefault(item => string.Equals(item.Id, _config.ActiveProfileId, StringComparison.OrdinalIgnoreCase)) ?? Profiles.FirstOrDefault();

    private void Load()
    {
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
        _config.Settings.PropertyChanged += Settings_PropertyChanged;
        RefreshAssignments();
        UpdateRoutingStatus();
        UpdateStatus();
        RaiseSoundCollectionStats();
    }

    private void Settings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AppSettings.OutputDeviceId)
            or nameof(AppSettings.InputDeviceId)
            or nameof(AppSettings.VirtualCableDeviceId)
            or nameof(AppSettings.VBCableDetected))
        {
            UpdateRoutingStatus();
            Save();
        }
    }

    private void RebuildKeyboard()
    {
        var selectedKeyId = SelectedKey?.Id ?? _selectedKeyId;
        var keys = _keyboardLayoutService.CreateKeyboard(Settings.KeyboardLayout);
        KeyboardKeys.Clear();
        foreach (var key in keys)
        {
            KeyboardKeys.Add(key);
        }

        SelectedKey = selectedKeyId is null ? null : KeyboardKeys.FirstOrDefault(item => string.Equals(item.Id, selectedKeyId, StringComparison.OrdinalIgnoreCase));
        RefreshAssignments();
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

    private void RefreshAssignments()
    {
        var profile = ActiveProfile;
        if (profile is null)
        {
            return;
        }

        var selectedKeyId = SelectedKey?.Id;

        foreach (var key in KeyboardKeys)
        {
            var assignment = profile.Assignments.FirstOrDefault(item => string.Equals(item.KeyId, key.Id, StringComparison.OrdinalIgnoreCase));
            var sound = assignment is null ? null : Sounds.FirstOrDefault(item => string.Equals(item.Id, assignment.SoundId, StringComparison.OrdinalIgnoreCase));
            var category = sound is null ? null : Categories.FirstOrDefault(item => string.Equals(item.Name, sound.Category, StringComparison.OrdinalIgnoreCase));

            key.ImagePath = assignment?.ImagePath ?? sound?.ImagePath;
            key.AssignedSoundId = assignment?.SoundId;
            key.AssignedSoundName = sound?.Name;
            key.AssignmentName = assignment?.BindingName;
            key.CategoryAccentColor = string.IsNullOrWhiteSpace(category?.AccentColor) ? "#00000000" : category.AccentColor;
            key.IsSelected = string.Equals(key.Id, selectedKeyId, StringComparison.OrdinalIgnoreCase);
            UpdateKeyVisualState(key);
            key.IsEnabled = true;
        }
    }

    private void HandlePhysicalKey(Key key, bool isKeyDown = true)
    {
        var token = NormalizeTokenForLayout(ToKeyToken(key, ModifierKeys.None));
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        var keyboardKey = KeyboardKeys.FirstOrDefault(item => string.Equals(item.KeyName, token, StringComparison.OrdinalIgnoreCase));
        if (keyboardKey is null)
        {
            return;
        }

        RunOnUiThread(() =>
        {
            if (isKeyDown)
            {
                // Cancel existing unhighlight timer for this key (user is pressing again)
                if (_unhighlightTimers.TryGetValue(token, out var cts))
                {
                    cts.Cancel();
                    _unhighlightTimers.Remove(token);
                }

                // Track multi-key presses
                _pressedKeys.Add(token);
                keyboardKey.IsSelected = true; // Show visual feedback
                FlashKey(keyboardKey);
                PlayKey(keyboardKey);

                // Start auto-unhighlight timer (fallback in case KeyUp doesn't fire)
                var unhighlightCts = new CancellationTokenSource();
                _unhighlightTimers[token] = unhighlightCts;

                Task.Delay(300, unhighlightCts.Token).ContinueWith(_ =>
                {
                    if (!unhighlightCts.Token.IsCancellationRequested)
                    {
                        RunOnUiThread(() =>
                        {
                            keyboardKey.IsSelected = false;
                            _unhighlightTimers.Remove(token);
                        });
                    }
                });
            }
            else
            {
                // Release key - cancel timer and deselect immediately
                if (_unhighlightTimers.TryGetValue(token, out var cts))
                {
                    cts.Cancel();
                    _unhighlightTimers.Remove(token);
                }

                _pressedKeys.Remove(token);
                keyboardKey.IsSelected = false;
            }
        });
    }

    private static ModifierKeys GetModifierState()
        => (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl) ? ModifierKeys.Control : ModifierKeys.None)
           | (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt) ? ModifierKeys.Alt : ModifierKeys.None)
           | (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift) ? ModifierKeys.Shift : ModifierKeys.None)
           | (Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin) ? ModifierKeys.Windows : ModifierKeys.None);

    private void PlayKey(KeyboardKey key)
    {
        var profile = ActiveProfile;
        var assignment = profile?.Assignments.FirstOrDefault(item => string.Equals(item.KeyId, key.Id, StringComparison.OrdinalIgnoreCase));
        var sound = assignment is null ? null : Sounds.FirstOrDefault(item => string.Equals(item.Id, assignment.SoundId, StringComparison.OrdinalIgnoreCase));

        if (sound is null)
        {
            return;
        }

        PlaySound(sound, assignment);
    }

    private void PlaySound(SoundEntry sound, KeyAssignment? assignment = null)
    {
        if (!File.Exists(sound.FilePath))
        {
            RunOnUiThread(() => StatusText = $"Missing file: {sound.Name}");
            return;
        }

        var deviceId = Settings.OutputDeviceId;
        var deviceIndex = OutputDevices.ToList().FindIndex(device => string.Equals(device.Id, deviceId, StringComparison.OrdinalIgnoreCase));

        _audioPlayer.Play(
            sound.Id,
            sound.FilePath,
            assignment?.VolumeOverride ?? sound.Volume,
            assignment?.Loop ?? sound.Loop,
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
        if (parameter is not KeyboardKey key)
        {
            return;
        }

        SelectedKey = key;

        if (key.AssignedSoundId is null)
        {
            StatusText = $"Selected {key.DisplayLabel}";
            return;
        }

        PlayKey(key);
    }

    private void FlashKey(KeyboardKey key)
    {
        key.State = KeyState.Pressed;
        Task.Delay(120).ContinueWith(_ =>
        {
            RunOnUiThread(() => UpdateKeyVisualState(key));
        });
    }

    private void AddSound()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Add Sound",
            Filter = "Audio Files|*.mp3;*.wav;*.flac;*.ogg;*.m4a|All Files|*.*",
            Multiselect = true
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        foreach (var fileName in dialog.FileNames)
        {
            if (!TryGetSoundDetails(fileName, null, out var details))
            {
                continue;
            }

            AddSoundFromDetails(details);
        }

        Save();
        UpdateStatus();
        RaiseSoundCollectionStats();
    }

    private void AddMultipleSounds() => AddSound();

    private Task AddSoundFromUrlAsync()
    {
        var input = Microsoft.VisualBasic.Interaction.InputBox("Paste an audio URL", "Add Sound From URL", string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(input))
        {
            return Task.CompletedTask;
        }

        if (!Uri.TryCreate(input, UriKind.Absolute, out var uri) || (uri.Scheme is not "http" and not "https"))
        {
            StatusText = "Enter a valid http or https audio URL";
            return Task.CompletedTask;
        }

        return ImportSoundFromUrlAsync(uri);
    }

    private void DeleteMarkedSounds()
    {
        var marked = Sounds.Where(item => item.IsMarkedForDelete).ToList();
        if (marked.Count == 0)
        {
            return;
        }

        foreach (var sound in marked)
        {
            Sounds.Remove(sound);
            _config.Sounds.Remove(sound);
        }

        RefreshAssignments();
        Save();
        UpdateStatus();
        SoundsView.Refresh();
        RaiseSoundCollectionStats();
    }

    private SoundEntry? ResolveSound(object? parameter) => parameter as SoundEntry ?? SelectedSound;

    private bool TryGetSoundDetails(string? initialFilePath, SoundEntry? existingSound, out SoundAssignmentViewModel details)
    {
        details = new SoundAssignmentViewModel(KeyboardKeys);

        if (existingSound is not null)
        {
            details.FilePath = existingSound.FilePath;
            details.ImagePath = existingSound.ImagePath ?? string.Empty;
            details.Name = existingSound.Name;
            details.Category = existingSound.Category;
            details.VolumePercent = existingSound.Volume * 100;
            details.IsFavorite = existingSound.IsFavorite;
            details.Loop = existingSound.Loop;
            details.SelectedKey = GetAssignedKeyIdForSound(existingSound) ?? string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(initialFilePath) && string.IsNullOrWhiteSpace(details.FilePath))
        {
            details.FilePath = initialFilePath;
            details.Name = Path.GetFileNameWithoutExtension(initialFilePath);
            details.Category = string.IsNullOrWhiteSpace(details.Category) ? "Custom" : details.Category;
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

    private string? GetAssignedKeyIdForSound(SoundEntry sound)
    {
        var profile = ActiveProfile;
        if (profile is null)
        {
            return null;
        }

        return profile.Assignments.FirstOrDefault(item => string.Equals(item.SoundId, sound.Id, StringComparison.OrdinalIgnoreCase))?.KeyId;
    }

    private void AddSoundFromDetails(SoundAssignmentViewModel details)
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
            Category = string.IsNullOrWhiteSpace(details.Category) ? "Custom" : details.Category.Trim(),
            Volume = (float)Math.Clamp(details.VolumePercent / 100.0, 0.0, 1.0),
            IsFavorite = details.IsFavorite,
            Loop = details.Loop
        };

        if (!string.IsNullOrWhiteSpace(details.ImagePath) && File.Exists(details.ImagePath))
        {
            sound.ImagePath = ImportImage(details.ImagePath);
        }

        Sounds.Add(sound);
        _config.Sounds.Add(sound);

        AssignSoundToKeyIfSelected(sound, details.SelectedKey);

        RefreshAssignments();
        SoundsView.Refresh();
        SelectedSound = sound;
        Save();
        StatusText = $"Added {sound.Name}";
        RaiseSoundCollectionStats();
    }

    private void AssignSoundToKeyIfSelected(SoundEntry sound, string? keyId)
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
            assignment = new KeyAssignment { KeyId = key.Id, SoundId = sound.Id };
            profile.Assignments.Add(assignment);
        }
        else
        {
            assignment.SoundId = sound.Id;
        }

        sound.AssignedKeyId = key.Id;
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
        sound.Category = string.IsNullOrWhiteSpace(details.Category) ? sound.Category : details.Category.Trim();
        sound.Volume = (float)Math.Clamp(details.VolumePercent / 100.0, 0.0, 1.0);
        sound.IsFavorite = details.IsFavorite;
        sound.Loop = details.Loop;

        if (!string.IsNullOrWhiteSpace(details.ImagePath) && File.Exists(details.ImagePath))
        {
            sound.ImagePath = ImportImage(details.ImagePath);
        }
        else if (string.IsNullOrWhiteSpace(details.ImagePath))
        {
            sound.ImagePath = null;
        }

        UpdateSoundKeyAssignment(sound, details.SelectedKey);

        RefreshAssignments();
        Save();
        SelectedSound = sound;
        StatusText = $"Updated {sound.Name}";
        RaiseSoundCollectionStats();
    }

    private void UpdateSoundKeyAssignment(SoundEntry sound, string? keyId)
    {
        var profile = ActiveProfile;
        if (profile is null)
        {
            return;
        }

        var existingAssignment = profile.Assignments.FirstOrDefault(item => string.Equals(item.SoundId, sound.Id, StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrWhiteSpace(keyId))
        {
            if (existingAssignment is not null)
            {
                profile.Assignments.Remove(existingAssignment);
            }

            sound.AssignedKeyId = null;
            return;
        }

        var key = KeyboardKeys.FirstOrDefault(item => string.Equals(item.Id, keyId, StringComparison.OrdinalIgnoreCase));
        if (key is null)
        {
            return;
        }

        var assignment = profile.Assignments.FirstOrDefault(item => string.Equals(item.KeyId, key.Id, StringComparison.OrdinalIgnoreCase));
        if (assignment is null)
        {
            assignment = new KeyAssignment { KeyId = key.Id, SoundId = sound.Id };
            profile.Assignments.Add(assignment);
        }
        else
        {
            assignment.SoundId = sound.Id;
        }

        if (existingAssignment is not null && !string.Equals(existingAssignment.KeyId, key.Id, StringComparison.OrdinalIgnoreCase))
        {
            profile.Assignments.Remove(existingAssignment);
        }

        sound.AssignedKeyId = key.Id;
    }

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

        sound.Name = input.Trim();
        Save();
        SelectedSound = sound;
        StatusText = $"Renamed sound to {sound.Name}";
    }

    private void ChooseSelectedSoundImage(object? parameter)
    {
        var sound = ResolveSound(parameter);
        if (sound is null)
        {
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "Choose Sound Image",
            Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp|All Files|*.*"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        sound.ImagePath = ImportImage(dialog.FileName);
        Save();
        SelectedSound = sound;
        StatusText = $"Updated image for {sound.Name}";
    }

    private void SetSoundHotkey(object? parameter)
    {
        var sound = ResolveSound(parameter);
        if (sound is null)
        {
            return;
        }

        var current = string.IsNullOrWhiteSpace(sound.Hotkey) ? "F1" : sound.Hotkey;
        var input = Microsoft.VisualBasic.Interaction.InputBox("Enter a hotkey like CTRL+SHIFT+F1 or NUMPAD1", "Hotkey Editor", current).Trim();
        if (string.IsNullOrWhiteSpace(input))
        {
            return;
        }

        sound.Hotkey = input;
        if (SelectedSound is not null && string.Equals(SelectedSound.Id, sound.Id, StringComparison.OrdinalIgnoreCase))
        {
            SelectedSound = sound;
        }

        RegisterGlobalHotkeys();
        Save();
        StatusText = $"Shortcut set for {sound.Name}";
    }

    private void ImportSound(string fileName)
    {
        var destinationFolder = _configService.GetSoundsFolder();
        var destinationFile = Path.Combine(destinationFolder, GetUniqueFileName(destinationFolder, Path.GetFileName(fileName)));
        if (!string.Equals(Path.GetFullPath(fileName), Path.GetFullPath(destinationFile), StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(fileName, destinationFile, true);
        }

        var sound = new SoundEntry
        {
            Name = Path.GetFileNameWithoutExtension(fileName),
            FilePath = destinationFile,
            Category = "Custom"
        };

        Sounds.Add(sound);
        _config.Sounds.Add(sound);
        SoundsView.Refresh();
        StatusText = $"Imported {sound.Name}";
        RaiseSoundCollectionStats();
    }

    private async Task ImportSoundFromUrlAsync(Uri uri)
    {
        try
        {
            using var response = await _httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var destinationFolder = _configService.GetSoundsFolder();
            var fileName = ResolveFileNameFromUrl(uri, response);
            var destinationFile = Path.Combine(destinationFolder, GetUniqueFileName(destinationFolder, fileName));

            await using (var sourceStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
            await using (var destinationStream = File.Create(destinationFile))
            {
                await sourceStream.CopyToAsync(destinationStream).ConfigureAwait(false);
            }

            var sound = new SoundEntry
            {
                Name = Path.GetFileNameWithoutExtension(destinationFile),
                FilePath = destinationFile,
                Category = "Custom"
            };

            RunOnUiThread(() =>
            {
                Sounds.Add(sound);
                _config.Sounds.Add(sound);
                SoundsView.Refresh();
                StatusText = $"Imported {sound.Name} from URL";
                RaiseSoundCollectionStats();
                Save();
            });
        }
        catch (Exception ex)
        {
            RunOnUiThread(() => StatusText = $"URL import failed: {ex.Message}");
        }
    }

    private static string ResolveFileNameFromUrl(Uri uri, HttpResponseMessage response)
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

    private string ImportImage(string fileName)
    {
        var destinationFolder = _configService.GetImagesFolder();
        var destinationFile = Path.Combine(destinationFolder, GetUniqueFileName(destinationFolder, Path.GetFileName(fileName)));
        if (!string.Equals(Path.GetFullPath(fileName), Path.GetFullPath(destinationFile), StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(fileName, destinationFile, true);
        }

        return destinationFile;
    }

    private void DuplicateSelectedSound()
    {
        if (SelectedSound is null)
        {
            return;
        }

        var original = SelectedSound;
        var duplicate = new SoundEntry
        {
            Name = $"{original.Name} Copy",
            FilePath = original.FilePath,
            Volume = original.Volume,
            ImagePath = original.ImagePath,
            Category = original.Category,
            Loop = original.Loop,
            IsFavorite = original.IsFavorite,
            Hotkey = string.Empty
        };

        Sounds.Add(duplicate);
        _config.Sounds.Add(duplicate);
        SelectedSound = duplicate;
        Save();
    }

    private void DeleteSelectedSound()
    {
        if (SelectedSound is null)
        {
            return;
        }

        var sound = SelectedSound;
        if (System.Windows.MessageBox.Show($"Delete '{sound.Name}'?", "Delete Sound", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question) != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        Sounds.Remove(sound);
        _config.Sounds.Remove(sound);

        foreach (var profile in Profiles)
        {
            var assignment = profile.Assignments.FirstOrDefault(item => string.Equals(item.SoundId, sound.Id, StringComparison.OrdinalIgnoreCase));
            if (assignment is not null)
            {
                profile.Assignments.Remove(assignment);
            }
        }

        SelectedSound = null;
        RefreshAssignments();
        Save();
    }

    private void PlaySelectedSound()
    {
        if (SelectedSound is not null)
        {
            PlaySound(SelectedSound);
        }
    }

    private void PlaySoundFromLibrary(object? parameter)
    {
        var sound = ResolveSound(parameter);
        if (sound is null)
        {
            return;
        }

        PlaySound(sound);
    }

    private void AssignSelectedSoundToSelectedKey(object? parameter)
    {
        var key = ResolveKey(parameter);
        if (key is null || SelectedSound is null)
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
            assignment = new KeyAssignment { KeyId = key.Id, SoundId = SelectedSound.Id };
            profile.Assignments.Add(assignment);
        }
        else
        {
            assignment.SoundId = SelectedSound.Id;
        }

        SelectedSound.AssignedKeyId = key.Id;
        RefreshAssignments();
        Save();
        StatusText = $"Assigned {SelectedSound.Name} to {key.DisplayLabel}";
        SelectedKey = key;
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

        var current = Math.Clamp(assignment.VolumeOverride, 0f, 1f);
        var input = Microsoft.VisualBasic.Interaction.InputBox("Volume 0.0 - 1.0", "Key Volume", current.ToString("0.00"));
        if (!float.TryParse(input, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var volume))
        {
            return;
        }

        assignment.VolumeOverride = Math.Clamp(volume, 0f, 1f);
        Save();
        SelectedKey = key;
        StatusText = $"Volume set for {key.DisplayLabel}";
    }

    private void ToggleKeyLoop(object? parameter)
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

        assignment.Loop = !assignment.Loop;
        Save();
        SelectedKey = key;
        StatusText = $"Loop {(assignment.Loop ? "enabled" : "disabled")} for {key.DisplayLabel}";
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

        _audioPlayer.Stop(assignment.SoundId);
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
            BindingName = assignment.BindingName,
            ImagePath = assignment.ImagePath,
            HotkeyText = assignment.HotkeyText,
            IsGlobal = assignment.IsGlobal,
            VolumeOverride = assignment.VolumeOverride,
            Loop = assignment.Loop,
            FadeOutMs = assignment.FadeOutMs,
            StopOnReplay = assignment.StopOnReplay
        };

        profile.Assignments.Add(clone);
        RefreshAssignments();
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
        var input = Microsoft.VisualBasic.Interaction.InputBox("Binding name", "Rename Binding", current);
        if (string.IsNullOrWhiteSpace(input))
        {
            return;
        }

        var assignment = profile.Assignments.FirstOrDefault(item => string.Equals(item.KeyId, key.Id, StringComparison.OrdinalIgnoreCase));
        if (assignment is null)
        {
            assignment = new KeyAssignment { KeyId = key.Id, SoundId = string.Empty };
            profile.Assignments.Add(assignment);
        }

        assignment.BindingName = input.Trim();
        RefreshAssignments();
        Save();
        SelectedKey = key;
        StatusText = $"Renamed binding on {key.DisplayLabel}";
    }

    private KeyboardKey? ResolveKey(object? parameter) => parameter as KeyboardKey ?? SelectedKey;

    private KeyAssignment? GetAssignmentForKey(KeyboardKey key)
    {
        var profile = ActiveProfile;
        if (profile is null)
        {
            return null;
        }

        return profile.Assignments.FirstOrDefault(item => string.Equals(item.KeyId, key.Id, StringComparison.OrdinalIgnoreCase));
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

        var current = string.IsNullOrWhiteSpace(SelectedSound.Hotkey) ? "F1" : SelectedSound.Hotkey;
        var input = Microsoft.VisualBasic.Interaction.InputBox("Enter a hotkey like CTRL+SHIFT+F1 or NUMPAD1", "Hotkey Editor", current);
        if (string.IsNullOrWhiteSpace(input))
        {
            return;
        }

        SelectedSound.Hotkey = input;
        var assignment = ActiveProfile?.Assignments.FirstOrDefault(item => string.Equals(item.SoundId, SelectedSound.Id, StringComparison.OrdinalIgnoreCase));
        if (assignment is null && SelectedKey is not null)
        {
            assignment = new KeyAssignment { KeyId = SelectedKey.Id, SoundId = SelectedSound.Id };
            ActiveProfile?.Assignments.Add(assignment);
        }

        if (assignment is not null)
        {
            assignment.HotkeyText = SelectedSound.Hotkey;
        }

        RegisterGlobalHotkeys();
        Save();
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
    }

    private void StopAll()
    {
        _audioPlayer.StopAll();
        StatusText = "Stopped all sounds";
    }

    private void Refresh()
    {
        Load();
        RegisterGlobalHotkeys();
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

        var deviceIndex = OutputDevices.ToList().FindIndex(device => string.Equals(device.Id, selectedOutput.Id, StringComparison.OrdinalIgnoreCase));
        var testTonePath = EnsureRoutingTestTone();

        _audioPlayer.Play("routing-test", testTonePath, 0.8f, false, deviceIndex);
        StatusText = $"Routing test playing through {selectedOutput.Name}";
        UpdateRoutingStatus();
    }

    private void Save()
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

    private void RegisterGlobalHotkeys()
    {
        _hotkeyService.Clear();

        if (!Settings.EnableGlobalHotkeys)
        {
            return;
        }

        var profile = ActiveProfile;
        if (profile is null)
        {
            return;
        }

        foreach (var assignment in profile.Assignments.Where(item => !string.IsNullOrWhiteSpace(item.HotkeyText)))
        {
            _hotkeyService.Register(assignment.Id, assignment.HotkeyText);
        }
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
        (ToggleFavoriteCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (DeleteProfileCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private void UpdateStatus()
    {
        StatusText = $"{Sounds.Count} sounds · {Profiles.Count} profiles · {KeyboardKeys.Count} keys";

        UpdateRoutingStatus();
    }

    private void UpdateTitle()
    {
        var presetName = SelectedProfile?.Name ?? ActiveProfile?.Name ?? "Default";
        WindowTitle = $"SoundFX Studio · {presetName}";
        CurrentPreset = presetName;
    }

    private void UpdateRoutingStatus()
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

    private void RaiseSoundCollectionStats()
    {
        OnPropertyChanged(nameof(MostPlayedSounds));
        OnPropertyChanged(nameof(RecentSounds));
        OnPropertyChanged(nameof(FavoriteSounds));
    }

    private static string GetUniqueFileName(string folder, string fileName)
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
        if (Settings.KeyboardLayout != KeyboardLayoutMode.German)
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

    private void UpdateKeyVisualState(KeyboardKey key)
    {
        var isPlaying = !string.IsNullOrWhiteSpace(key.AssignedSoundId) && _audioPlayer.IsPlaying(key.AssignedSoundId);
        if (isPlaying)
        {
            key.State = KeyState.Playing;
            return;
        }

        key.State = key.HasAssignment ? KeyState.Assigned : KeyState.Empty;
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

    private void RunOnUiThread(Action action)
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

    private void RaiseCommandStateIfNeeded() => RaiseCommandState();
}