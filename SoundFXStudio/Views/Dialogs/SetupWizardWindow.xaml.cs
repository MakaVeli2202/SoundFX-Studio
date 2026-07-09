using SoundFXStudio.Models;
using SoundFXStudio.Services;
using System.Diagnostics;
using System.Windows;

namespace SoundFXStudio.Views.Dialogs;

public partial class SetupWizardWindow : Window
{
    private readonly ConfigService _configService = new();
    private readonly AudioDeviceService _audioDeviceService = new();
    private AppConfig _config;

    public SetupWizardWindow()
    {
        InitializeComponent();
        _config = _configService.Load();
        Loaded += SetupWizardWindow_Loaded;
    }

    private void SetupWizardWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Check if Voicemeeter is installed
        if (!VoicemeeterService.IsVoicemeeterInstalled())
        {
            var result = MessageBox.Show(
                "Voicemeeter is required for audio routing and virtual cable support.\n\n" +
                "Would you like to download Voicemeeter now?\n\n" +
                "SoundFX Studio works best with Voicemeeter installed.",
                "Voicemeeter Not Found",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    Process.Start(new ProcessStartInfo("https://vb-audio.com/Voicemeeter/") { UseShellExecute = true });
                }
                catch { }
            }
        }

        OutputCombo.ItemsSource = _audioDeviceService.GetOutputDevices();
        InputCombo.ItemsSource = _audioDeviceService.GetInputDevices();
        CableCombo.ItemsSource = _audioDeviceService.GetOutputDevices().Concat(_audioDeviceService.GetInputDevices()).ToList();

        SelectBest(OutputCombo);
        SelectBest(InputCombo);
        SelectCable();
        UpdateStatus();
    }

    private void AutoConfigure_Click(object sender, RoutedEventArgs e)
    {
        ApplySelection();
        StatusText.Text = "Auto-configured and saved. You can fine-tune the devices here before finishing.";
    }

    private void Finish_Click(object sender, RoutedEventArgs e)
    {
        ApplySelection();
        _config.Settings.LastConfigurationDate = DateTime.UtcNow;
        _configService.Save(_config);
        DialogResult = true;
    }

    private void OpenSound_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("control", "mmsys.cpl,,1") { UseShellExecute = true });
        }
        catch
        {
            StatusText.Text = "Could not open Windows Sound settings.";
        }
    }

    private void ApplySelection()
    {
        if (OutputCombo.SelectedItem is AudioDeviceInfo output)
        {
            _config.Settings.OutputDeviceId = output.Id;
            _config.Settings.PlaybackDeviceId = output.Id;
        }

        if (InputCombo.SelectedItem is AudioDeviceInfo input)
        {
            _config.Settings.InputDeviceId = input.Id;
            _config.Settings.MicrophoneDeviceId = input.Id;
        }

        var cable = CableCombo.SelectedItem as AudioDeviceInfo;
        _config.Settings.VirtualCableDeviceId = cable?.Id ?? string.Empty;
        _config.Settings.VBCableDetected = cable is not null && _audioDeviceService.IsVBCableDevice(cable.Name);
    }

    private void SelectBest(System.Windows.Controls.ComboBox combo)
    {
        foreach (var item in combo.Items.OfType<AudioDeviceInfo>())
        {
            if (item.IsDefaultCommunication || item.IsDefault)
            {
                combo.SelectedItem = item;
                return;
            }
        }

        if (combo.Items.Count > 0)
        {
            combo.SelectedIndex = 0;
        }
    }

    private void SelectCable()
    {
        foreach (var item in CableCombo.Items.OfType<AudioDeviceInfo>())
        {
            if (_audioDeviceService.IsVBCableDevice(item.Name))
            {
                CableCombo.SelectedItem = item;
                return;
            }
        }

        if (CableCombo.Items.Count > 0)
        {
            CableCombo.SelectedIndex = 0;
        }
    }

    private void UpdateStatus()
    {
        var output = OutputCombo.SelectedItem as AudioDeviceInfo;
        var input = InputCombo.SelectedItem as AudioDeviceInfo;
        var cable = CableCombo.SelectedItem as AudioDeviceInfo;

        StatusText.Text = $"Output: {output?.Name ?? "none"} | Input: {input?.Name ?? "none"} | Cable: {cable?.Name ?? "none"}";
    }
}