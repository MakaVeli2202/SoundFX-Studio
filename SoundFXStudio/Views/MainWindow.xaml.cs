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
using System.Windows.Automation;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace SoundFXStudio;

public partial class MainWindow : Window
{
    private MainViewModel ViewModel => (MainViewModel)DataContext;
    private readonly ILogService? _logService;
    private KeyboardCalibrationWindow? _keyboardCalibrationWindow;
    private KeyboardWindow? _keyboardWindow;
    private VoiceChangerService? _voiceChanger;
    private TeamMonitorService? _teamMonitor;
    private bool _loadingVoiceChangerUi;
    private System.Windows.Threading.DispatcherTimer? _advancedVmTimer;
    private bool _suppressB1;
    private bool _suppressA1;
    private bool _suppressVirtualB1;
    private bool _exitFlowRunning;
    private bool _isClosing;

    public (double Width, double Height) HeroOverlaySize =>
        HeroImage.ActualWidth > 0 && HeroImage.ActualHeight > 0
            ? (HeroImage.ActualWidth, HeroImage.ActualHeight)
            : (998, 1024);

    public MainWindow(ILogService? logService = null)
    {
        _logService = logService;
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

        ShowWizardOnStartupCheck.Checked += (_, _) => { if (!_isClosing) ViewModel.Save(); };
        ShowWizardOnStartupCheck.Unchecked += (_, _) => { if (!_isClosing) ViewModel.Save(); };
        StartMinimizedCheck.Checked += (_, _) => { if (!_isClosing) ViewModel.Save(); };
        StartMinimizedCheck.Unchecked += (_, _) => { if (!_isClosing) ViewModel.Save(); };

        SetupVoiceChangerPresets();
    }

    private void ToggleVoiceChanger()
    {
        if (_isClosing)
        {
            return;
        }

        try
        {
            ToggleVoiceChangerCore();
        }
        catch (Exception ex)
        {
            Services.ActionLog.Instance.Error("VC", $"ToggleVoiceChanger crashed: {ex}");
            _logService?.Error("Voice changer toggle crashed.", ex);
            ViewModel.StatusText = "Voice changer encountered an error. Please try again.";
            VoiceChangerStatus.Text = "Error";
            VoiceChangerStatus.Foreground = new SolidColorBrush(Color.FromRgb(0xF4, 0x3F, 0x5E));
            VoiceChangerStatusDot.Background = new SolidColorBrush(Color.FromRgb(0xF4, 0x3F, 0x5E));
        }
    }

