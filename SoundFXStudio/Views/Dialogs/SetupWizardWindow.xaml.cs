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

        WizardHearCombo.SelectedItem = outputs.FirstOrDefault(d => d.Name == _config.Settings.HearDeviceName) ?? outputs.FirstOrDefault();
        WizardTalkCombo.SelectedItem = inputs.FirstOrDefault(d => d.Name == _config.Settings.TalkDeviceName) ?? inputs.FirstOrDefault();

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

        if (!VoicemeeterRemote.IsInstalled()) { VmSetupStatus.Text = "✗ Voicemeeter not installed."; return; }

        WizardAutoSetupBtn.IsEnabled = false;

        var overlay = new ProgressOverlayWindow("Auto Setup") { Owner = this };
        overlay.Show();

        string result = string.Empty;
        try
        {
            var (applied, diagnostics) = await Task.Run(() =>
            {
                overlay.UpdateStep("Connecting to Voicemeeter…");
                using var vm = new VoicemeeterRemote();
                if (!vm.Login()) return (false, "Login failed.");
                bool ok = vm.ApplyRouting(hear.Name, talk.Name, step => overlay.UpdateStep(step));
                string diag = vm.LastDiagnostics;
                vm.Dispose();
                return (ok, diag);
            });

            if (applied)
            {
                overlay.UpdateStep("Routing Windows input…");
                _config.Settings.HearDeviceName = hear.Name;
                _config.Settings.TalkDeviceName = talk.Name;
                _config.Settings.SpeakersDeviceName = hear.Name;
                _config.Settings.VoicemeeterDetected = true;

                var vmOutputId = _audioDeviceService.GetVoicemeeterOutputId();
                if (string.IsNullOrWhiteSpace(vmOutputId))
                {
                    result = $"✓ Voicemeeter configured:\n   Hear: {hear.Name}\n   Talk: {talk.Name}\n   ⚠  VoiceMeeter Output (B1) not found — Windows input not routed";
                }
                else
                {
                    var currentCapture = _audioDeviceService.GetDefaultDeviceId(DataFlow.Capture);
                    if (!string.IsNullOrWhiteSpace(currentCapture)
                        && !string.Equals(currentCapture, vmOutputId, StringComparison.OrdinalIgnoreCase))
                    {
                        _config.Settings.SavedDefaultCaptureId = currentCapture;
                    }

                    var inputApplied = _windowsAudioRoutingService.TrySetDefaultInput(vmOutputId);
                    var reboundInput = _audioDeviceService.GetDefaultDeviceId(DataFlow.Capture);
                    var verified = inputApplied && string.Equals(reboundInput, vmOutputId, StringComparison.OrdinalIgnoreCase);

                    _config.Settings.InputDeviceId = vmOutputId;
                    _config.Settings.MicrophoneDeviceId = vmOutputId;

                    result = $"✓ Voicemeeter configured:\n   Hear: {hear.Name}\n   Talk: {talk.Name}\n   " +
                        (verified
                            ? "✓ Windows input → VoiceMeeter Output (B1)"
                            : "⚠  Windows input → VoiceMeeter Output (B1) not confirmed");
                }

            try { _configService.Save(_config); } catch { }
                overlay.Complete("Everything ready — have fun!");
                ToastWindow.ShowDiscordStudioTip();
            }
            else
            {
                result = "✗ Setup failed: could not configure Voicemeeter.\n   Check Hear/Talk device names and retry.";
                if (!string.IsNullOrWhiteSpace(diagnostics))
                    result += $"\n\n{diagnostics}";
                overlay.Complete("Setup failed — check the status below.");
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
        VmSetupStatus.Foreground = new System.Windows.Media.SolidColorBrush(result.StartsWith("✓")
            ? System.Windows.Media.Color.FromRgb(0x10, 0xB9, 0x81)
            : System.Windows.Media.Color.FromRgb(0xE8, 0x55, 0x55));
    }

    private async void WizardResetWindows_Click(object sender, RoutedEventArgs e)
    {
        WizardStatusText.Text = "Resetting Voicemeeter devices…";
        WizardStatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x98, 0xA0, 0xC0));
        WizardResetWindowsBtn.IsEnabled = false;

        string windowsResult = "Windows input device unchanged.";
        if (!string.IsNullOrWhiteSpace(_config.Settings.SavedDefaultCaptureId))
        {
            bool restored = _windowsAudioRoutingService.TrySetDefaultInput(_config.Settings.SavedDefaultCaptureId);
            windowsResult = restored
                ? "✓ Windows input restored to previous device"
                : "⚠ Could not restore Windows input device — kept for retry";
            if (restored)
            {
                _config.Settings.SavedDefaultCaptureId = string.Empty;
            }
            _configService.Save(_config);
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
            vmResult = $"✗ Voicemeeter reset failed: {ex.Message}";
        }

        WizardResetWindowsBtn.IsEnabled = true;
        WizardStatusText.Text = $"{windowsResult}\n{vmResult}";
        WizardStatusText.Foreground = new System.Windows.Media.SolidColorBrush(vmResult.StartsWith("✓")
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

        try { _configService.Save(_config); } catch { }

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
