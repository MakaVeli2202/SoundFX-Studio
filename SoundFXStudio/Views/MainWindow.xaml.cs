using SoundFXStudio.Models;
using SoundFXStudio.Services;
using SoundFXStudio.ViewModels;
using SoundFXStudio.Views;
using SoundFXStudio.Views.Dialogs;
using System.Diagnostics;
using System.IO;
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
        VoiceChangerToggleBtn.Click += (_, _) =>
        {
            if (_voiceChanger is { IsRunning: true })
            {
                _voiceChanger.Stop();
                _voiceChanger.Dispose();
                _voiceChanger = null;
                VoiceChangerToggleBtn.Content = "Start Voice Changer";
                VoiceChangerStatus.Text = "Stopped";
                VoiceChangerStatus.Foreground = new SolidColorBrush(Color.FromRgb(0xE8, 0x55, 0x55));
                return;
            }

            var micIdx = ViewModel.GetVoiceChangerMicIndex();
            if (micIdx < 0)
            {
                ViewModel.StatusText = "No input device available for voice changer.";
                return;
            }

            var pitch = ViewModel.Settings.PitchShift;
            _voiceChanger = new VoiceChangerService();
            _voiceChanger.Start(micIdx, micIdx, pitch);
            VoiceChangerToggleBtn.Content = "Stop Voice Changer";
            VoiceChangerStatus.Text = "Running";
            VoiceChangerStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
            ViewModel.StatusText = "Voice changer started";
        };

        ActivationKeyBox.LostFocus += (_, _) => { ViewModel.Save(); };
        MuteAllKeyBox.LostFocus += (_, _) => { ViewModel.Save(); ViewModel.RegisterMuteHotkeys(); };
        MuteHearKeyBox.LostFocus += (_, _) => { ViewModel.Save(); ViewModel.RegisterMuteHotkeys(); };
        MuteTeamKeyBox.LostFocus += (_, _) => { ViewModel.Save(); ViewModel.RegisterMuteHotkeys(); };
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
        Hide();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Title = ViewModel.WindowTitle;
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
    }

    private void PopulateAudioCombos()
    {
        var outputs = ViewModel.OutputDevices.ToList();
        var inputs = ViewModel.InputDevices.ToList();
        var settings = ViewModel.Settings;

        HearCombo.ItemsSource = outputs;
        TalkCombo.ItemsSource = inputs;
        SpeakersComboBox.ItemsSource = outputs;

        HearCombo.SelectedItem = outputs.FirstOrDefault(d => d.Name == settings.HearDeviceName);
        TalkCombo.SelectedItem = inputs.FirstOrDefault(d => d.Name == settings.TalkDeviceName);
        SpeakersComboBox.SelectedItem = outputs.FirstOrDefault(d => d.Id == settings.OutputDeviceId);

        HearCombo.SelectedItem ??= outputs.FirstOrDefault();
        TalkCombo.SelectedItem ??= inputs.FirstOrDefault();
        SpeakersComboBox.SelectedItem ??= outputs.FirstOrDefault();

        ActivationModeCombo.SelectedIndex = settings.HotkeyHoldMode ? 0 : 1;
        ActivationModeCombo.SelectionChanged += (_, _) =>
        {
            settings.HotkeyHoldMode = ActivationModeCombo.SelectedIndex == 0;
            ViewModel.Save();
        };

        HearCombo.SelectionChanged += (_, _) =>
        {
            if (HearCombo.SelectedItem is AudioDeviceInfo d) { settings.HearDeviceName = d.Name; ViewModel.Save(); }
        };
        TalkCombo.SelectionChanged += (_, _) =>
        {
            if (TalkCombo.SelectedItem is AudioDeviceInfo d) { settings.TalkDeviceName = d.Name; ViewModel.Save(); }
        };
        SpeakersComboBox.SelectionChanged += (_, _) =>
        {
            if (SpeakersComboBox.SelectedItem is AudioDeviceInfo d) { settings.OutputDeviceId = d.Id; settings.PlaybackDeviceId = d.Id; ViewModel.Save(); }
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

        var dialog = new KeyCaptureDialog
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true) return;

        var key = ViewModel.TryResolveKeyboardKeyFromPhysicalKey(dialog.CapturedKey);
        if (key is null)
        {
            key = ViewModel.ResolveKeyFromName(dialog.CapturedKeyName);
        }
        if (key is null) return;

        ViewModel.AssignSoundToKeyFromUi(sound, key);
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

    private void MainWindow_OpenSoundSettings(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("control", "mmsys.cpl,,1") { UseShellExecute = true });
        }
        catch
        {
            ViewModel.StatusText = "Could not open Windows Sound settings.";
        }
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

    // ─── Settings page handlers (Voicemeeter section) ─────────────────────

    private async void VoicemeeterButton_Click(object sender, RoutedEventArgs e)
    {
        var hear = HearCombo.SelectedItem as AudioDeviceInfo;
        var talk = TalkCombo.SelectedItem as AudioDeviceInfo;
        if (hear is null || talk is null) { VoicemeeterStatus.Text = "Select both devices first."; return; }

        VoicemeeterButton.IsEnabled = false;
        VoicemeeterStatus.Text = "Setting up Voicemeeter…";
        VoicemeeterStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x98, 0xA0, 0xC0));
        Mouse.OverrideCursor = Cursors.Wait;

        string result = await ViewModel.AutoSetupVoicemeeterAsync(hear, talk);

        Mouse.OverrideCursor = null;
        VoicemeeterButton.IsEnabled = true;
        VoicemeeterStatus.Text = result;
        VoicemeeterStatus.Foreground = new SolidColorBrush(result.StartsWith("✓")
            ? Color.FromRgb(0x10, 0xB9, 0x81)
            : Color.FromRgb(0xE8, 0x55, 0x55));
    }

    private void MixerButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.OpenMixer(this);
    }

    private void RerunWizard_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.OpenSetupWizard(this);
    }

    private void OpenAppVolumeButton_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo("ms-settings:apps-volume") { UseShellExecute = true }); }
        catch { try { Process.Start(new ProcessStartInfo("sndvol.exe") { UseShellExecute = true }); } catch { } }
    }

    private void OpenSound_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo("control", "mmsys.cpl") { UseShellExecute = true }); }
        catch { }
    }

}