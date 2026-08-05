using SoundFXStudio.Models;
using SoundFXStudio.Services;
using SoundFXStudio.Services.DSP;
using SoundFXStudio.ViewModels;
using SoundFXStudio.Views;
using SoundFXStudio.Views.Dialogs;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using NAudio.Wave;

namespace SoundFXStudio;

public partial class MainWindow : Window
{
    private MainViewModel ViewModel => (MainViewModel)DataContext;
    private KeyboardCalibrationWindow? _keyboardCalibrationWindow;
    private KeyboardWindow? _keyboardWindow;
    private VoiceChangerService? _voiceChanger;
    private TeamMonitorService? _teamMonitor;
    private bool _loadingVoiceChangerUi;

    public MainWindow(ILogService? logService = null)
    {
        InitializeComponent();
        DataContext = new MainViewModel(logService);
        Loaded += MainWindow_Loaded;
        Closed += MainWindow_Closed;
        PreviewKeyDown += MainWindow_PreviewKeyDown;
        PreviewKeyUp += MainWindow_PreviewKeyUp;
        AllowDrop = true;
        Drop += MainWindow_Drop;
    }

    private void SetupVoiceChangerAndMuteHotkeys()
    {
        VoiceChangerToggleBtn.Click += (_, _) => ToggleVoiceChanger();

        ShowWizardOnStartupCheck.Checked += (_, _) => ViewModel.Save();
        ShowWizardOnStartupCheck.Unchecked += (_, _) => ViewModel.Save();
        StartMinimizedCheck.Checked += (_, _) => ViewModel.Save();
        StartMinimizedCheck.Unchecked += (_, _) => ViewModel.Save();

        SetupVoiceChangerPresets();
    }

    private void ToggleVoiceChanger()
    {
        if (_voiceChanger is { IsRunning: true })
        {
            _voiceChanger.Stop();
            _voiceChanger.Dispose();
            _voiceChanger = null;
        ViewModel.EndVoiceChangerDryMicMute();
        ViewModel.EndVoiceChangerMonitorMute();
            if (_teamMonitor is not { IsRunning: true })
            {
                ViewModel.EndVoiceChangerMonitorMute();
            }
            VoiceChangerToggleBtn.Content = "Start Voice Changer";
            VoiceChangerStatus.Text = "Stopped";
            VoiceChangerStatus.Foreground = new SolidColorBrush(Color.FromRgb(0xE8, 0x55, 0x55));
            VoiceChangerStatusDot.Background = new SolidColorBrush(Color.FromRgb(0xE8, 0x55, 0x55));
            return;
        }

        var micId = ViewModel.GetVoiceChangerMicId();
        if (string.IsNullOrWhiteSpace(micId))
        {
            ViewModel.StatusText = "No input device available for voice changer.";
            return;
        }

        if (!ViewModel.Settings.SetupCompleted)
        {
            ViewModel.StatusText = "Voice changer is not configured yet. Run the Audio Setup wizard first.";
            VoiceChangerStatus.Text = "Not configured";
            VoiceChangerStatus.Foreground = new SolidColorBrush(Color.FromRgb(0xF4, 0x3F, 0x5E));
            VoiceChangerStatusDot.Background = new SolidColorBrush(Color.FromRgb(0xF4, 0x3F, 0x5E));
            return;
        }

        var pitch = ViewModel.Settings.PitchShift;
        var outIdx = GetVoiceChangerOutputIndex();
        _voiceChanger = new VoiceChangerService();
        PresetManager.Apply(PresetManager.GetById(ViewModel.Settings.VoiceChangerPresetId), _voiceChanger);
        _voiceChanger.SetFormant(ViewModel.Settings.FormantShift);
        _voiceChanger.Start(micId, outIdx, pitch);
        ViewModel.BeginVoiceChangerDryMicMute();
        VoiceChangerToggleBtn.Content = "Stop Voice Changer";
        VoiceChangerStatus.Text = "Running";
        VoiceChangerStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
        VoiceChangerStatusDot.Background = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
        ViewModel.StatusText = outIdx > 0
            ? "Voice changer started -> VoiceMeeter Input"
            : "Voice changer started (default output)";
    }

