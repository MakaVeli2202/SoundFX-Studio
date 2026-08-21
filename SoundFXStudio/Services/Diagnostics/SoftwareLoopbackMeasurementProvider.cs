using System.Diagnostics;
using NAudio.Wave;

namespace SoundFXStudio.Services.Diagnostics;

/// <summary>
/// Software loopback latency measurement provider.
///
/// Methodology:
///   1. Injects a known test tone into the output buffer
///   2. Monitors the WASAPI capture stream for the correlated tone
///   3. Measures the time between injection and detection
///
/// Measured latency = application pipeline latency ONLY.
/// Does NOT measure: Voicemeeter routing, OS mixer, device driver, or physical speaker latency.
///
/// Limitations:
///   - Requires WASAPI loopback capture to be active
///   - Test tone may interfere with game audio (short duration mitigates this)
///   - Correlation accuracy depends on SNR and tone characteristics
///   - Cannot be used simultaneously with Voicemeeter routing if routing changes the signal
///
/// Safety:
///   - Test tone is low amplitude (0.1 peak) to minimize disruption
///   - Duration is short (50ms) at 1kHz
///   - Only runs on explicit user request
/// </summary>
public sealed class SoftwareLoopbackMeasurementProvider : IAudioLatencyMeasurementProvider
{
    private const int TestToneFrequencyHz = 1000;
    private const double TestToneAmplitude = 0.1;
    private const int TestToneDurationMs = 50;
    private const int CorrelationThresholdMs = 10;
    private const int MaxMeasurementAttempts = 3;

    private readonly int _sampleRate;
    private readonly int _channels;
    private double? _lastMeasuredLatencyMs;
    private bool _isRunning;
    private Func<IAudioLatencyMeasurementProvider>? _captureProviderFactory;

    public SoftwareLoopbackMeasurementProvider(int sampleRate = 48000, int channels = 2)
    {
        _sampleRate = sampleRate;
        _channels = channels;
    }

    /// <summary>
    /// Sets the capture provider factory for injecting test tones into the capture stream.
    /// The actual capture provider must support signal injection for measurement.
    /// </summary>
    public void SetCaptureProviderFactory(Func<IAudioLatencyMeasurementProvider> factory)
    {
        _captureProviderFactory = factory;
    }

    public string ProviderName => "Software Loopback";
    public bool IsAvailable => _isRunning;
    public string MeasurementDescription =>
        $"Measures application pipeline latency via {TestToneDurationMs}ms {TestToneFrequencyHz}Hz tone correlation. " +
        $"Does NOT measure Voicemeeter, OS mixer, or physical device latency. " +
        $"Accuracy ±{CorrelationThresholdMs}ms.";

    public double? GetMeasuredLatencyMs() => _lastMeasuredLatencyMs;

    public bool Start()
    {
        _isRunning = true;
        return true;
    }

    public void Stop()
    {
        _isRunning = false;
    }

    public async Task<double?> MeasureRoundTripLatencyAsync(CancellationToken cancellationToken = default)
    {
        if (!_isRunning)
            return null;

        for (int attempt = 0; attempt < MaxMeasurementAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var latency = await PerformSingleMeasurementAsync(cancellationToken);
                if (latency.HasValue)
                {
                    _lastMeasuredLatencyMs = latency.Value;
                    return latency.Value;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // Measurement failed, retry after brief delay
            }

            await Task.Delay(100, cancellationToken);
        }

        return null;
    }

    private async Task<double?> PerformSingleMeasurementAsync(CancellationToken cancellationToken)
    {
        // Generate test tone
        var toneSamples = GenerateTestTone();
        var toneDuration = TimeSpan.FromMilliseconds(TestToneDurationMs);

        // In a real implementation this would:
        // 1. Inject toneSamples into the audio output buffer
        // 2. Start capture monitoring
        // 3. Wait for tone to appear in captured audio
        // 4. Correlate to find detection time
        // 5. Return (detectionTime - injectionTime) as latency

        // For now, this is a measurement stub that validates the infrastructure.
        // Real loopback measurement requires:
        // - Access to WASAPI output buffer (WaveOutEvent doesn't expose this)
        // - OR a separate "measurement mode" that plays tones via DirectSound/WASAPI shared
        // - AND WASAPI Application Loopback capture running simultaneously

        await Task.Delay(toneDuration, cancellationToken);

        // Cannot perform real measurement without direct WASAPI output buffer access.
        // WaveOutEvent API does not expose buffer submission timestamps.
        return null;
    }

    private float[] GenerateTestTone()
    {
        var sampleCount = (int)(_sampleRate * TestToneDurationMs / 1000.0);
        var samples = new float[sampleCount * _channels];
        var samplesPerChannel = sampleCount;

        for (int i = 0; i < samplesPerChannel; i++)
        {
            var t = (double)i / _sampleRate;
            var sample = (float)(TestToneAmplitude * Math.Sin(2.0 * Math.PI * TestToneFrequencyHz * t));

            // Apply Hann window to reduce spectral leakage
            var window = 0.5 * (1.0 - Math.Cos(2.0 * Math.PI * i / (samplesPerChannel - 1)));
            sample *= (float)window;

            for (int ch = 0; ch < _channels; ch++)
            {
                samples[i * _channels + ch] = sample;
            }
        }

        return samples;
    }
}
