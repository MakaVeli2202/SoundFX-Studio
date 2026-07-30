using NAudio.Wave;
using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace SoundFXStudio.Views.Dialogs;

public partial class MicrophoneRecorderDialog : Window
{
    private WaveInEvent? _waveIn;
    private MemoryStream? _audioStream;
    private WaveFileWriter? _writer;
    private System.Timers.Timer? _timer;
    private int _seconds;
    private bool _isRecording;
    private string _tempFile = string.Empty;

    public MicrophoneRecorderDialog()
    {
        InitializeComponent();
    }

    public string RecordedFilePath => _tempFile;
    public string SoundName { get; private set; } = string.Empty;

    private void RecordButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isRecording)
        {
            StopRecording();
        }
        else
        {
            StartRecording();
        }
    }

    private void StartRecording()
    {
        try
        {
            _audioStream = new MemoryStream();
            _writer = new WaveFileWriter(_audioStream, new WaveFormat(44100, 16, 1));

            _waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(44100, 16, 1),
                BufferMilliseconds = 100
            };
            _waveIn.DataAvailable += OnDataAvailable;
            _waveIn.RecordingStopped += OnRecordingStopped;
            _waveIn.StartRecording();

            _isRecording = true;
            _seconds = 0;
            RecordButton.Content = "\u25A0 Stop";
            RecordButton.Background = new SolidColorBrush(Color.FromRgb(0x80, 0x20, 0x20));
            StatusText.Text = "Recording...";
            StatusIcon.Text = "\u25CF";
            StatusCircle.BorderBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x44, 0x4D));
            TimerText.Text = "00:00";

            _timer = new System.Timers.Timer(1000);
            _timer.Elapsed += (_, _) =>
            {
                _seconds++;
                Dispatcher.Invoke(() => TimerText.Text = $"{_seconds / 60:D2}:{_seconds % 60:D2}");
            };
            _timer.AutoReset = true;
            _timer.Start();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not access microphone: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Cleanup();
        }
    }

    private void StopRecording()
    {
        try { _waveIn?.StopRecording(); } catch { }
        _timer?.Stop();
        _timer?.Dispose();
        _timer = null;
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        _writer?.Write(e.Buffer, 0, e.BytesRecorded);
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        _writer?.Flush();
        _writer?.Dispose();
        _writer = null;

        _waveIn?.Dispose();
        _waveIn = null;

        if (_audioStream is not null)
        {
            try
            {
                _tempFile = Path.Combine(Path.GetTempPath(), $"mic_{Guid.NewGuid():N}.wav");
                File.WriteAllBytes(_tempFile, _audioStream.ToArray());
            }
            catch { }
            _audioStream.Dispose();
            _audioStream = null;
        }

        _isRecording = false;
        Dispatcher.Invoke(() =>
        {
            RecordButton.Visibility = Visibility.Collapsed;
            SaveButton.Visibility = Visibility.Visible;
            NameBox.Visibility = Visibility.Visible;
            StatusText.Text = "Recording finished. Name your sound:";
            StatusIcon.Text = "\u2714";
            StatusCircle.BorderBrush = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
        });
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var name = NameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            name = $"Mic Recording {DateTime.Now:yyyy-MM-dd HH-mm}";
        }
        SoundName = name;
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Cleanup();
        DialogResult = false;
        Close();
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is System.Windows.Shapes.Path or System.Windows.Controls.Canvas)
            return;
        if (e.ClickCount == 2)
            return;
        DragMove();
    }

    private void Cleanup()
    {
        _timer?.Stop();
        _timer?.Dispose();
        _timer = null;
        try { _waveIn?.StopRecording(); } catch { }
        _waveIn?.Dispose();
        _waveIn = null;
        _writer?.Dispose();
        _writer = null;
        _audioStream?.Dispose();
        _audioStream = null;
        if (File.Exists(_tempFile))
        {
            try { File.Delete(_tempFile); } catch { }
        }
    }
}