    private void ToggleVoiceChangerCore()
    {
        if (_voiceChanger is { IsRunning: true })
        {
            Services.ActionLog.Instance.Action("VC", "Stop Voice Changer");
            try { _voiceChanger.Stop(); } catch { }
            try { _voiceChanger.Dispose(); } catch { }
            _voiceChanger = null;
            App.IsVoiceChangerRunning = false;
            if (!ViewModel.TryRestoreVoiceChangerRouting(out var restoreError))
            {
                ViewModel.StatusText = restoreError;
                Services.ActionLog.Instance.Error("VC", $"Restore routing failed: {restoreError}");
            }
            ViewModel.EndVoiceChangerMonitorMute();
            if (_teamMonitor is not { IsRunning: true })
            {
                ViewModel.EndVoiceChangerMonitorMute();
            }
            VoiceChangerToggleBtn.Content = "Start Voice Changer";
            VoiceChangerStatus.Text = "Stopped";
            VoiceChangerStatus.Foreground = new SolidColorBrush(Color.FromRgb(0xE8, 0x55, 0x55));
            VoiceChangerStatusDot.Background = new SolidColorBrush(Color.FromRgb(0xE8, 0x55, 0x55));
            ToastWindow.Show("Voice Changer", "Stopped", false);
            return;
        }

        Services.ActionLog.Instance.Action("VC", "Start Voice Changer — resolving mic");
        var micId = ViewModel.GetVoiceChangerMicId();
        if (string.IsNullOrWhiteSpace(micId))
        {
            Services.ActionLog.Instance.Error("VC", $"No mic found. MicrophoneDeviceId='{ViewModel.Settings.MicrophoneDeviceId}', InputDevices={ViewModel.InputDevices.Count}");
            ViewModel.StatusText = "No microphone available.";
            return;
        }
        Services.ActionLog.Instance.Info("VC", $"Mic resolved: {micId}");

        if (string.IsNullOrWhiteSpace(ViewModel.Settings.HearDeviceName)
            && string.IsNullOrWhiteSpace(ViewModel.Settings.TalkDeviceName))
        {
            Services.ActionLog.Instance.Error("VC", "No devices configured");
            ViewModel.StatusText = "Run the Audio Setup wizard first.";
            VoiceChangerStatus.Text = "Not configured";
            VoiceChangerStatus.Foreground = new SolidColorBrush(Color.FromRgb(0xF4, 0x3F, 0x5E));
            VoiceChangerStatusDot.Background = new SolidColorBrush(Color.FromRgb(0xF4, 0x3F, 0x5E));
            return;
        }

        var pitch = ViewModel.Settings.PitchShift;
        Services.ActionLog.Instance.Info("VC", $"Preparing routing: mic={micId}, pitch={pitch}");
        if (!ViewModel.TryPrepareVoiceChangerRouting(micId, out var outIdx, out var outputDeviceId, out var outputDeviceName, out var routingError))
        {
            Services.ActionLog.Instance.Error("VC", $"Routing failed: {routingError} (outIdx={outIdx}, outDev='{outputDeviceName}')");
            ViewModel.StatusText = routingError;
            VoiceChangerStatus.Text = "Routing error";
            VoiceChangerStatus.Foreground = new SolidColorBrush(Color.FromRgb(0xF4, 0x3F, 0x5E));
            VoiceChangerStatusDot.Background = new SolidColorBrush(Color.FromRgb(0xF4, 0x3F, 0x5E));
            return;
        }
        Services.ActionLog.Instance.Info("VC", $"Routing OK: outIdx={outIdx}, outDev='{outputDeviceName}'");

        try
        {
            Services.ActionLog.Instance.Info("VC", $"Creating VoiceChangerService, preset={ViewModel.Settings.VoiceChangerPresetId}, formant={ViewModel.Settings.FormantShift}");
            _voiceChanger = new VoiceChangerService();
            PresetManager.Apply(PresetManager.GetById(ViewModel.Settings.VoiceChangerPresetId), _voiceChanger);
            _voiceChanger.SetFormant(ViewModel.Settings.FormantShift);
            _voiceChanger.Start(micId, outIdx, pitch);
            App.IsVoiceChangerRunning = true;
            Services.ActionLog.Instance.Action("VC", "Voice Changer started OK");
        }
        catch (Exception ex)
        {
            Services.ActionLog.Instance.Error("VC", $"VoiceChangerService.Start failed: {ex}");
            _voiceChanger?.Dispose();
            _voiceChanger = null;
            App.IsVoiceChangerRunning = false;
            _logService?.Error("Voice changer failed to start.", ex);
            ViewModel.TryRestoreVoiceChangerRouting(out var restoreError);
            ViewModel.StatusText = string.IsNullOrWhiteSpace(restoreError)
                ? "Voice changer failed to start."
                : "Voice changer failed to start. Audio routing couldn't be restored.";
            VoiceChangerStatus.Text = "Start failed";
            VoiceChangerStatus.Foreground = new SolidColorBrush(Color.FromRgb(0xF4, 0x3F, 0x5E));
            VoiceChangerStatusDot.Background = new SolidColorBrush(Color.FromRgb(0xF4, 0x3F, 0x5E));
            return;
        }

        VoiceChangerToggleBtn.Content = "Stop Voice Changer";
        VoiceChangerStatus.Text = "Running";
        VoiceChangerStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
        VoiceChangerStatusDot.Background = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
        ViewModel.StatusText = "Voice changer started.";
        ToastWindow.Show("Voice Changer", "Running", true);
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

        if (ViewModel.SettingsSection == "Advanced")
        {
            EnsureAdvancedVmTimer();
        }
        else
        {
            _advancedVmTimer?.Stop();
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
        if (App.IsShuttingDown)
        {
            base.OnClosing(e);
            return;
        }

        Services.ActionLog.Instance.Action("App", "Window closing");
        Services.ActionLog.Instance.Flush();
        e.Cancel = true;
        _ = RunExitResetAsync(resetAudio: App.IsSessionEnding || AskResetOnClose());
    }

    private bool AskResetOnClose()
    {
        var dialog = new CloseOptionsDialog { Owner = this };
        dialog.ShowDialog();
        return dialog.ResetAudioOnClose;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private async Task RunExitResetAsync(bool resetAudio)
    {
        if (_exitFlowRunning)
        {
            return;
        }

        _exitFlowRunning = true;

        ProgressOverlayWindow? overlay = null;
        try
        {
            _voiceChanger?.Stop();
            _voiceChanger?.Dispose();
            _voiceChanger = null;
            App.IsVoiceChangerRunning = false;
            ViewModel.Gaming.StopCapture();
            ViewModel.Gaming.Dispose();
            _teamMonitor?.Dispose();
            _teamMonitor = null;

            if (resetAudio)
            {
                if (App.IsSessionEnding)
                {
                    await ViewModel.ResetVoicemeeterAsync();
                }
                else
                {
                    Hide();
                    overlay = new ProgressOverlayWindow("Resetting Audio")
                    {
                        Owner = this,
                        WindowStartupLocation = WindowStartupLocation.CenterScreen
                    };
                    overlay.Show();
                    await ViewModel.ResetVoicemeeterAsync(step => overlay.UpdateStep(step));
                    overlay.Complete("Devices restored to defaults.");
                    await Task.Delay(900);
                    overlay.Close();
                    overlay = null;
                    ToastWindow.Show("Devices reset", "Input/output devices restored to default.", "DONE", Color.FromRgb(0x10, 0xB9, 0x81));
                    await Task.Delay(1800);
                }
            }
        }
        catch
        {
            // reset failed — still shut down cleanly
        }
        finally
        {
            overlay?.Close();
            App.RequestShutdown();
            Application.Current.Shutdown();
        }
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Title = "SoundFX Studio";
        AutomationProperties.SetAutomationId(this, "MainWindow");
        AutomationProperties.SetName(this, "SoundFX Studio");
        UpdateCurrentPageDisplay();

        ApplyOpenKeyboardButtonCalibration();
        HeroSectionBorder.SizeChanged += (_, _) => ApplyOpenKeyboardButtonCalibration();

        ViewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MainViewModel.WindowTitle))
            {
                Title = ViewModel.WindowTitle;
            }
            else if (args.PropertyName == nameof(MainViewModel.CurrentPage))
            {
                UpdateCurrentPageDisplay();
            }
        };

