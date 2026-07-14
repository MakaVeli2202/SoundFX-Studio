using NAudio.Wave;
using SoundFXStudio.Models;
using SoundFXStudio.Services;
using System.Collections.ObjectModel;
using System.IO;

namespace SoundFXStudio.ViewModels;

public sealed class RoutingViewModel
{
    private readonly Func<AppConfig> _getConfig;
    private readonly Func<AppSettings> _getSettings;
    private readonly ConfigService _configService;
    private readonly AudioPlayer _audioPlayer;
    private readonly ObservableCollection<AudioDeviceInfo> _outputDevices;
    private readonly ObservableCollection<AudioDeviceInfo> _inputDevices;
    private readonly Action<string> _setCurrentOutputDevice;
    private readonly Action<string> _setCurrentInputDevice;
    private readonly Action<string> _setRoutingStatus;
    private readonly Action<string> _setStatusText;
    private readonly Action _save;

    public RoutingViewModel(
        Func<AppConfig> getConfig,
        Func<AppSettings> getSettings,
        ConfigService configService,
        AudioPlayer audioPlayer,
        ObservableCollection<AudioDeviceInfo> outputDevices,
        ObservableCollection<AudioDeviceInfo> inputDevices,
        Action<string> setCurrentOutputDevice,
        Action<string> setCurrentInputDevice,
        Action<string> setRoutingStatus,
        Action<string> setStatusText,
        Action save)
    {
        _getConfig = getConfig;
        _getSettings = getSettings;
        _configService = configService;
        _audioPlayer = audioPlayer;
        _outputDevices = outputDevices;
        _inputDevices = inputDevices;
        _setCurrentOutputDevice = setCurrentOutputDevice;
        _setCurrentInputDevice = setCurrentInputDevice;
        _setRoutingStatus = setRoutingStatus;
        _setStatusText = setStatusText;
        _save = save;
    }

    public void AutoConfigureAudio()
    {
        var output = PickBestDevice(_outputDevices, preferVirtual: false);
        var input = PickBestDevice(_inputDevices, preferVirtual: false);
        if (output is not null)
        {
            _getSettings().OutputDeviceId = output.Id;
            _getSettings().PlaybackDeviceId = output.Id;
        }

        if (input is not null)
        {
            _getSettings().InputDeviceId = input.Id;
            _getSettings().MicrophoneDeviceId = input.Id;
        }

        _getSettings().VirtualCableDeviceId = string.Empty;
        _getSettings().VBCableDetected = false;

        _getSettings().LastConfigurationDate = DateTime.UtcNow;
        _save();
        UpdateRoutingStatus();

        var outputName = output?.Name ?? "no output device";
        var inputName = input?.Name ?? "no input device";
        _setStatusText($"Auto-configured audio: {outputName} / {inputName}");
    }

    public void TestRouting()
    {
        var selectedOutput = _outputDevices.FirstOrDefault(device => string.Equals(device.Id, _getSettings().OutputDeviceId, StringComparison.OrdinalIgnoreCase))
            ?? _outputDevices.FirstOrDefault(device => device.IsDefaultCommunication)
            ?? _outputDevices.FirstOrDefault(device => device.IsDefault)
            ?? _outputDevices.FirstOrDefault();

        if (selectedOutput is null)
        {
            _setStatusText("No output device available for a routing test.");
            return;
        }

        var deviceIndex = _outputDevices.ToList().FindIndex(device => string.Equals(device.Id, selectedOutput.Id, StringComparison.OrdinalIgnoreCase));
        var testTonePath = EnsureRoutingTestTone();

        _audioPlayer.Play("routing-test", testTonePath, 0.8f, false, PlaybackMode.Restart, deviceIndex);
        _setStatusText($"Routing test playing through {selectedOutput.Name}");
        UpdateRoutingStatus();
    }

    public void UpdateRoutingStatus()
    {
        _setCurrentOutputDevice(ResolveDeviceName(_outputDevices, _getSettings().OutputDeviceId));
        _setCurrentInputDevice(ResolveDeviceName(_inputDevices, _getSettings().InputDeviceId));
        var routingParts = new List<string>
        {
            $"Output: {ResolveDeviceName(_outputDevices, _getSettings().OutputDeviceId)}",
            $"Input: {ResolveDeviceName(_inputDevices, _getSettings().InputDeviceId)}"
        };

        _setRoutingStatus(_getSettings().VBCableDetected
            ? $"Ready · {string.Join(" · ", routingParts)}"
            : $"Needs setup · {string.Join(" · ", routingParts)}");
    }

    private string EnsureRoutingTestTone()
    {
        var path = Path.Combine(_configService.GetAppFolder(), "routing-test-tone.wav");
        if (File.Exists(path))
        {
            return path;
        }

        const int sampleRate = 44100;
        const int durationMs = 900;
        const double frequency = 440.0;
        const float amplitude = 0.25f;

        using var writer = new WaveFileWriter(path, WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1));
        var sampleCount = sampleRate * durationMs / 1000;

        for (var n = 0; n < sampleCount; n++)
        {
            var sample = (float)(amplitude * Math.Sin(2.0 * Math.PI * frequency * n / sampleRate));
            writer.WriteSample(sample);
        }

        return path;
    }

    private static AudioDeviceInfo? PickBestDevice(IEnumerable<AudioDeviceInfo> devices, bool preferVirtual)
    {
        var list = devices.ToList();
        if (list.Count == 0)
        {
            return null;
        }

        var byDefault = list.FirstOrDefault(device => device.IsDefaultCommunication) ?? list.FirstOrDefault(device => device.IsDefault);
        if (byDefault is not null)
        {
            return byDefault;
        }

        var preferred = preferVirtual
            ? list.FirstOrDefault(device => device.IsVirtual)
            : list.FirstOrDefault(device => !device.IsVirtual);

        return preferred ?? list.First();
    }

    private static string ResolveDeviceName(IEnumerable<AudioDeviceInfo> devices, string deviceId, string fallback = "System Default")
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return fallback;
        }

        var device = devices.FirstOrDefault(item => string.Equals(item.Id, deviceId, StringComparison.OrdinalIgnoreCase));
        return device?.Name ?? fallback;
    }
}
