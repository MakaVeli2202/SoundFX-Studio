using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using SoundFXStudio.Models;
using SoundFXStudio.Services;

namespace SoundFXStudio.Views.Dialogs;

public partial class TeamMonitorWindow : Window
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int a, ref int v, int s);

    private readonly TeamMonitorService _monitor = new();
    private readonly AudioDeviceService _audioDeviceService = new();

    private sealed class ModeOption
    {
        public TeamMonitorService.SimMode Mode { get; init; }
        public string Label { get; init; } = "";
        public override string ToString() => Label;
    }

    public TeamMonitorWindow()
    {
        InitializeComponent();
        Loaded += TeamMonitorWindow_Loaded;
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
    }

    private void Window_SourceInitialized(object sender, EventArgs e)
    {
        var h = new WindowInteropHelper(this).Handle;
        int p = 2; DwmSetWindowAttribute(h, 33, ref p, Marshal.SizeOf<int>());
    }

    private void TeamMonitorWindow_Loaded(object sender, RoutedEventArgs e)
    {
        ModeCombo.ItemsSource = new[]
        {
            new ModeOption { Mode = TeamMonitorService.SimMode.None, Label = "Raw mix (what your mic sends)" },
            new ModeOption { Mode = TeamMonitorService.SimMode.Opus64Mono, Label = "Discord ~64 kbps mono (default voice)" },
            new ModeOption { Mode = TeamMonitorService.SimMode.Opus128Stereo, Label = "Discord ~128 kbps stereo" },
        };
        ModeCombo.SelectedIndex = 0;

        PopulateDevices();
        DevicesText.Text = BuildDevicesLine();

        var config = new ConfigService().Load();
        var isB1Source = (CaptureCombo.SelectedItem as AudioDeviceInfo)?.Name?.Contains("B1", StringComparison.OrdinalIgnoreCase) == true;

        if (!config.Settings.SetupCompleted)
        {
            StartBtn.IsEnabled = false;
            MonitorStatus.Foreground = new SolidColorBrush(Color.FromRgb(0xF4, 0x3F, 0x5E));
            MonitorStatus.Text = "✗ VoiceMeeter setup hasn't been run yet.\nClose this window, run Setup, then reopen.";
        }
        else if (!isB1Source)
        {
            StartBtn.IsEnabled = false;
            MonitorStatus.Foreground = new SolidColorBrush(Color.FromRgb(0xF4, 0x3F, 0x5E));
            MonitorStatus.Text = "✗ VoiceMeeter B1 monitor source not found.\nInstall/launch Voicemeeter, run Setup, then reopen.";
        }
        else
        {
            StartBtn.IsEnabled = true;
            MonitorStatus.Text = "Press Start to hear what teammates hear.";
        }
    }

    private void PopulateDevices()
    {
        var captures = _audioDeviceService.GetAllInputDevices().ToList();
        var playbacks = _audioDeviceService.GetAllOutputDevices().ToList();

        CaptureCombo.ItemsSource = captures;
        PlaybackCombo.ItemsSource = playbacks;

        CaptureCombo.SelectedItem = captures.FirstOrDefault(d =>
            d.Name.Contains("Voicemeeter", StringComparison.OrdinalIgnoreCase)
            && d.Name.Contains("B1", StringComparison.OrdinalIgnoreCase)
            && !d.Name.Contains("Aux", StringComparison.OrdinalIgnoreCase)
            && !d.Name.Contains("Virtual", StringComparison.OrdinalIgnoreCase));

        var config = new ConfigService().Load();
        var saved = !string.IsNullOrWhiteSpace(config.Settings.HearDeviceName)
            ? config.Settings.HearDeviceName
            : config.Settings.SpeakersDeviceName;
        PlaybackCombo.SelectedItem = playbacks.FirstOrDefault(d => d.Name == saved)
            ?? playbacks.FirstOrDefault(d => d.IsDefaultCommunication)
            ?? playbacks.FirstOrDefault(d => d.IsDefault)
            ?? playbacks.FirstOrDefault();
    }

    private string BuildDevicesLine()
    {
        var capName = (CaptureCombo.SelectedItem as AudioDeviceInfo)?.Name ?? "none";
        var outName = (PlaybackCombo.SelectedItem as AudioDeviceInfo)?.Name ?? "none";
        return $"Capture: {capName}\nPlaying through: {outName}";
    }

    private void StartStop_Click(object sender, RoutedEventArgs e)
    {
        if (_monitor.IsRunning)
        {
            StopMonitor();
            return;
        }

        var capture = CaptureCombo.SelectedItem as AudioDeviceInfo;
        if (capture is null)
        {
            MonitorStatus.Text = "✗ No monitor source selected.";
            MonitorStatus.Foreground = new SolidColorBrush(Color.FromRgb(0xF4, 0x3F, 0x5E));
            return;
        }

        var playback = PlaybackCombo.SelectedItem as AudioDeviceInfo;
        if (playback is null)
        {
            MonitorStatus.Text = "✗ No playback device selected.";
            MonitorStatus.Foreground = new SolidColorBrush(Color.FromRgb(0xF4, 0x3F, 0x5E));
            return;
        }

        int captureIndex = TeamMonitorService.ResolveWaveInIndex(capture.Name);
        int playbackIndex = TeamMonitorService.ResolveWaveOutIndex(playback.Name);
        if (captureIndex < 0 || playbackIndex < 0)
        {
            MonitorStatus.Text = "✗ Selected device is not available to the mixer. Pick another from the lists.";
            MonitorStatus.Foreground = new SolidColorBrush(Color.FromRgb(0xF4, 0x3F, 0x5E));
            return;
        }

        var mode = (ModeCombo.SelectedItem as ModeOption)?.Mode ?? TeamMonitorService.SimMode.None;

        bool ok = _monitor.Start(captureIndex, playbackIndex, mode);
        if (!ok)
        {
            MonitorStatus.Text = $"✗ Could not start monitor: {_monitor.LastError}";
            MonitorStatus.Foreground = new SolidColorBrush(Color.FromRgb(0xF4, 0x3F, 0x5E));
            return;
        }

        _monitor.SetVolume((float)VolumeSlider.Value);

        StartBtn.Content = "\u25A0 Stop";
        MonitorStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
        MonitorStatus.Text = "Listening... this is exactly what your mic sends to Discord."
            + (mode != TeamMonitorService.SimMode.None
                ? "\n(Opus simulation on — add headphones + press Stop when done.)"
                : "\n(Press Stop when done.)");
        DevicesText.Text = BuildDevicesLine();
    }

    private void ModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || !_monitor.IsRunning) return;
        StopMonitor();
        StartStop_Click(sender, e);
    }

    private void StopMonitor()
    {
        _monitor.Stop();
        StartBtn.Content = "Start";
        MonitorStatus.Text = "Stopped.";
        MonitorStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x98, 0xA0, 0xC0));
    }

    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_monitor.IsRunning)
            _monitor.SetVolume((float)e.NewValue);
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();
    private void Done_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_Closed(object sender, EventArgs e)
    {
        _monitor.Dispose();
    }
}
