using SoundFXStudio.Models;
using SoundFXStudio.Services;
using SoundFXStudio.ViewModels;
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
    private readonly MainViewModel? _viewModel;
    private AppConfig _config;
    private bool _cleanupInFlight;

    /// <summary>
    /// Pass the live <paramref name="viewModel"/> so the wizard edits the same
    /// settings instance the running app uses. The startup path (App.xaml) has no
    /// view model yet and passes null, which falls back to a freshly loaded config.
    /// </summary>
    public SetupWizardWindow(MainViewModel? viewModel = null)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _config = _configService.Load();
        Loaded += SetupWizardWindow_Loaded;
        PreviewMouseLeftButtonDown += TryDragWindow;
        PreviewKeyDown += SetupWizardWindow_PreviewKeyDown;
        SourceInitialized += SetupWizardWindow_SourceInitialized;
    }

    private AppSettings Settings => _viewModel?.Settings ?? _config.Settings;

    private void SaveConfig()
    {
        if (_viewModel is not null)
        {
            _viewModel.Save();
            return;
        }

        try { _configService.Save(_config); } catch { }
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

    private async void SetupWizardWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var outputs = _audioDeviceService.GetOutputDevices().ToList();
        var inputs = _audioDeviceService.GetInputDevices().ToList();

        WizardHearCombo.ItemsSource = outputs;
        WizardTalkCombo.ItemsSource = inputs;

        WizardHearCombo.SelectedItem = outputs.FirstOrDefault(d => d.Name == Settings.HearDeviceName)
            ?? outputs.FirstOrDefault(d => d.Id == _audioDeviceService.GetDefaultDeviceId(DataFlow.Render))
            ?? outputs.FirstOrDefault(d => d.Id == _audioDeviceService.GetDefaultCommunicationDeviceId(DataFlow.Render))
            ?? outputs.FirstOrDefault(d => d.IsDefaultCommunication)
            ?? outputs.FirstOrDefault(d => d.IsDefault)
            ?? outputs.FirstOrDefault();

        WizardTalkCombo.SelectedItem = inputs.FirstOrDefault(d => d.Name == Settings.TalkDeviceName)
            ?? inputs.FirstOrDefault(d => d.Id == _audioDeviceService.GetDefaultDeviceId(DataFlow.Capture))
            ?? inputs.FirstOrDefault(d => d.Id == _audioDeviceService.GetDefaultCommunicationDeviceId(DataFlow.Capture))
            ?? inputs.FirstOrDefault(d => d.IsDefaultCommunication)
            ?? inputs.FirstOrDefault(d => d.IsDefault)
            ?? inputs.FirstOrDefault();

        CheckVoicemeeter();
        await HideUnusedVoicemeeterChannelsOnceAsync();
    }

    /// <summary>
    /// The app only ever routes through Voicemeeter Input (playback) and Out B1
    /// (microphone), so every other Voicemeeter endpoint is dead weight in the
    /// Windows sound flyout. Runs once ever — the VoicemeeterEndpointsCleaned flag
    /// is the latch, so later launches never prompt for elevation again.
    /// </summary>
    private async Task HideUnusedVoicemeeterChannelsOnceAsync()
    {
        if (_cleanupInFlight || Settings.VoicemeeterEndpointsCleaned) return;
        if (!VoicemeeterService.IsVoicemeeterInstalled()) return;

        var renderId = _audioDeviceService.GetVoicemeeterInputId();
        var captureId = _audioDeviceService.GetVoicemeeterOutputId();
        if (string.IsNullOrWhiteSpace(renderId) || string.IsNullOrWhiteSpace(captureId)) return;

        _cleanupInFlight = true;
        try
        {
            VmSetupStatus.Text = "Tidying unused Voicemeeter channels — Windows may ask for permission…";

            var result = await VoicemeeterEndpointCleanupService.CleanupAsync(renderId, captureId);
            if (result == VoicemeeterCleanupResult.Failed)
            {
                VmSetupStatus.Text = "Voicemeeter channels left as they are (permission declined).";
                return;
            }

            Settings.VoicemeeterEndpointsCleaned = true;
            SaveConfig();

            VmSetupStatus.Text = result == VoicemeeterCleanupResult.Completed
                ? "Unused Voicemeeter channels hidden from Windows sound settings."
                : "Voicemeeter channels are already tidy.";
            Services.ActionLog.Instance.Action("Wizard", $"Voicemeeter endpoint cleanup: {result}");
        }
        catch (Exception ex)
        {
            VmSetupStatus.Text = "Could not tidy Voicemeeter channels.";
            Services.ActionLog.Instance.Error("Wizard", $"Voicemeeter endpoint cleanup failed: {ex.Message}");
        }
        finally
        {
            _cleanupInFlight = false;
        }
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
                overlay.UpdateStep("Routing Windows playback and microphone…");
                Settings.HearDeviceName = hear.Name;
                Settings.TalkDeviceName = talk.Name;
                Settings.SpeakersDeviceName = hear.Name;
                Settings.VoicemeeterDetected = true;

                var vmInputId = _audioDeviceService.GetVoicemeeterInputId();
                var vmOutputId = _audioDeviceService.GetVoicemeeterOutputId();
                if (string.IsNullOrWhiteSpace(vmInputId) || string.IsNullOrWhiteSpace(vmOutputId))
                {
                    result = "✗ Voicemeeter endpoints were not found; Windows devices were not changed.";
                    overlay.Complete("Voicemeeter endpoints not found.", succeeded: false);
                }
                else
                {
                    var currentCapture = _audioDeviceService.GetDefaultDeviceId(DataFlow.Capture);
                    var currentRender = _audioDeviceService.GetDefaultDeviceId(DataFlow.Render);
                    if (string.IsNullOrWhiteSpace(Settings.SavedDefaultCaptureId)
                        || string.Equals(Settings.SavedDefaultCaptureId, vmOutputId, StringComparison.OrdinalIgnoreCase))
                    {
                        Settings.SavedDefaultCaptureId =
                            !string.IsNullOrWhiteSpace(currentCapture)
                            && !string.Equals(currentCapture, vmOutputId, StringComparison.OrdinalIgnoreCase)
                                ? currentCapture
                                : talk.Id;
                    }
                    if (string.IsNullOrWhiteSpace(Settings.SavedDefaultRenderId)
                        || string.Equals(Settings.SavedDefaultRenderId, vmInputId, StringComparison.OrdinalIgnoreCase))
                    {
                        Settings.SavedDefaultRenderId =
                            !string.IsNullOrWhiteSpace(currentRender)
                            && !string.Equals(currentRender, vmInputId, StringComparison.OrdinalIgnoreCase)
                                ? currentRender
                                : hear.Id;
                    }

                    var outputApplied = _windowsAudioRoutingService.TrySetDefaultOutput(vmInputId);
                    var inputApplied = _windowsAudioRoutingService.TrySetDefaultInput(vmOutputId);
                    var outputVerified = outputApplied && string.Equals(
                        _audioDeviceService.GetDefaultDeviceId(DataFlow.Render), vmInputId, StringComparison.OrdinalIgnoreCase);
                    var inputVerified = inputApplied && string.Equals(
                        _audioDeviceService.GetDefaultDeviceId(DataFlow.Capture), vmOutputId, StringComparison.OrdinalIgnoreCase);

                    Settings.OutputDeviceId = vmInputId;
                    Settings.PlaybackDeviceId = vmInputId;
                    Settings.InputDeviceId = vmOutputId;
                    Settings.MicrophoneDeviceId = vmOutputId;

                    var cleanupSucceeded = false;
                    if (outputVerified && inputVerified)
                    {
                        overlay.UpdateStep("Hiding unused Voicemeeter channels…");
                        var cleanupResult = Settings.VoicemeeterEndpointsCleaned
                            ? VoicemeeterCleanupResult.NothingToHide
                            : await VoicemeeterEndpointCleanupService.CleanupAsync(vmInputId, vmOutputId);
                        cleanupSucceeded = cleanupResult != VoicemeeterCleanupResult.Failed;
                        if (cleanupSucceeded)
                        {
                            Settings.VoicemeeterEndpointsCleaned = true;
                            outputApplied = _windowsAudioRoutingService.TrySetDefaultOutput(vmInputId);
                            inputApplied = _windowsAudioRoutingService.TrySetDefaultInput(vmInputId);
                            outputVerified = outputApplied && string.Equals(
                                _audioDeviceService.GetDefaultDeviceId(DataFlow.Render), vmInputId, StringComparison.OrdinalIgnoreCase);
                            inputVerified = inputApplied && string.Equals(
                                _audioDeviceService.GetDefaultDeviceId(DataFlow.Capture), vmOutputId, StringComparison.OrdinalIgnoreCase);
                        }
                    }

                    var routingVerified = outputVerified && inputVerified;

                    result = routingVerified
                        ? $"✓ Audio configured:\n   Playback: {hear.Name}\n   Microphone: {talk.Name}"
                        : $"✗ Voicemeeter channels configured, but Windows routing was not verified.\n   Playback: {(outputVerified ? "✓" : "✗")}\n   Microphone: {(inputVerified ? "✓" : "✗")}";
                    if (routingVerified && !cleanupSucceeded)
                        result += "\n⚠ Unused Voicemeeter channels could not be hidden; Windows defaults are configured.";

                    SaveConfig();
                    overlay.Complete(routingVerified ? "Ready to play!" : "Windows routing could not be verified.", succeeded: routingVerified);
                    if (routingVerified) ToastWindow.ShowDiscordStudioTip();
                }
            }
            else
            {
                result = "✗ Setup failed — check your devices and retry.";
                if (!string.IsNullOrWhiteSpace(diagnostics))
                    result += $"\n\n{diagnostics}";
                overlay.Complete("Setup failed — check status.", succeeded: false);
            }
        }
        catch (Exception ex)
        {
            result = $"✗ Setup failed: {ex.Message}";
            overlay.Complete("Setup failed — check the status below.", succeeded: false);
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
        var previousRender = Settings.SavedDefaultRenderId;
        var previousCapture = Settings.SavedDefaultCaptureId;

        if (!string.IsNullOrWhiteSpace(previousRender) || !string.IsNullOrWhiteSpace(previousCapture))
        {
            bool restored = _windowsAudioRoutingService.TrySetDefaultDevices(previousRender ?? "", previousCapture ?? "");
            windowsResult = restored
                ? "✓ Windows defaults restored"
                : "⚠  Could not restore Windows defaults";
            if (restored)
            {
                Settings.SavedDefaultRenderId = string.Empty;
                Settings.SavedDefaultCaptureId = string.Empty;
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

        Settings.HearDeviceName = string.Empty;
        Settings.TalkDeviceName = string.Empty;
        Settings.VoicemeeterDetected = false;
        SaveConfig();

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
            Settings.ShowSetupWizardOnStartup = false;

        Settings.SetupCompleted = true;
        Settings.LastConfigurationDate = DateTime.UtcNow;

        try
        {
            SaveConfig();
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
        Settings.VirtualCableDeviceId = string.Empty;
        Settings.VBCableDetected = false;
        WizardStatusText.Text = "Settings saved.";
    }
}
