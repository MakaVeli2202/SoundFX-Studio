using SoundFXStudio.Models;
using SoundFXStudio.Services;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using NAudio.CoreAudioApi;

namespace SoundFXStudio.Views.Dialogs;

public partial class SetupWizardWindow : Window
{
    private readonly ConfigService _configService = new();
    private readonly AudioDeviceService _audioDeviceService = new();
    private readonly WindowsAudioRoutingService _windowsAudioRoutingService = new();
    private AppConfig _config;

    public SetupWizardWindow()
    {
        InitializeComponent();
        _config = _configService.Load();
        Loaded += SetupWizardWindow_Loaded;
        Closed += (_, _) => { _stateTimer?.Stop(); _vm.Dispose(); };
        PreviewMouseLeftButtonDown += TryDragWindow;
    }

    private void TryDragWindow(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            return;
        }

        var source = e.OriginalSource as DependencyObject;
        while (source is not null)
        {
            if (source is System.Windows.Controls.Primitives.ButtonBase
                or System.Windows.Controls.TextBox
                or System.Windows.Controls.ComboBox
                or System.Windows.Controls.Slider
                or System.Windows.Controls.CheckBox
                or System.Windows.Controls.ListBox
                or System.Windows.Controls.Primitives.ScrollBar)
            {
                return;
            }

            source = System.Windows.Media.VisualTreeHelper.GetParent(source);
        }

        DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void SetupWizardWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var outputs = _audioDeviceService.GetOutputDevices().ToList();
        var inputs = _audioDeviceService.GetInputDevices().ToList();

        WizardHearCombo.ItemsSource = outputs;
        WizardTalkCombo.ItemsSource = inputs;

        WizardHearCombo.SelectedItem = outputs.FirstOrDefault(d => d.Name == _config.Settings.HearDeviceName) ?? outputs.FirstOrDefault();
        WizardTalkCombo.SelectedItem = inputs.FirstOrDefault(d => d.Name == _config.Settings.TalkDeviceName) ?? inputs.FirstOrDefault();

