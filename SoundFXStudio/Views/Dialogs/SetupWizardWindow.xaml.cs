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
    private bool _ready;

    public SetupWizardWindow()
    {
        InitializeComponent();
        _config = _configService.Load();
        Loaded += SetupWizardWindow_Loaded;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2) return;
        DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void SetupWizardWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var outputs = _audioDeviceService.GetOutputDevices().ToList();
        var inputs = _audioDeviceService.GetInputDevices().ToList();

        OutputCombo.ItemsSource = outputs;
        InputCombo.ItemsSource = inputs;
        WizardHearCombo.ItemsSource = outputs;
        WizardTalkCombo.ItemsSource = inputs;

        OutputDefaultText.Text = BuildDefaultLabel(_audioDeviceService.GetDefaultDeviceName(DataFlow.Render));
        InputDefaultText.Text = BuildDefaultLabel(_audioDeviceService.GetDefaultDeviceName(DataFlow.Capture));

        SelectSavedOrDefault(OutputCombo, _config.Settings.OutputDeviceId, outputs);
        SelectSavedOrDefault(InputCombo, _config.Settings.InputDeviceId, inputs);
        WizardHearCombo.SelectedItem = outputs.FirstOrDefault(d => d.Name == _config.Settings.HearDeviceName) ?? outputs.FirstOrDefault();
        WizardTalkCombo.SelectedItem = inputs.FirstOrDefault(d => d.Name == _config.Settings.TalkDeviceName) ?? inputs.FirstOrDefault();

        UpdateStatus();
        CheckVoicemeeter();
        _ready = true;
    }

    private void CheckVoicemeeter()
    {
        if (VoicemeeterService.IsVoicemeeterInstalled())
        {
            VmDetectText.Text = "✓ Voicemeeter detected";
            VmDetectText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x10, 0xB9, 0x81));
            WizardAutoSetupBtn.IsEnabled = true;
        }
        else
        {
            VmDetectText.Text = "✗ Voicemeeter not installed — install it for best routing";
            VmDetectText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF4, 0x3F, 0x5E));
            WizardAutoSetupBtn.IsEnabled = false;
        }
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
                    if (!string.IsNullOrWhiteSpace(vmInputId))
                        parts.Add("   ✓ App output + Windows playback → VoiceMeeter Input");
                    else
                        parts.Add("   ⚠  VoiceMeeter Input not found — playback not routed");
                    if (!string.IsNullOrWhiteSpace(vmOutputId))
                        parts.Add("   ✓ Mic + Windows input → VoiceMeeter Output (B1)");
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

        return _windowsAudioRoutingService.TrySetDefaultDevices(vmInputId, vmOutputId ?? string.Empty);
    }

    private void RevertAppsRouting()
    {
        var previousRender = _config.Settings.SavedDefaultRenderId;
        var previousCapture = _config.Settings.SavedDefaultCaptureId;
        if (string.IsNullOrWhiteSpace(previousRender) && string.IsNullOrWhiteSpace(previousCapture))
            return;

        _windowsAudioRoutingService.TrySetDefaultDevices(previousRender, previousCapture);
        _config.Settings.SavedDefaultRenderId = string.Empty;
        _config.Settings.SavedDefaultCaptureId = string.Empty;
    }

    private void RouteAppsToVmCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        ApplyAppsRouting();
        _configService.Save(_config);
    }

    private void RouteAppsToVmCheckBox_Unchecked(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        RevertAppsRouting();
        _configService.Save(_config);
    }

    private void WizardResetWindows_Click(object sender, RoutedEventArgs e)
    {
        RevertAppsRouting();
        RouteAppsToVmCheckBox.IsChecked = false;
        _configService.Save(_config);
        WizardStatusText.Text = "Windows sound settings reset to previous defaults.";
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
        string? outputId = null;
        string? inputId = null;

        if (OutputCombo.SelectedItem is AudioDeviceInfo output)
        {
            outputId = output.Id;
            _config.Settings.OutputDeviceId = output.Id;
            _config.Settings.PlaybackDeviceId = output.Id;
        }
        if (InputCombo.SelectedItem is AudioDeviceInfo input)
        {
            inputId = input.Id;
            _config.Settings.InputDeviceId = input.Id;
            _config.Settings.MicrophoneDeviceId = input.Id;
        }

        _config.Settings.VirtualCableDeviceId = string.Empty;
        _config.Settings.VBCableDetected = false;

        bool appsRouted = ApplyAppsRouting();
        bool defaultsOk;
        if (appsRouted)
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
            defaultsOk = true;
        }
        else
        {
            defaultsOk = _windowsAudioRoutingService.TrySetDefaultDevices(outputId ?? string.Empty, inputId ?? string.Empty);
        }

        if (defaultsOk)
            WizardStatusText.Text = "System defaults updated.";
        else
            WizardStatusText.Text = "Saved. System defaults could not be updated.";
    }

    private static void SelectSavedOrDefault(System.Windows.Controls.ComboBox combo, string? savedId, List<AudioDeviceInfo> items)
    {
        if (!string.IsNullOrWhiteSpace(savedId))
        {
            foreach (var item in items)
            {
                if (string.Equals(item.Id, savedId, StringComparison.OrdinalIgnoreCase))
                {
                    combo.SelectedItem = item;
                    return;
                }
            }
        }
        foreach (var item in items)
        {
            if (item.IsDefaultCommunication || item.IsDefault)
            {
                combo.SelectedItem = item;
                return;
            }
        }
        if (items.Count > 0) combo.SelectedIndex = 0;
    }

    private static string BuildDefaultLabel(string? deviceName)
        => string.IsNullOrWhiteSpace(deviceName) ? "Current Windows Default: not detected" : $"Current Windows Default: {deviceName}";

    private void UpdateStatus()
    {
        var output = OutputCombo.SelectedItem as AudioDeviceInfo;
        var input = InputCombo.SelectedItem as AudioDeviceInfo;
        WizardStatusText.Text = $"Output: {output?.Name ?? "none"} | Input: {input?.Name ?? "none"}";
    }
}