        ViewModel.AttachWindow(this);
        PopulateAudioCombos();
        SetupVoiceChangerAndMuteHotkeys();
        ViewModel.VoiceChangerToggleRequested += () => Dispatcher.Invoke(ToggleVoiceChanger);
        ViewModel.Keybindings.CollectionChanged += (_, _) => UpdateNoKeybindingsVisibility();
        UpdateNoKeybindingsVisibility();
    }

    private void UpdateCurrentPageDisplay()
    {
    }

    private void PopulateAudioCombos()
    {
        var outputs = ViewModel.OutputDevices.ToList();
        var inputs = ViewModel.InputDevices.ToList();
        var settings = ViewModel.Settings;

        SpeakersComboBox.ItemsSource = outputs;
        MicrophoneComboBox.ItemsSource = inputs;

        var ads = new AudioDeviceService();
        SpeakersComboBox.SelectedItem = outputs.FirstOrDefault(d => d.Id == settings.OutputDeviceId)
            ?? outputs.FirstOrDefault(d => d.Id == ads.GetDefaultDeviceId(DataFlow.Render))
            ?? outputs.FirstOrDefault(d => d.Id == ads.GetDefaultCommunicationDeviceId(DataFlow.Render))
            ?? outputs.FirstOrDefault(d => d.IsDefaultCommunication)
            ?? outputs.FirstOrDefault(d => d.IsDefault)
            ?? outputs.FirstOrDefault();
        MicrophoneComboBox.SelectedItem = inputs.FirstOrDefault(d => d.Id == settings.InputDeviceId)
            ?? inputs.FirstOrDefault(d => d.Id == ads.GetDefaultDeviceId(DataFlow.Capture))
            ?? inputs.FirstOrDefault(d => d.Id == ads.GetDefaultCommunicationDeviceId(DataFlow.Capture))
            ?? inputs.FirstOrDefault(d => d.IsDefaultCommunication)
            ?? inputs.FirstOrDefault(d => d.IsDefault)
            ?? inputs.FirstOrDefault();

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
        _isClosing = true;
        _advancedVmTimer?.Stop();
        _voiceChanger?.Stop();
        _voiceChanger?.Dispose();
        _voiceChanger = null;
        App.IsVoiceChangerRunning = false;
        _teamMonitor?.Dispose();
        _teamMonitor = null;
        ViewModel.TryRestoreVoiceChangerRouting(out _);

        if (_keyboardWindow is { IsLoaded: true })
        {
            _keyboardWindow.Close();
        }

        if (DataContext is IDisposable disposable)
        {
            try
            {
                disposable.Dispose();
            }
            catch (Exception ex)
            {
                _logService?.Error("MainViewModel dispose failed during close", ex);
            }
        }
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

        var dialog = new KeyCaptureDialog(chordMode: true, existingKeys: ViewModel.GetChordKeysForSound(sound))
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
            Owner = this,
            HeroOverlayWidth = HeroOverlaySize.Width,
            HeroOverlayHeight = HeroOverlaySize.Height
        };

        _keyboardCalibrationWindow.OpenKeyboardButtonChanged += (_, _) =>
        {
            if (_keyboardCalibrationWindow is null)
            {
                return;
            }

            ApplyOpenKeyboardButton(
                _keyboardCalibrationWindow.OpenKeyboardButtonX,
                _keyboardCalibrationWindow.OpenKeyboardButtonY,
                _keyboardCalibrationWindow.OpenKeyboardButtonWidth,
                _keyboardCalibrationWindow.OpenKeyboardButtonHeight);
        };
        _keyboardCalibrationWindow.LivePreviewChanged += (_, _) =>
        {
            if (_keyboardCalibrationWindow is null)
            {
                return;
            }

            var live = _keyboardCalibrationWindow.BuildCalibration();
            ViewModel.PushLiveKeyboardCalibration(live);
            _keyboardWindow?.RefreshPanels(live);
        };
        _keyboardCalibrationWindow.CalibrationSaved += (_, _) =>
        {
            ViewModel.RefreshCommand.Execute(null);
            ViewModel.ClearLiveKeyboardCalibration();
            ApplyOpenKeyboardButtonCalibration();
            _keyboardWindow?.RefreshPanels();
        };
        _keyboardCalibrationWindow.Closed += (_, _) =>
        {
            _keyboardCalibrationWindow = null;
            ViewModel.ClearLiveKeyboardCalibration();
            ViewModel.ApplyKeyboardCalibrationFromSettings();
            ApplyOpenKeyboardButtonCalibration();
            _keyboardWindow?.RefreshPanels();
        };
        _keyboardCalibrationWindow.Show();
    }

    private void ApplyOpenKeyboardButtonCalibration()
    {
        var calibration = ViewModel.Settings.KeyboardCalibration;
        if (calibration is null)
        {
            return;
        }

        var (heroWidth, heroHeight) = HeroOverlaySize;
        if (heroWidth > 0 && heroHeight > 0)
        {
            calibration.RescaleForHeroSize(heroWidth, heroHeight);
        }

        ApplyOpenKeyboardButton(
            calibration.OpenKeyboardButtonX,
            calibration.OpenKeyboardButtonY,
            calibration.OpenKeyboardButtonWidth,
            calibration.OpenKeyboardButtonHeight);
    }

    private void ApplyOpenKeyboardButton(double x, double y, double width, double height)
    {
        var (heroWidth, heroHeight) = HeroOverlaySize;
        if (heroWidth <= 0 || heroHeight <= 0)
        {
            return;
        }

        var buttonWidth = Math.Clamp(width, 20, Math.Max(20, heroWidth - 4));
        var buttonHeight = Math.Clamp(height, 10, Math.Max(10, heroHeight - 4));

        OpenKeyboardOverlayButton.Width = buttonWidth;
        OpenKeyboardOverlayButton.Height = buttonHeight;
        OpenKeyboardOverlayButton.HorizontalAlignment = HorizontalAlignment.Left;
        OpenKeyboardOverlayButton.VerticalAlignment = VerticalAlignment.Top;
        OpenKeyboardOverlayButton.Margin = new Thickness(
            Math.Clamp(x, 0, Math.Max(0, heroWidth - buttonWidth)),
            Math.Clamp(y, 0, Math.Max(0, heroHeight - buttonHeight)),
            0,
            0);

        if (OpenKeyboardLedRing is not null)
        {
            OpenKeyboardLedRing.Width = buttonWidth;
            OpenKeyboardLedRing.Height = buttonHeight;
            OpenKeyboardLedRing.HorizontalAlignment = HorizontalAlignment.Left;
            OpenKeyboardLedRing.VerticalAlignment = VerticalAlignment.Top;
            OpenKeyboardLedRing.Margin = OpenKeyboardOverlayButton.Margin;
        }
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
        UpdateCurrentPageDisplay();
    }

    private void NavigateSoundLibrary_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.CurrentPage = "SoundLibrary";
        UpdateCurrentPageDisplay();
    }

    private void NavigateEffects_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.CurrentPage = "Effects";
        UpdateCurrentPageDisplay();
    }

    private void NavigateSettings_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.CurrentPage = "Settings";
        UpdateCurrentPageDisplay();
    }

    private void NavigateGaming_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.CurrentPage = "Gaming";
        UpdateCurrentPageDisplay();
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

    private void EnsureAdvancedVmTimer()
    {
        if (_advancedVmTimer is not null)
        {
            _advancedVmTimer.Start();
            RefreshAdvancedVmState();
            return;
        }

        _advancedVmTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _advancedVmTimer.Tick += (_, _) => RefreshAdvancedVmState();
        _advancedVmTimer.Start();
        RefreshAdvancedVmState();
    }

    private void RefreshAdvancedVmState()
    {
        using var vm = App.Vm();
        if (vm is null)
        {
            AdvancedB1ToggleBtn.IsEnabled = false;
            AdvancedA1ToggleBtn.IsEnabled = false;
            AdvancedVirtualB1ToggleBtn.IsEnabled = false;
            if (!AdvancedVmStatus.Text.StartsWith("✗"))
                SetAdvancedVmStatus("✗ Audio engine not running or not installed", Color.FromRgb(0xE8, 0x55, 0x55));
            return;
        }

        int stripCount = vm.StripCount();
        int firstVirtual = vm.FirstVirtualStrip(stripCount);

        AdvancedB1ToggleBtn.IsEnabled = true;
        bool b1 = vm.GetFloat("Strip[0].B1") >= 0.5f;
        _suppressB1 = true;
        AdvancedB1ToggleBtn.IsChecked = b1;
        _suppressB1 = false;
        SetAdvancedB1Status(b1
            ? "B1 ON — mic goes to Discord/teams"
            : "B1 OFF — mic blocked from Discord/teams", b1
            ? Color.FromRgb(0x10, 0xB9, 0x81)
            : Color.FromRgb(0xE8, 0x55, 0x55));

        bool vcRunning = App.IsVoiceChangerRunning;
        AdvancedA1ToggleBtn.IsEnabled = !vcRunning;
        if (vcRunning)
        {
            _suppressA1 = true;
            AdvancedA1ToggleBtn.IsChecked = false;
            _suppressA1 = false;
            SetAdvancedA1Status("A1 OFF — locked while voice changer is running", Color.FromRgb(0xE8, 0x55, 0x55));
        }
        else
        {
            bool a1 = firstVirtual >= 0 && vm.GetFloat($"Strip[{firstVirtual}].A1") >= 0.5f;
            _suppressA1 = true;
            AdvancedA1ToggleBtn.IsChecked = a1;
            _suppressA1 = false;
            SetAdvancedA1Status(a1
                ? "A1 ON — processed voice goes to speakers"
                : "A1 OFF — processed voice muted from speakers", a1
                ? Color.FromRgb(0x10, 0xB9, 0x81)
                : Color.FromRgb(0xE8, 0x55, 0x55));
        }

        AdvancedVirtualB1ToggleBtn.IsEnabled = true;
        bool vb1 = firstVirtual >= 0 && vm.GetFloat($"Strip[{firstVirtual}].B1") >= 0.5f;
        _suppressVirtualB1 = true;
        AdvancedVirtualB1ToggleBtn.IsChecked = vb1;
        _suppressVirtualB1 = false;
        SetAdvancedVirtualB1Status(vb1
            ? "Virtual B1 ON — processed voice goes to Discord/teams"
            : "Virtual B1 OFF — processed voice blocked from Discord/teams", vb1
            ? Color.FromRgb(0x10, 0xB9, 0x81)
            : Color.FromRgb(0xE8, 0x55, 0x55));

        if (AdvancedVmStatus.Text.StartsWith("✗"))
            SetAdvancedVmStatus("Audio engine connected", Color.FromRgb(0x10, 0xB9, 0x81));
    }

    private void AdvancedB1Toggle_Checked(object sender, RoutedEventArgs e)
    {
        if (_suppressB1) return;
        using var vm = App.Vm();
        if (vm is null)
        {
            _suppressB1 = true;
            AdvancedB1ToggleBtn.IsChecked = false;
            _suppressB1 = false;
            SetAdvancedB1Status("✗ Audio engine not running", Color.FromRgb(0xE8, 0x55, 0x55));
            return;
        }
        vm.SetFloat("Strip[0].B1", 1);
        SetAdvancedB1Status("B1 ON — mic goes to Discord/teams", Color.FromRgb(0x10, 0xB9, 0x81));
    }

    private void AdvancedB1Toggle_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_suppressB1) return;
        using var vm = App.Vm();
        if (vm is null)
        {
            _suppressB1 = true;
            AdvancedB1ToggleBtn.IsChecked = true;
            _suppressB1 = false;
            SetAdvancedB1Status("✗ Audio engine not running", Color.FromRgb(0xE8, 0x55, 0x55));
            return;
        }
        vm.SetFloat("Strip[0].B1", 0);
        SetAdvancedB1Status("B1 OFF — mic blocked from Discord/teams", Color.FromRgb(0xE8, 0x55, 0x55));
    }

    private void AdvancedA1Toggle_Checked(object sender, RoutedEventArgs e)
    {
        if (_suppressA1) return;
        if (App.IsVoiceChangerRunning)
        {
            _suppressA1 = true;
            AdvancedA1ToggleBtn.IsChecked = false;
            _suppressA1 = false;
            SetAdvancedA1Status("A1 OFF — locked while voice changer is running", Color.FromRgb(0xE8, 0x55, 0x55));
            return;
        }
        using var vm = App.Vm();
        if (vm is null)
        {
            _suppressA1 = true;
            AdvancedA1ToggleBtn.IsChecked = false;
            _suppressA1 = false;
            SetAdvancedA1Status("✗ Audio engine not running", Color.FromRgb(0xE8, 0x55, 0x55));
            return;
        }
        int firstVirtual = vm.FirstVirtualStrip(vm.StripCount());
        if (firstVirtual >= 0)
        {
            vm.SetFloat($"Strip[{firstVirtual}].A1", 1);
            SetAdvancedA1Status("A1 ON — processed voice goes to speakers", Color.FromRgb(0x10, 0xB9, 0x81));
        }
    }

    private void AdvancedA1Toggle_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_suppressA1) return;
        using var vm = App.Vm();
        if (vm is null)
        {
            _suppressA1 = true;
            AdvancedA1ToggleBtn.IsChecked = true;
            _suppressA1 = false;
            SetAdvancedA1Status("✗ Audio engine not running", Color.FromRgb(0xE8, 0x55, 0x55));
            return;
        }
        int firstVirtual = vm.FirstVirtualStrip(vm.StripCount());
        if (firstVirtual >= 0)
        {
            vm.SetFloat($"Strip[{firstVirtual}].A1", 0);
            SetAdvancedA1Status("A1 OFF — processed voice muted from speakers", Color.FromRgb(0xE8, 0x55, 0x55));
        }
    }

    private void AdvancedVirtualB1Toggle_Checked(object sender, RoutedEventArgs e)
    {
        if (_suppressVirtualB1) return;
        using var vm = App.Vm();
        if (vm is null)
        {
            _suppressVirtualB1 = true;
            AdvancedVirtualB1ToggleBtn.IsChecked = false;
            _suppressVirtualB1 = false;
            SetAdvancedVirtualB1Status("✗ Audio engine not running", Color.FromRgb(0xE8, 0x55, 0x55));
            return;
        }
        int firstVirtual = vm.FirstVirtualStrip(vm.StripCount());
        if (firstVirtual >= 0)
        {
            vm.SetFloat($"Strip[{firstVirtual}].B1", 1);
            SetAdvancedVirtualB1Status("Virtual B1 ON — processed voice goes to Discord/teams", Color.FromRgb(0x10, 0xB9, 0x81));
        }
    }

    private void AdvancedVirtualB1Toggle_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_suppressVirtualB1) return;
        using var vm = App.Vm();
        if (vm is null)
        {
            _suppressVirtualB1 = true;
            AdvancedVirtualB1ToggleBtn.IsChecked = true;
            _suppressVirtualB1 = false;
            SetAdvancedVirtualB1Status("✗ Audio engine not running", Color.FromRgb(0xE8, 0x55, 0x55));
            return;
        }
        int firstVirtual = vm.FirstVirtualStrip(vm.StripCount());
        if (firstVirtual >= 0)
        {
            vm.SetFloat($"Strip[{firstVirtual}].B1", 0);
            SetAdvancedVirtualB1Status("Virtual B1 OFF — processed voice blocked from Discord/teams", Color.FromRgb(0xE8, 0x55, 0x55));
        }
    }

    private async void AdvancedConfigureVmOnly_Click(object sender, RoutedEventArgs e)
    {
        Services.ActionLog.Instance.Action("Advanced", "Configure VM Only clicked");
        if (!VoicemeeterRemote.IsInstalled())
        {
            SetAdvancedVmStatus("✗ Audio engine not installed.", Color.FromRgb(0xE8, 0x55, 0x55));
            return;
        }

        var configService = new ConfigService();
        var config = configService.Load();
        var hear = config.Settings.HearDeviceName;
        var talk = config.Settings.TalkDeviceName;
        if (string.IsNullOrWhiteSpace(hear) || string.IsNullOrWhiteSpace(talk))
        {
            SetAdvancedVmStatus("✗ No Hear/Talk devices saved — run the setup wizard first.", Color.FromRgb(0xE8, 0x55, 0x55));
            return;
        }

        AdvancedConfigureVmOnlyBtn.IsEnabled = false;
        AdvancedRouteInputOnlyBtn.IsEnabled = false;
        SetAdvancedVmStatus("Configuring audio…", Color.FromRgb(0x98, 0xA0, 0xC0));
        Mouse.OverrideCursor = Cursors.Wait;

        string result;
        try
        {
            var (applied, diagnostics) = await Task.Run(() =>
            {
                using var vm = new VoicemeeterRemote();
                if (!vm.Login()) return (false, "Login failed.");
                bool ok = vm.ApplyRouting(hear, talk, step => Dispatcher.BeginInvoke(() => SetAdvancedVmStatus(step, Color.FromRgb(0x98, 0xA0, 0xC0))));
                string diag = vm.LastDiagnostics;
                vm.Dispose();
                return (ok, diag);
            });

            if (applied)
            {
                config.Settings.HearDeviceName = hear;
                config.Settings.TalkDeviceName = talk;
                config.Settings.SpeakersDeviceName = hear;
                config.Settings.VoicemeeterDetected = true;
                configService.Save(config);

                result = $"✓ Audio configured:\n   Hear: {hear}\n   Talk: {talk}\n   Windows defaults unchanged.";
            }
            else
            {
                result = "✗ Setup failed: could not configure audio engine.\n   Check Hear/Talk device names and retry.";
                if (!string.IsNullOrWhiteSpace(diagnostics))
                    result += $"\n\n{diagnostics}";
            }
        }
        catch (Exception ex)
        {
            result = $"✗ Setup failed: {ex.Message}";
        }

        Mouse.OverrideCursor = null;
        AdvancedConfigureVmOnlyBtn.IsEnabled = true;
        AdvancedRouteInputOnlyBtn.IsEnabled = true;
        ViewModel.Refresh();
        SetAdvancedVmStatus(result, result.StartsWith("✓")
            ? Color.FromRgb(0x10, 0xB9, 0x81)
            : Color.FromRgb(0xE8, 0x55, 0x55));
    }

    private void AdvancedRouteInputOnly_Click(object sender, RoutedEventArgs e)
    {
        if (!VoicemeeterRemote.IsInstalled())
        {
            SetAdvancedVmStatus("✗ Audio engine not installed.", Color.FromRgb(0xE8, 0x55, 0x55));
            return;
        }

        var configService = new ConfigService();
        var config = configService.Load();
        var audioDeviceService = new AudioDeviceService();
        var routing = new WindowsAudioRoutingService();

        var vmOutputId = audioDeviceService.GetVoicemeeterOutputId();
        if (string.IsNullOrWhiteSpace(vmOutputId))
        {
            SetAdvancedVmStatus("✗ Virtual Audio Output (B1) not found.", Color.FromRgb(0xE8, 0x55, 0x55));
            return;
        }

        var currentCapture = audioDeviceService.GetDefaultDeviceId(DataFlow.Capture);
        if (!string.IsNullOrWhiteSpace(currentCapture)
            && !string.Equals(currentCapture, vmOutputId, StringComparison.OrdinalIgnoreCase))
        {
            config.Settings.SavedDefaultCaptureId = currentCapture;
        }

        var inputApplied = routing.TrySetDefaultInput(vmOutputId);
        var reboundInput = audioDeviceService.GetDefaultDeviceId(DataFlow.Capture);
        var verified = inputApplied && string.Equals(reboundInput, vmOutputId, StringComparison.OrdinalIgnoreCase);

        config.Settings.VoicemeeterDetected = true;
        config.Settings.InputDeviceId = vmOutputId;
        config.Settings.MicrophoneDeviceId = vmOutputId;
        configService.Save(config);

        ViewModel.Refresh();

        var virtualB1Activated = false;
        using (var vm = App.Vm())
        {
            if (vm is not null)
            {
                int firstVirtual = vm.FirstVirtualStrip(vm.StripCount());
                if (firstVirtual >= 0 && vm.GetFloat($"Strip[{firstVirtual}].B1") < 0.5f)
                {
                    vm.SetFloat($"Strip[{firstVirtual}].B1", 1);
                    vm.IsDirty();
                    virtualB1Activated = true;
                }
            }
        }

        SetAdvancedVmStatus(verified
            ? virtualB1Activated
                ? "✓ Windows input → Virtual Audio Output (B1). Virtual Input B1 also enabled."
                : "✓ Windows input → Virtual Audio Output (B1). Output device left unchanged."
            : "⚠ Windows input → Virtual Audio Output (B1) not confirmed. Output device left unchanged.",
            verified
                ? Color.FromRgb(0x10, 0xB9, 0x81)
                : Color.FromRgb(0xF5, 0x9E, 0x0B));
    }

    private void AdvancedTestHear_Click(object sender, RoutedEventArgs e)
    {
        var monitor = new Views.Dialogs.TeamMonitorWindow { Owner = this };
        monitor.Show();
    }

    private async void AdvancedResetWindows_Click(object sender, RoutedEventArgs e)
    {
        var configService = new ConfigService();
        var config = configService.Load();

        string windowsResult = "Windows input device unchanged.";
        if (!string.IsNullOrWhiteSpace(config.Settings.SavedDefaultCaptureId))
        {
            var routing = new WindowsAudioRoutingService();
            bool restored = routing.TrySetDefaultInput(config.Settings.SavedDefaultCaptureId);
            windowsResult = restored
                ? "✓ Windows input restored to previous device"
                : "⚠ Could not restore Windows input device — kept for retry";
            if (restored)
            {
                config.Settings.SavedDefaultCaptureId = string.Empty;
            }
            try { configService.Save(config); } catch { }
        }

        AdvancedResetWindowsBtn.IsEnabled = false;
        SetAdvancedResetStatus("Resetting audio routing…", Color.FromRgb(0x98, 0xA0, 0xC0));

        var audioDeviceService = new AudioDeviceService();
        string vmResult;
        try
        {
            vmResult = await Task.Run(() =>
            {
                using var vm = new VoicemeeterRemote();
                var inputs = audioDeviceService.GetAllInputDevices().Select(d => d.Name).ToList();
                var outputs = audioDeviceService.GetAllOutputDevices().Select(d => d.Name).ToList();
                return vm.ResetRouting(inputs, outputs);
            });
        }
        catch (Exception ex)
        {
            vmResult = $"✗ Audio reset failed: {ex.Message}";
        }

        AdvancedResetWindowsBtn.IsEnabled = true;
        SetAdvancedResetStatus($"{windowsResult}\n{vmResult}",
            vmResult.StartsWith("✓")
                ? Color.FromRgb(0x10, 0xB9, 0x81)
                : Color.FromRgb(0xE8, 0x55, 0x55));
    }

    private void SetAdvancedB1Status(string text, Color color)
    {
        AdvancedB1Status.Text = text;
        AdvancedB1Status.Foreground = new SolidColorBrush(color);
    }

    private void SetAdvancedA1Status(string text, Color color)
    {
        AdvancedA1Status.Text = text;
        AdvancedA1Status.Foreground = new SolidColorBrush(color);
    }

    private void SetAdvancedVirtualB1Status(string text, Color color)
    {
        AdvancedVirtualB1Status.Text = text;
        AdvancedVirtualB1Status.Foreground = new SolidColorBrush(color);
    }

    private void SetAdvancedVmStatus(string text, Color color)
    {
        AdvancedVmStatus.Text = text;
        AdvancedVmStatus.Foreground = new SolidColorBrush(color);
    }

    private void SetAdvancedResetStatus(string text, Color color)
    {
        AdvancedResetStatus.Text = text;
        AdvancedResetStatus.Foreground = new SolidColorBrush(color);
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
            SetInlineMonitorStatus("✗ B1 or playback device not found", "#F43F5E");
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