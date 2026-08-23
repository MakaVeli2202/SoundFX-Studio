using SoundFXStudio.Models;
using SoundFXStudio.Services;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using NAudio.CoreAudioApi;

namespace SoundFXStudio.Views.Dialogs;

public partial class SetupWizardWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr h, int attribute, ref int value, int size);

    private readonly ConfigService _configService = new();
    private readonly AudioDeviceService _audioDeviceService = new();
    private readonly WindowsAudioRoutingService _windowsAudioRoutingService = new();
    private AppConfig _config;

    public SetupWizardWindow()
    {
        InitializeComponent();
        _config = _configService.Load();
        Loaded += SetupWizardWindow_Loaded;
        PreviewMouseLeftButtonDown += TryDragWindow;
        PreviewKeyDown += SetupWizardWindow_PreviewKeyDown;
        SourceInitialized += SetupWizardWindow_SourceInitialized;
    }

    private void SetupWizardWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape)
        {
            if (WizardHearCombo.IsDropDownOpen || WizardTalkCombo.IsDropDownOpen)
            {
                CloseDropdownPanels();
                e.Handled = true;
                return;
            }

            Close();
        }
    }

    private void SetupWizardWindow_SourceInitialized(object? sender, EventArgs e)
    {
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        int cornerPreference = 2;
        DwmSetWindowAttribute(hwnd, 33, ref cornerPreference, Marshal.SizeOf<int>());
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
            if (source is System.Windows.Controls.Primitives.Popup)
            {
                return;
            }

            if (source is System.Windows.Window)
            {
                break;
            }

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

        if (source is null)
        {
            return;
        }

        CloseDropdownPanels();
        DragMove();
    }

    private void CloseDropdownPanels()
    {
        WizardHearCombo.IsDropDownOpen = false;
        WizardTalkCombo.IsDropDownOpen = false;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void SetupWizardWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var outputs = _audioDeviceService.GetOutputDevices().ToList();
        var inputs = _audioDeviceService.GetInputDevices().ToList();

        WizardHearCombo.ItemsSource = outputs;
        WizardTalkCombo.ItemsSource = inputs;

        WizardHearCombo.SelectedItem = outputs.FirstOrDefault(d => d.Name == _config.Settings.HearDeviceName)
            ?? outputs.FirstOrDefault(d => d.Id == _audioDeviceService.GetDefaultDeviceId(DataFlow.Render))
            ?? outputs.FirstOrDefault(d => d.Id == _audioDeviceService.GetDefaultCommunicationDeviceId(DataFlow.Render))
            ?? outputs.FirstOrDefault(d => d.IsDefaultCommunication)
            ?? outputs.FirstOrDefault(d => d.IsDefault)
            ?? outputs.FirstOrDefault();

        WizardTalkCombo.SelectedItem = inputs.FirstOrDefault(d => d.Name == _config.Settings.TalkDeviceName)
            ?? inputs.FirstOrDefault(d => d.Id == _audioDeviceService.GetDefaultDeviceId(DataFlow.Capture))
            ?? inputs.FirstOrDefault(d => d.Id == _audioDeviceService.GetDefaultCommunicationDeviceId(DataFlow.Capture))
            ?? inputs.FirstOrDefault(d => d.IsDefaultCommunication)
            ?? inputs.FirstOrDefault(d => d.IsDefault)
            ?? inputs.FirstOrDefault();

        CheckVoicemeeter();
    }

    private void CheckVoicemeeter()
    {
        var green = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x10, 0xB9, 0x81));
        var red = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF4, 0x3F, 0x5E));
        if (VoicemeeterService.IsVoicemeeterInstalled())
        {
            VmStatusDot.Fill = green;
            WizardAutoSetupBtn.IsEnabled = true;
        }
        else
        {
            VmStatusDot.Fill = red;
            WizardAutoSetupBtn.IsEnabled = false;
        }
    }

    private async void WizardAutoSetup_Click(object sender, RoutedEventArgs e)
    {
        var hear = WizardHearCombo.SelectedItem as AudioDeviceInfo;
        var talk = WizardTalkCombo.SelectedItem as AudioDeviceInfo;
        if (hear is null || talk is null) { VmSetupStatus.Text = "Select both devices first."; return; }

        Services.ActionLog.Instance.Action("Wizard", $"AutoSetup: hear='{hear.Name}', talk='{talk.Name}'");

        if (!VoicemeeterRemote.IsInstalled()) { VmSetupStatus.Text = "✗ Audio engine not installed."; return; }

        WizardAutoSetupBtn.IsEnabled = false;

        var overlay = new ProgressOverlayWindow("Setting up") { Owner = this };
        overlay.Show();

        string result = string.Empty;
        try
        {
            var (applied, diagnostics) = await Task.Run(() =>
            {
                overlay.UpdateStep("Connecting to audio engine…");
                using var vm = new VoicemeeterRemote();
                if (!vm.Login()) return (false, "Could not connect.");
                bool ok = vm.ApplyRouting(hear.Name, talk.Name, step => overlay.UpdateStep(step));
                string diag = vm.LastDiagnostics;
                vm.Dispose();
                return (ok, diag);
            });

            if (applied)
            {
                overlay.UpdateStep("Wiring Windows input…");
                _config.Settings.HearDeviceName = hear.Name;
                _config.Settings.TalkDeviceName = talk.Name;
                _config.Settings.SpeakersDeviceName = hear.Name;
                _config.Settings.VoicemeeterDetected = true;

                var vmOutputId = _audioDeviceService.GetVoicemeeterOutputId();
                if (string.IsNullOrWhiteSpace(vmOutputId))
                {
                    result = $"✓ Channels configured:\n   Output: {hear.Name}\n   Mic: {talk.Name}\n   ⚠  Windows input not routed — virtual output not found";
                }
                else
                {
                    var currentCapture = _audioDeviceService.GetDefaultDeviceId(DataFlow.Capture);
                    if (!string.IsNullOrWhiteSpace(currentCapture)
                        && !string.Equals(currentCapture, vmOutputId, StringComparison.OrdinalIgnoreCase))
                    {
                        _config.Settings.SavedDefaultCaptureId = currentCapture;
                    }

                    var currentRender = _audioDeviceService.GetDefaultDeviceId(DataFlow.Render);
                    if (!string.IsNullOrWhiteSpace(currentRender))
                    {
                        _config.Settings.SavedDefaultRenderId = currentRender;
                    }

                    var inputApplied = _windowsAudioRoutingService.TrySetDefaultInput(vmOutputId);
                    var reboundInput = _audioDeviceService.GetDefaultDeviceId(DataFlow.Capture);
                    var verified = inputApplied && string.Equals(reboundInput, vmOutputId, StringComparison.OrdinalIgnoreCase);

                    _config.Settings.InputDeviceId = vmOutputId;
                    _config.Settings.MicrophoneDeviceId = vmOutputId;

                    result = $"✓ Channels configured:\n   Output: {hear.Name}\n   Mic: {talk.Name}\n   " +
                        (verified
                            ? "✓ Windows input wired"
                            : "⚠  Windows input wiring unconfirmed");
                }

            try { _configService.Save(_config); } catch { }
                overlay.Complete("Ready to play!");
                ToastWindow.ShowDiscordStudioTip();
            }
            else
            {
                result = "✗ Setup failed — check your devices and retry.";
                if (!string.IsNullOrWhiteSpace(diagnostics))
                    result += $"\n\n{diagnostics}";
                overlay.Complete("Setup failed — check status.");
            }
        }
        catch (Exception ex)
        {
            result = $"✗ Setup failed: {ex.Message}";
            overlay.Complete("Setup failed — check the status below.");
        }

        await Task.Delay(1400);
        overlay.Close();
        WizardAutoSetupBtn.IsEnabled = true;
        VmSetupStatus.Text = result;
        Services.ActionLog.Instance.Action("Wizard", $"AutoSetup result: {result.Replace("\n", " | ")}");
        VmSetupStatus.Foreground = new System.Windows.Media.SolidColorBrush(result.StartsWith("✓")
            ? System.Windows.Media.Color.FromRgb(0x10, 0xB9, 0x81)
            : System.Windows.Media.Color.FromRgb(0xE8, 0x55, 0x55));
    }

    private async void WizardResetWindows_Click(object sender, RoutedEventArgs e)
    {
        WizardStatusText.Text = "Resetting channels…";
        WizardStatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x98, 0xA0, 0xC0));
        WizardResetWindowsBtn.IsEnabled = false;

        string windowsResult = "";
        var previousRender = _config.Settings.SavedDefaultRenderId;
        var previousCapture = _config.Settings.SavedDefaultCaptureId;

        if (!string.IsNullOrWhiteSpace(previousRender) || !string.IsNullOrWhiteSpace(previousCapture))
        {
            bool restored = _windowsAudioRoutingService.TrySetDefaultDevices(previousRender ?? "", previousCapture ?? "");
            windowsResult = restored
                ? "✓ Windows defaults restored"
                : "⚠  Could not restore Windows defaults";
            if (restored)
            {
                _config.Settings.SavedDefaultRenderId = string.Empty;
                _config.Settings.SavedDefaultCaptureId = string.Empty;
            }
        }
        else
        {
            windowsResult = "No saved Windows defaults to restore";
        }

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
            vmResult = $"✗ Reset failed: {ex.Message}";
        }

        _config.Settings.HearDeviceName = string.Empty;
        _config.Settings.TalkDeviceName = string.Empty;
        _config.Settings.VoicemeeterDetected = false;
        try { _configService.Save(_config); } catch { }

        WizardResetWindowsBtn.IsEnabled = true;
        WizardStatusText.Text = $"{vmResult}\n{windowsResult}";
        WizardStatusText.Foreground = new System.Windows.Media.SolidColorBrush(
            vmResult.StartsWith("✓") && windowsResult.StartsWith("✓")
            ? System.Windows.Media.Color.FromRgb(0x10, 0xB9, 0x81)
            : System.Windows.Media.Color.FromRgb(0xE8, 0x55, 0x55));
    }

    private void Finish_Click(object sender, RoutedEventArgs e)
    {
        ApplySelection();
        if (DontShowAgainCheckBox.IsChecked == true)
            _config.Settings.ShowSetupWizardOnStartup = false;

        _config.Settings.SetupCompleted = true;
        _config.Settings.LastConfigurationDate = DateTime.UtcNow;

        try
        {
            _configService.Save(_config);
            Services.ActionLog.Instance.Info("Wizard", $"Finish: SetupCompleted=true saved to disk");
        }
        catch (Exception ex)
        {
            Services.ActionLog.Instance.Error("Wizard", $"Finish: Save failed: {ex.Message}");
        }

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