        CheckVoicemeeter();
    }

    private void CheckVoicemeeter()
    {
        if (VoicemeeterService.IsVoicemeeterInstalled())
        {
            VmDetectText.Text = "✓ Voicemeeter detected";
            VmDetectText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x10, 0xB9, 0x81));
            WizardAutoSetupBtn.IsEnabled = true;
            WizardConfigureVmOnlyBtn.IsEnabled = true;
            WizardRouteInputOnlyBtn.IsEnabled = true;
        }
        else
        {
            VmDetectText.Text = "✗ Voicemeeter not installed — install it for best routing";
            VmDetectText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF4, 0x3F, 0x5E));
            WizardAutoSetupBtn.IsEnabled = false;
            WizardConfigureVmOnlyBtn.IsEnabled = false;
            WizardRouteInputOnlyBtn.IsEnabled = false;
        }
    }

    private void SetB1Status(string text, System.Windows.Media.Color color)
    {
        WizardB1Status.Text = text;
        WizardB1Status.Foreground = new System.Windows.Media.SolidColorBrush(color);
    }

    private void SetA1Status(string text, System.Windows.Media.Color color)
    {
        WizardA1Status.Text = text;
        WizardA1Status.Foreground = new System.Windows.Media.SolidColorBrush(color);
    }

    private void SetVirtualB1Status(string text, System.Windows.Media.Color color)
    {
        WizardVirtualB1Status.Text = text;
        WizardVirtualB1Status.Foreground = new System.Windows.Media.SolidColorBrush(color);
    }

    private readonly VoicemeeterRemote _vm = new();
    private bool _suppressB1;
    private bool _suppressA1;
    private bool _suppressVirtualB1;
    private System.Windows.Threading.DispatcherTimer? _stateTimer;

    private int VirtualInputStrip => _vm.LoggedIn ? _vm.FirstVirtualStrip(_vm.StripCount()) : -1;

    private void WizardB1Toggle_Checked(object sender, RoutedEventArgs e)
    {
        if (_suppressB1) return;
        if (!EnsureVmConnected()) return;
        _vm.SetFloat("Strip[0].B1", 1);
        SetB1Status("B1 ON — mic goes to Discord/teams", System.Windows.Media.Color.FromRgb(0x10, 0xB9, 0x81));
    }

    private void WizardB1Toggle_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_suppressB1) return;
        if (!EnsureVmConnected()) return;
        _vm.SetFloat("Strip[0].B1", 0);
        SetB1Status("B1 OFF — mic blocked from Discord/teams", System.Windows.Media.Color.FromRgb(0xE8, 0x55, 0x55));
    }

    private void WizardA1Toggle_Checked(object sender, RoutedEventArgs e)
    {
        if (_suppressA1) return;
        if (App.IsVoiceChangerRunning)
        {
            _suppressA1 = true;
            WizardA1ToggleBtn.IsChecked = false;
            _suppressA1 = false;
            SetA1Status("A1 OFF — locked while voice changer is running", System.Windows.Media.Color.FromRgb(0xE8, 0x55, 0x55));
            return;
        }
        if (!EnsureVmConnected()) return;
        _vm.SetFloat($"Strip[{VirtualInputStrip}].A1", 1);
        SetA1Status("A1 ON — processed voice goes to speakers", System.Windows.Media.Color.FromRgb(0x10, 0xB9, 0x81));
    }

    private void WizardA1Toggle_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_suppressA1) return;
        if (!EnsureVmConnected()) return;
        _vm.SetFloat($"Strip[{VirtualInputStrip}].A1", 0);
        SetA1Status("A1 OFF — processed voice muted from speakers", System.Windows.Media.Color.FromRgb(0xE8, 0x55, 0x55));
    }

    private void WizardVirtualB1Toggle_Checked(object sender, RoutedEventArgs e)
    {
        if (_suppressVirtualB1) return;
        if (!EnsureVmConnected()) return;
        _vm.SetFloat($"Strip[{VirtualInputStrip}].B1", 1);
        SetVirtualB1Status("Virtual B1 ON — processed voice goes to Discord/teams", System.Windows.Media.Color.FromRgb(0x10, 0xB9, 0x81));
    }

    private void WizardVirtualB1Toggle_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_suppressVirtualB1) return;
        if (!EnsureVmConnected()) return;
        _vm.SetFloat($"Strip[{VirtualInputStrip}].B1", 0);
        SetVirtualB1Status("Virtual B1 OFF — processed voice blocked from Discord/teams", System.Windows.Media.Color.FromRgb(0xE8, 0x55, 0x55));
    }

    private bool EnsureVmConnected()
    {
        if (_vm.LoggedIn) return true;
        if (!_vm.Login())
        {
            var message = _vm.Available
                ? "✗ Voicemeeter not running — start it first"
                : "✗ Voicemeeter not installed";
            SetB1Status(message, System.Windows.Media.Color.FromRgb(0xE8, 0x55, 0x55));
            SetA1Status(message, System.Windows.Media.Color.FromRgb(0xE8, 0x55, 0x55));
            SetVirtualB1Status(message, System.Windows.Media.Color.FromRgb(0xE8, 0x55, 0x55));
            return false;
        }
        _stateTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _stateTimer.Tick += (_, _) =>
        {
            RefreshB1State();
            RefreshA1State();
            RefreshVirtualB1State();
        };
        _stateTimer.Start();
        return true;
    }

    private void RefreshB1State()
    {
        bool on = _vm.LoggedIn && _vm.GetFloat("Strip[0].B1") >= 0.5f;
        _suppressB1 = true;
        WizardB1ToggleBtn.IsChecked = on;
        _suppressB1 = false;
        SetB1Status(on
            ? "B1 ON — mic goes to Discord/teams"
            : "B1 OFF — mic blocked from Discord/teams", on
            ? System.Windows.Media.Color.FromRgb(0x10, 0xB9, 0x81)
            : System.Windows.Media.Color.FromRgb(0xE8, 0x55, 0x55));
    }

    private void RefreshA1State()
    {
        bool vcRunning = App.IsVoiceChangerRunning;
        if (vcRunning)
        {
            _suppressA1 = true;
            WizardA1ToggleBtn.IsChecked = false;
            _suppressA1 = false;
            WizardA1ToggleBtn.IsEnabled = false;
            SetA1Status("A1 OFF — voice changer is running", System.Windows.Media.Color.FromRgb(0xE8, 0x55, 0x55));
            return;
        }

        WizardA1ToggleBtn.IsEnabled = true;
        bool on = _vm.LoggedIn && _vm.GetFloat($"Strip[{VirtualInputStrip}].A1") >= 0.5f;
        _suppressA1 = true;
        WizardA1ToggleBtn.IsChecked = on;
        _suppressA1 = false;
        SetA1Status(on
            ? "A1 ON — processed voice goes to speakers"
            : "A1 OFF — processed voice muted from speakers", on
            ? System.Windows.Media.Color.FromRgb(0x10, 0xB9, 0x81)
            : System.Windows.Media.Color.FromRgb(0xE8, 0x55, 0x55));
    }

    private void RefreshVirtualB1State()
    {
        bool on = _vm.LoggedIn && _vm.GetFloat($"Strip[{VirtualInputStrip}].B1") >= 0.5f;
        _suppressVirtualB1 = true;
        WizardVirtualB1ToggleBtn.IsChecked = on;
        _suppressVirtualB1 = false;
        SetVirtualB1Status(on
            ? "Virtual B1 ON — processed voice goes to Discord/teams"
            : "Virtual B1 OFF — processed voice blocked from Discord/teams", on
            ? System.Windows.Media.Color.FromRgb(0x10, 0xB9, 0x81)
            : System.Windows.Media.Color.FromRgb(0xE8, 0x55, 0x55));
    }

    private void WizardRouteInputOnly_Click(object sender, RoutedEventArgs e)
    {
        if (!VoicemeeterRemote.IsInstalled())
        {
            VmSetupStatus.Text = "✗ Voicemeeter not installed.";
            VmSetupStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE8, 0x55, 0x55));
            return;
        }

        var vmOutputId = _audioDeviceService.GetVoicemeeterOutputId();
        if (string.IsNullOrWhiteSpace(vmOutputId))
        {
            VmSetupStatus.Text = "✗ VoiceMeeter Output (B1) not found.";
            VmSetupStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE8, 0x55, 0x55));
            return;
        }

        if (string.IsNullOrWhiteSpace(_config.Settings.SavedDefaultCaptureId))
        {
            _config.Settings.SavedDefaultCaptureId = _audioDeviceService.GetDefaultDeviceId(DataFlow.Capture) ?? string.Empty;
        }

        var inputApplied = _windowsAudioRoutingService.TrySetDefaultInput(vmOutputId);
        var reboundInput = _audioDeviceService.GetDefaultDeviceId(DataFlow.Capture);
        var verified = inputApplied && string.Equals(reboundInput, vmOutputId, StringComparison.OrdinalIgnoreCase);

        _config.Settings.VoicemeeterDetected = true;
        _config.Settings.InputDeviceId = vmOutputId;
        _config.Settings.MicrophoneDeviceId = vmOutputId;
        _configService.Save(_config);

        var virtualB1Activated = false;
        if (EnsureVmConnected() && _vm.GetFloat($"Strip[{VirtualInputStrip}].B1") < 0.5f)
        {
            _vm.SetFloat($"Strip[{VirtualInputStrip}].B1", 1);
            _vm.IsDirty();
            virtualB1Activated = true;
            RefreshVirtualB1State();
        }

        VmSetupStatus.Text = verified
            ? virtualB1Activated
                ? "✓ Input routed to VoiceMeeter Output (B1). Virtual Input B1 also enabled."
                : "✓ Input routed to VoiceMeeter Output (B1). Output device left unchanged."
            : "⚠ Input device setting was attempted, but Windows did not confirm VoiceMeeter Output (B1). Output device left unchanged.";
        VmSetupStatus.Foreground = new System.Windows.Media.SolidColorBrush(verified
            ? System.Windows.Media.Color.FromRgb(0x10, 0xB9, 0x81)
            : System.Windows.Media.Color.FromRgb(0xF5, 0x9E, 0x0B));
    }

    private async void WizardConfigureVmOnly_Click(object sender, RoutedEventArgs e)
    {
        var hear = WizardHearCombo.SelectedItem as AudioDeviceInfo;
        var talk = WizardTalkCombo.SelectedItem as AudioDeviceInfo;
        if (hear is null || talk is null) { VmSetupStatus.Text = "Select both devices first."; return; }

        if (!VoicemeeterRemote.IsInstalled()) { VmSetupStatus.Text = "✗ Voicemeeter not installed."; return; }

        WizardAutoSetupBtn.IsEnabled = false;
        WizardConfigureVmOnlyBtn.IsEnabled = false;
        VmSetupStatus.Text = "Configuring Voicemeeter only…";
        VmSetupStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x98, 0xA0, 0xC0));
        Mouse.OverrideCursor = Cursors.Wait;

        string result;
        try
        {
            var (applied, diagnostics) = await Task.Run(() =>
            {
                using var vm = new VoicemeeterRemote();
                if (!vm.Login()) return (false, "Login failed.");
                bool ok = vm.ApplyRouting(hear.Name, talk.Name);
                string diag = vm.LastDiagnostics;
                vm.Dispose();
                return (ok, diag);
            });

            if (applied)
            {
                _config.Settings.HearDeviceName = hear.Name;
                _config.Settings.TalkDeviceName = talk.Name;
                _config.Settings.SpeakersDeviceName = hear.Name;
                _config.Settings.VoicemeeterDetected = true;
                _configService.Save(_config);

                result = $"✓ Voicemeeter configured only:\n   Hear: {hear.Name}\n   Talk: {talk.Name}\n   Windows defaults unchanged.";
            }
            else
            {
                result = "✗ Setup failed: could not configure Voicemeeter.\n   Check Hear/Talk device names and retry.";
                if (!string.IsNullOrWhiteSpace(diagnostics))
                    result += $"\n\n{diagnostics}";
            }
        }
        catch (Exception ex)
        {
            result = $"✗ Setup failed: {ex.Message}";
        }

        Mouse.OverrideCursor = null;
        WizardAutoSetupBtn.IsEnabled = true;
        WizardConfigureVmOnlyBtn.IsEnabled = true;
        VmSetupStatus.Text = result;
        VmSetupStatus.Foreground = new System.Windows.Media.SolidColorBrush(result.StartsWith("✓")
            ? System.Windows.Media.Color.FromRgb(0x10, 0xB9, 0x81)
            : System.Windows.Media.Color.FromRgb(0xE8, 0x55, 0x55));
    }

    private async void WizardAutoSetup_Click(object sender, RoutedEventArgs e)
    {
        var hear = WizardHearCombo.SelectedItem as AudioDeviceInfo;
        var talk = WizardTalkCombo.SelectedItem as AudioDeviceInfo;
        if (hear is null || talk is null) { VmSetupStatus.Text = "Select both devices first."; return; }

        if (!VoicemeeterRemote.IsInstalled()) { VmSetupStatus.Text = "✗ Voicemeeter not installed."; return; }

        WizardAutoSetupBtn.IsEnabled = false;
        VmSetupStatus.Text = "Setting up…";
        VmSetupStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x98, 0xA0, 0xC0));
        Mouse.OverrideCursor = Cursors.Wait;

        string result;
        try
        {
            var (applied, diagnostics) = await Task.Run(() =>
            {
                using var vm = new VoicemeeterRemote();
                if (!vm.Login()) return (false, "Login failed.");
                bool ok = vm.ApplyRouting(hear.Name, talk.Name);
                string diag = vm.LastDiagnostics;
                vm.Dispose();
                return (ok, diag);
            });

            if (applied)
            {
                _config.Settings.HearDeviceName = hear.Name;
                _config.Settings.TalkDeviceName = talk.Name;
                _config.Settings.SpeakersDeviceName = hear.Name;
                _config.Settings.VoicemeeterDetected = true;

                bool routed = ApplyAppsRouting();
                if (routed)
                {
                    var vmInputId = _audioDeviceService.GetVoicemeeterInputId();
                    var vmOutputId = _audioDeviceService.GetVoicemeeterOutputId();
                    if (!string.IsNullOrWhiteSpace(vmInputId))
                    {
                        _config.Settings.OutputDeviceId = vmInputId;
                        _config.Settings.PlaybackDeviceId = vmInputId;
                    }
                    if (!string.IsNullOrWhiteSpace(vmOutputId))
                    {
                        _config.Settings.InputDeviceId = vmOutputId;
                        _config.Settings.MicrophoneDeviceId = vmOutputId;
                    }
                }

                _configService.Save(_config);
                var parts = new List<string> { $"✓ Voicemeeter configured:\n   Hear: {hear.Name}\n   Talk: {talk.Name}" };
                if (routed)
                {
                    var vmInputId = _audioDeviceService.GetVoicemeeterInputId();
                    var vmOutputId = _audioDeviceService.GetVoicemeeterOutputId();
                    var rbOut = _audioDeviceService.GetDefaultDeviceId(DataFlow.Render);
                    var rbIn = _audioDeviceService.GetDefaultDeviceId(DataFlow.Capture);
                    if (!string.IsNullOrWhiteSpace(vmInputId) && string.Equals(rbOut, vmInputId, StringComparison.OrdinalIgnoreCase))
                        parts.Add("   ✓ App output + Windows playback → VoiceMeeter Input");
                    else if (!string.IsNullOrWhiteSpace(vmInputId))
                        parts.Add("   ⚠  VoiceMeeter Input set failed — Windows playback unchanged");
                    else
                        parts.Add("   ⚠  VoiceMeeter Input not found — playback not routed");
                    if (!string.IsNullOrWhiteSpace(vmOutputId) && string.Equals(rbIn, vmOutputId, StringComparison.OrdinalIgnoreCase))
                        parts.Add("   ✓ Mic + Windows input → VoiceMeeter Output (B1)");
                    else if (!string.IsNullOrWhiteSpace(vmOutputId))
                        parts.Add("   ⚠  VoiceMeeter Output (B1) set failed — Windows input unchanged");
                    else
                        parts.Add("   ⚠  VoiceMeeter Output (B1) not found — input not routed");
                }
                result = string.Join("\n", parts);
            }
            else
            {
                result = "✗ Setup failed: could not configure Voicemeeter.\n   Check Hear/Talk device names and retry.";
                if (!string.IsNullOrWhiteSpace(diagnostics))
                    result += $"\n\n{diagnostics}";
            }
        }
        catch (Exception ex)
        {
            result = $"✗ Setup failed: {ex.Message}";
        }

        Mouse.OverrideCursor = null;
        WizardAutoSetupBtn.IsEnabled = true;
        VmSetupStatus.Text = result;
        VmSetupStatus.Foreground = new System.Windows.Media.SolidColorBrush(result.StartsWith("✓")
            ? System.Windows.Media.Color.FromRgb(0x10, 0xB9, 0x81)
            : System.Windows.Media.Color.FromRgb(0xE8, 0x55, 0x55));
    }

    private bool ApplyAppsRouting()
    {
        if (RouteAppsToVmCheckBox.IsChecked != true)
            return false;

        var vmInputId = _audioDeviceService.GetVoicemeeterInputId();
        var vmOutputId = _audioDeviceService.GetVoicemeeterOutputId();
        if (string.IsNullOrWhiteSpace(vmInputId))
            return false;

        if (string.IsNullOrWhiteSpace(_config.Settings.SavedDefaultRenderId))
            _config.Settings.SavedDefaultRenderId = _audioDeviceService.GetDefaultDeviceId(DataFlow.Render) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(_config.Settings.SavedDefaultCaptureId))
            _config.Settings.SavedDefaultCaptureId = _audioDeviceService.GetDefaultDeviceId(DataFlow.Capture) ?? string.Empty;

        bool ok = true;
        if (!string.IsNullOrWhiteSpace(vmOutputId))
            ok &= _windowsAudioRoutingService.TrySetDefaultInput(vmOutputId);
        ok &= TrySetDefaultOutputVerified(vmInputId);
        return ok;
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

    private void RevertAppsRouting()
    {
        var previousRender = (WizardHearCombo.SelectedItem as AudioDeviceInfo)?.Id;
        var previousCapture = (WizardTalkCombo.SelectedItem as AudioDeviceInfo)?.Id;
        _windowsAudioRoutingService.TrySetDefaultDevices(previousRender ?? string.Empty, previousCapture ?? string.Empty);
        _config.Settings.SavedDefaultRenderId = string.Empty;
        _config.Settings.SavedDefaultCaptureId = string.Empty;
    }

    private async void WizardResetWindows_Click(object sender, RoutedEventArgs e)
    {
        RevertAppsRouting();
        _configService.Save(_config);
        WizardStatusText.Text = "Resetting Voicemeeter devices…";
        WizardStatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x98, 0xA0, 0xC0));
        WizardResetWindowsBtn.IsEnabled = false;

        string vmResult;
        try
        {
            vmResult = await Task.Run(() =>
            {
                using var vm = new VoicemeeterRemote();
                var inputs = _audioDeviceService.GetAllInputDevices().Select(d => d.Name).ToList();
                var outputs = _audioDeviceService.GetAllOutputDevices().Select(d => d.Name).ToList();
                return vm.ResetRouting(inputs, outputs);
            });
        }
        catch (Exception ex)
        {
            vmResult = $"✗ Voicemeeter reset failed: {ex.Message}";
        }

        WizardResetWindowsBtn.IsEnabled = true;
        WizardStatusText.Text = $"Windows sound settings reset to selected devices.\n{vmResult}";
        WizardStatusText.Foreground = new System.Windows.Media.SolidColorBrush(vmResult.StartsWith("✓")
            ? System.Windows.Media.Color.FromRgb(0x10, 0xB9, 0x81)
            : System.Windows.Media.Color.FromRgb(0xE8, 0x55, 0x55));
    }

    private void WizardTestHear_Click(object sender, RoutedEventArgs e)
    {
        var monitor = new TeamMonitorWindow { Owner = this };
        monitor.ShowDialog();
    }

    private void Finish_Click(object sender, RoutedEventArgs e)
    {
        ApplySelection();
        if (DontShowAgainCheckBox.IsChecked == true)
            _config.Settings.ShowSetupWizardOnStartup = false;

        _config.Settings.SetupCompleted = true;
        _config.Settings.LastConfigurationDate = DateTime.UtcNow;
        _configService.Save(_config);
        DialogResult = true;
    }

    private void OpenSound_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo("control", "mmsys.cpl,,1") { UseShellExecute = true }); }
        catch { WizardStatusText.Text = "Could not open Windows Sound settings."; }
    }

    private void ApplySelection()
    {
        _config.Settings.VirtualCableDeviceId = string.Empty;
        _config.Settings.VBCableDetected = false;
        WizardStatusText.Text = "Settings saved.";
    }
}