    private static int GetVoiceChangerOutputIndex()
    {
        for (int i = 0; i < WaveOut.DeviceCount; i++)
        {
            var name = WaveOut.GetCapabilities(i).ProductName;
            if (name.Contains("Voicemeeter", StringComparison.OrdinalIgnoreCase)
                && name.Contains("Input", StringComparison.OrdinalIgnoreCase)
                && !name.Contains("Aux", StringComparison.OrdinalIgnoreCase)
                && !name.Contains("Virtual", StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }
        return 0;
    }

    private void SetupVoiceChangerPresets()
    {
        var settings = ViewModel.Settings;

        _loadingVoiceChangerUi = true;

        VoiceChangerPresetCombo.ItemsSource = PresetManager.Presets;
        VoiceChangerPresetCombo.DisplayMemberPath = nameof(VoiceChangerPreset.Name);
        VoiceChangerPresetCombo.SelectedItem = PresetManager.GetById(settings.VoiceChangerPresetId);
        PresetSummaryText.Text = BuildPresetSummary(PresetManager.GetById(settings.VoiceChangerPresetId));

        FormantSlider.Value = Math.Clamp(settings.FormantShift, 0.5, 2.0);
        FormantValueText.Text = $"{FormantSlider.Value:F2}x";

        _loadingVoiceChangerUi = false;

        VoiceChangerPresetCombo.SelectionChanged += (_, _) =>
        {
            if (_loadingVoiceChangerUi || VoiceChangerPresetCombo.SelectedItem is not VoiceChangerPreset preset)
            {
                return;
            }

            settings.VoiceChangerPresetId = preset.Id;
            PresetSummaryText.Text = BuildPresetSummary(preset);
            PitchSlider.Value = preset.PitchSemitones;
            FormantSlider.Value = preset.FormantShift;
            if (_voiceChanger is { IsRunning: true })
            {
                PresetManager.Apply(preset, _voiceChanger);
            }
            ViewModel.Save();
        };

        FormantSlider.ValueChanged += (_, _) =>
        {
            if (_loadingVoiceChangerUi)
            {
                return;
            }

            var val = Math.Round(FormantSlider.Value / 0.05) * 0.05;
            if (Math.Abs(FormantSlider.Value - val) > 0.001) FormantSlider.Value = val;
            FormantValueText.Text = $"{val:F2}x";
            settings.FormantShift = (float)val;
            _voiceChanger?.SetFormant((float)val);
            ViewModel.Save();
        };
    }

    private static string BuildPresetSummary(VoiceChangerPreset preset)
    {
        var parts = new List<string>();
        if (Math.Abs(preset.PitchSemitones) > 0.01f)
        {
            parts.Add($"Pitch {(preset.PitchSemitones > 0 ? "+" : "")}{preset.PitchSemitones} st");
        }
        if (Math.Abs(preset.FormantShift - 1f) > 0.01f)
        {
            parts.Add($"Formant {preset.FormantShift:F2}x");
        }
        if (preset.RobotEnabled) parts.Add("Robot");
        if (preset.DistortionEnabled) parts.Add("Distortion");
        if (preset.ReverbEnabled) parts.Add("Reverb");
        if (preset.ChorusEnabled) parts.Add("Chorus");
        if (preset.CompressorEnabled) parts.Add("Compressor");
        if (preset.LimiterEnabled) parts.Add("Limiter");
        if (preset.NoiseGateEnabled) parts.Add("Noise gate");
        return parts.Count > 0 ? string.Join(" \u00B7 ", parts) : "Clean voice";
    }

    private void SettingsSectionButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string section })
        {
            ViewModel.SettingsSection = section;
        }
    }

    private void AddBindingButton_Click(object sender, RoutedEventArgs e)
    {
        AddBindingPanel.Visibility = Visibility.Visible;
        if (ViewModel.AvailableKeybindActions.Count > 0)
        {
            AddBindingActionCombo.SelectedIndex = 0;
        }
        AddBindingActionCombo.Focus();
    }

    private void AddBindingCancelButton_Click(object sender, RoutedEventArgs e)
    {
        AddBindingPanel.Visibility = Visibility.Collapsed;
    }

    private void AddBindingRecordButton_Click(object sender, RoutedEventArgs e)
    {
        var actionName = AddBindingActionCombo.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(actionName))
        {
            ViewModel.StatusText = "Select an action first.";
            return;
        }

        var action = KeybindingItem.ParseAction(actionName);
        if (action is null)
        {
            return;
        }

        var dialog = new HotkeyCaptureDialog
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.CapturedHotkey))
        {
            return;
        }

        ViewModel.AddKeybinding(action.Value, dialog.CapturedHotkey);
        AddBindingPanel.Visibility = Visibility.Collapsed;
        ViewModel.StatusText = $"{actionName} bound to {dialog.CapturedHotkey}";
    }

    private void KeybindingChangeButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: KeybindingItem item })
        {
            var dialog = new HotkeyCaptureDialog(item.Key)
            {
                Owner = this
            };

            if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.CapturedHotkey))
            {
                return;
            }

            ViewModel.UpdateKeybindingKey(item, dialog.CapturedHotkey);
            ViewModel.StatusText = $"{item.ActionName} key changed to {dialog.CapturedHotkey}";
        }
    }

    private void KeybindingDeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: KeybindingItem item })
        {
            ViewModel.RemoveKeybinding(item);
            ViewModel.StatusText = $"{item.ActionName} keybinding removed.";
        }
    }

    private void UpdateNoKeybindingsVisibility()
    {
        if (NoKeybindingsText is not null)
        {
            NoKeybindingsText.Visibility = ViewModel.Keybindings.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        if (IsInteractiveElement(source))
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            WindowState = WindowState.Minimized;
            return;
        }

        DragMove();
    }

    private void WindowShell_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        if (IsInteractiveElement(source))
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            WindowState = WindowState.Minimized;
            return;
        }

        DragMove();
    }

    private static bool IsInteractiveElement(DependencyObject? current)
    {
        while (current is not null)
        {
            if (current is ButtonBase || current is TextBoxBase || current is ComboBox || current is ListBox || current is ListView || current is MenuItem || current is CheckBox || current is TabItem || current is Slider || current is PasswordBox || current is ScrollBar || current is Thumb)
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!App.IsShuttingDown)
        {
            e.Cancel = true;
            WindowState = WindowState.Minimized;
            return;
        }
        base.OnClosing(e);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Title = ViewModel.WindowTitle;

        var calibration = ViewModel.Settings.KeyboardCalibration;
        if (calibration is not null)
        {
            OpenKeyboardOverlayButton.Width = calibration.OpenKeyboardButtonWidth;
            OpenKeyboardOverlayButton.Height = calibration.OpenKeyboardButtonHeight;
            var heroHeight = HeroSectionBorder.ActualHeight > 0 ? HeroSectionBorder.ActualHeight : 520;
            var y = calibration.OpenKeyboardButtonY - (heroHeight - calibration.OpenKeyboardButtonHeight) / 2;
            OpenKeyboardOverlayButton.Margin = new Thickness(calibration.OpenKeyboardButtonX, y, 0, 0);
        }

        ViewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MainViewModel.WindowTitle))
            {
                Title = ViewModel.WindowTitle;
            }
        };

        ViewModel.AttachWindow(this);
        PopulateAudioCombos();
        SetupVoiceChangerAndMuteHotkeys();
        ViewModel.VoiceChangerToggleRequested += () => Dispatcher.Invoke(ToggleVoiceChanger);
        ViewModel.Keybindings.CollectionChanged += (_, _) => UpdateNoKeybindingsVisibility();
        UpdateNoKeybindingsVisibility();
    }

    private void PopulateAudioCombos()
    {
        var outputs = ViewModel.OutputDevices.ToList();
        var inputs = ViewModel.InputDevices.ToList();
        var settings = ViewModel.Settings;

        SpeakersComboBox.ItemsSource = outputs;
        MicrophoneComboBox.ItemsSource = inputs;

        SpeakersComboBox.SelectedItem = outputs.FirstOrDefault(d => d.Id == settings.OutputDeviceId);
        MicrophoneComboBox.SelectedItem = inputs.FirstOrDefault(d => d.Id == settings.InputDeviceId);

        SpeakersComboBox.SelectedItem ??= outputs.FirstOrDefault();
        MicrophoneComboBox.SelectedItem ??= inputs.FirstOrDefault();

        KeybindHoldModeCombo.SelectedIndex = settings.HotkeyHoldMode ? 0 : 1;
        KeybindHoldModeCombo.SelectionChanged += (_, _) =>
        {
            settings.HotkeyHoldMode = KeybindHoldModeCombo.SelectedIndex == 0;
            ViewModel.Save();
        };

        SpeakersComboBox.SelectionChanged += (_, _) =>
        {
            if (SpeakersComboBox.SelectedItem is AudioDeviceInfo d) { settings.OutputDeviceId = d.Id; settings.PlaybackDeviceId = d.Id; ViewModel.Save(); }
        };
        MicrophoneComboBox.SelectionChanged += (_, _) =>
        {
            if (MicrophoneComboBox.SelectedItem is AudioDeviceInfo d) { settings.InputDeviceId = d.Id; settings.MicrophoneDeviceId = d.Id; ViewModel.Save(); }
        };

        PitchSlider.Value = Math.Clamp(settings.PitchShift, -12, 12);
        PitchValueText.Text = $"{PitchSlider.Value:F1} st";
        PitchSlider.ValueChanged += (_, _) =>
        {
            double val = Math.Round(PitchSlider.Value / 0.5) * 0.5;
            if (Math.Abs(PitchSlider.Value - val) > 0.01) PitchSlider.Value = val;
            PitchValueText.Text = $"{val:F1} st";
            settings.PitchShift = (float)val;
            _voiceChanger?.SetPitch((float)val);
            ViewModel.Save();
        };
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        _voiceChanger?.Stop();
        _voiceChanger?.Dispose();
        _voiceChanger = null;
        _teamMonitor?.Dispose();
        _teamMonitor = null;
        ViewModel.EndVoiceChangerDryMicMute();

        if (_keyboardWindow is { IsLoaded: true })
        {
            _keyboardWindow.Close();
        }

        if (DataContext is IDisposable disposable)
        {
            disposable.Dispose();
        }

        DataContext = null;
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        ViewModel.HandlePreviewKeyDown(e);
    }

    private void MainWindow_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        ViewModel.HandlePreviewKeyUp(e);
    }

    private void MainWindow_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return;
        }

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files)
        {
            return;
        }

        ViewModel.HandleDropFiles(files);
    }

    private static SoundEntry? GetSoundFromMenu(object sender)
    {
        if (sender is MenuItem { Parent: ContextMenu { PlacementTarget: FrameworkElement target } } && target.DataContext is SoundEntry sound)
            return sound;
        return null;
    }

    private void SoundTileMenu_Play(object sender, RoutedEventArgs e)
    {
        var sound = GetSoundFromMenu(sender);
        if (sound is not null)
            ViewModel.PlaySoundCommand.Execute(sound);
    }

    private void SoundTileMenu_Edit(object sender, RoutedEventArgs e)
    {
        var sound = GetSoundFromMenu(sender);
        if (sound is not null)
            ViewModel.EditSoundCommand.Execute(sound);
    }

    private void SoundTileMenu_Assign(object sender, RoutedEventArgs e)
    {
        var sound = GetSoundFromMenu(sender);
        if (sound is null) return;

        var dialog = new KeyCaptureDialog(chordMode: true)
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true) return;

        var key = ViewModel.TryResolveKeyboardKeyFromPhysicalKey(dialog.CapturedKey);
        if (key is null)
        {
            key = ViewModel.ResolveKeyFromName(dialog.KeyNames.FirstOrDefault() ?? string.Empty);
        }
        if (key is null) return;

        ViewModel.AssignSoundToKeyFromUi(sound, key);
        ViewModel.AssignSoundChordFromUi(sound, dialog.KeyNames);
    }

    private void SoundTileMenu_Delete(object sender, RoutedEventArgs e)
    {
        var sound = GetSoundFromMenu(sender);
        if (sound is not null)
        {
            ViewModel.SelectedSound = sound;
            ViewModel.DeleteSoundCommand.Execute(null);
        }
    }

    private void SoundTileMenu_Duplicate(object sender, RoutedEventArgs e)
    {
        var sound = GetSoundFromMenu(sender);
        if (sound is not null)
        {
            ViewModel.SelectedSound = sound;
            ViewModel.DuplicateSoundCommand.Execute(null);
        }
    }

    private void SoundTileMenu_Favorite(object sender, RoutedEventArgs e)
    {
        var sound = GetSoundFromMenu(sender);
        if (sound is not null)
        {
            ViewModel.SelectedSound = sound;
            ViewModel.ToggleFavoriteCommand.Execute(null);
        }
    }

    private void SoundTileMenu_ChooseImage(object sender, RoutedEventArgs e)
    {
        var sound = GetSoundFromMenu(sender);
        if (sound is not null)
        {
            ViewModel.SelectedSound = sound;
            ViewModel.ChooseSoundImageCommand.Execute(sound);
        }
    }

    private void SoundTileMenu_RemoveImage(object sender, RoutedEventArgs e)
    {
        var sound = GetSoundFromMenu(sender);
        if (sound is not null)
        {
            ViewModel.SelectedSound = sound;
            ViewModel.RemoveSoundImageCommand.Execute(sound);
        }
    }

    private void SoundTileMenu_ClearBinding(object sender, RoutedEventArgs e)
    {
        var sound = GetSoundFromMenu(sender);
        if (sound is not null)
        {
            ViewModel.SelectedSound = sound;
            ViewModel.ClearSelectedSoundBindingCommand.Execute(null);
        }
    }

    private void LibraryMicButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new MicrophoneRecorderDialog { Owner = this };
        if (dialog.ShowDialog() != true) return;

        if (!File.Exists(dialog.RecordedFilePath)) return;

        var details = new SoundAssignmentViewModel
        {
            FilePath = dialog.RecordedFilePath,
            Name = dialog.SoundName
        };

        ViewModel.AddSoundFromDetails(details);
    }

    private void OpenCalibrationButton_Click(object sender, RoutedEventArgs e)
    {
        if (_keyboardCalibrationWindow is { IsLoaded: true })
        {
            if (_keyboardCalibrationWindow.WindowState == WindowState.Minimized)
            {
                _keyboardCalibrationWindow.WindowState = WindowState.Normal;
            }

            _keyboardCalibrationWindow.Activate();
            return;
        }

        _keyboardCalibrationWindow = new KeyboardCalibrationWindow
        {
            Owner = this
        };

        _keyboardCalibrationWindow.CalibrationSaved += (_, _) => ViewModel.RefreshCommand.Execute(null);
        _keyboardCalibrationWindow.Closed += (_, _) => _keyboardCalibrationWindow = null;
        _keyboardCalibrationWindow.Show();
    }

    private void OpenKeyboardWindowButton_Click(object sender, RoutedEventArgs e)
    {
        if (_keyboardWindow is { IsLoaded: true })
        {
            if (_keyboardWindow.WindowState == WindowState.Minimized)
            {
                _keyboardWindow.WindowState = WindowState.Normal;
            }

            _keyboardWindow.Activate();
            return;
        }

        _keyboardWindow = new KeyboardWindow
        {
            Owner = this
        };

        _keyboardWindow.Initialize(ViewModel);
        _keyboardWindow.Closed += (_, _) => _keyboardWindow = null;
        _keyboardWindow.Show();
        _keyboardWindow.Activate();
    }

    private void NavigateHome_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.CurrentPage = "Home";
    }

    private void NavigateSoundLibrary_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.CurrentPage = "SoundLibrary";
    }

    private void NavigateEffects_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.CurrentPage = "Effects";
    }

    private void NavigateSettings_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.CurrentPage = "Settings";
    }

    private void MixerButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.OpenMixer(this);
    }

    private void RerunWizard_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.OpenSetupWizard(this);
    }

    private void TeamMonitorButton_Click(object sender, RoutedEventArgs e)
    {
        var monitor = new Views.Dialogs.TeamMonitorWindow { Owner = this };
        monitor.Show();
    }

    private void TeamMonitorToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_teamMonitor is { IsRunning: true })
        {
            _teamMonitor.Stop();
            TeamMonitorToggleBtn.Content = "Hear What People Hear";
            SetInlineMonitorStatus("Off", "#98A0C0");
            ViewModel.EndVoiceChangerMonitorMute();
            return;
        }

        var devices = new AudioDeviceService();
        var captures = devices.GetAllInputDevices();
        var playbacks = devices.GetAllOutputDevices();

        var capture = captures.FirstOrDefault(d =>
            d.Name.Contains("Voicemeeter", StringComparison.OrdinalIgnoreCase)
            && d.Name.Contains("B1", StringComparison.OrdinalIgnoreCase)
            && !d.Name.Contains("Aux", StringComparison.OrdinalIgnoreCase)
            && !d.Name.Contains("Virtual", StringComparison.OrdinalIgnoreCase));

        var config = new ConfigService().Load();
        var saved = !string.IsNullOrWhiteSpace(config.Settings.HearDeviceName)
            ? config.Settings.HearDeviceName
            : config.Settings.SpeakersDeviceName;
        var playback = playbacks.FirstOrDefault(d => d.Name == saved)
            ?? playbacks.FirstOrDefault(d => d.IsDefaultCommunication)
            ?? playbacks.FirstOrDefault(d => d.IsDefault)
            ?? playbacks.FirstOrDefault();

        if (capture is null || playback is null)
        {
            SetInlineMonitorStatus("✗ VoiceMeeter B1 or playback device not found", "#F43F5E");
            return;
        }

        int capIdx = TeamMonitorService.ResolveWaveInIndex(capture.Name);
        int outIdx = TeamMonitorService.ResolveWaveOutIndex(playback.Name);
        if (capIdx < 0 || outIdx < 0)
        {
            SetInlineMonitorStatus("✗ Monitor source unavailable to the mixer", "#F43F5E");
            return;
        }

        _teamMonitor ??= new TeamMonitorService();
        if (!_teamMonitor.Start(capIdx, outIdx, TeamMonitorService.SimMode.None))
        {
            SetInlineMonitorStatus($"✗ {_teamMonitor.LastError}", "#F43F5E");
            return;
        }

        TeamMonitorToggleBtn.Content = "Stop Monitor";
        SetInlineMonitorStatus("Listening — this is what people hear", "#10B981");
        ViewModel.BeginVoiceChangerMonitorMute();
    }

    private void SetInlineMonitorStatus(string text, string color)
    {
        TeamMonitorStatus.Text = text;
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        TeamMonitorStatus.Foreground = brush;
        TeamMonitorStatusDot.Background = brush;
    }

}