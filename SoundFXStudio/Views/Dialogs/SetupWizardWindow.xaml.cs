using SoundFXStudio.Models;
using SoundFXStudio.Services;
using System.Diagnostics;
using System.Windows;

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
    }

    private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            return;
        }

        DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void SetupWizardWindow_Loaded(object sender, RoutedEventArgs e)
    {
        OutputCombo.ItemsSource = _audioDeviceService.GetOutputDevices();
        InputCombo.ItemsSource = _audioDeviceService.GetInputDevices();

        SelectBest(OutputCombo);
        SelectBest(InputCombo);
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
        _config.Settings.SetupCompleted = true;
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

        if (_windowsAudioRoutingService.TrySetDefaultDevices(outputId ?? string.Empty, inputId ?? string.Empty))
        {
            StatusText.Text = "System sound defaults updated for playback and input.";
        }
        else
        {
            StatusText.Text = "Saved app config. System sound defaults could not be updated on this system.";
        }
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

    private void UpdateStatus()
    {
        var output = OutputCombo.SelectedItem as AudioDeviceInfo;
        var input = InputCombo.SelectedItem as AudioDeviceInfo;
        StatusText.Text = $"Output: {output?.Name ?? "none"} | Input: {input?.Name ?? "none"}";
    }
}