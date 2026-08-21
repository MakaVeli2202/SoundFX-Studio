using System.Net;
using System.Net.Sockets;
using System.Text;
using SoundFXStudio.Services.Hrtf;
using Xunit;

namespace SoundFXStudio.Tests;

public class OpenTrackHeadTrackingProviderTests : IDisposable
{
    private readonly List<OpenTrackHeadTrackingProvider> _providers = new();
    private readonly List<UdpClient> _senders = new();

    private OpenTrackHeadTrackingProvider CreateProvider(int port = 0)
    {
        var opts = new OpenTrackHeadTrackingOptions
        {
            Port = port > 0 ? port : GetAvailablePort(),
            BindAddress = "127.0.0.1"
        };
        var provider = new OpenTrackHeadTrackingProvider(opts);
        _providers.Add(provider);
        return provider;
    }

    private int GetAvailablePort()
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private UdpClient CreateSender()
    {
        var sender = new UdpClient();
        _senders.Add(sender);
        return sender;
    }

    private static byte[] BuildPacket(double yaw, double pitch, double roll)
    {
        var data = new byte[48];
        BitConverter.GetBytes(yaw).CopyTo(data, 0);
        BitConverter.GetBytes(pitch).CopyTo(data, 8);
        BitConverter.GetBytes(roll).CopyTo(data, 16);
        // X, Y, Z position (cm) — leave as zeros
        return data;
    }

    // ── Options tests ────────────────────────────────────────────────────

    [Fact]
    public void Options_DefaultValues()
    {
        var opts = new OpenTrackHeadTrackingOptions();
        Assert.Equal(4242, opts.Port);
        Assert.Equal("127.0.0.1", opts.BindAddress);
        Assert.False(opts.AutoStart);
    }

    [Fact]
    public void Options_PortValidation()
    {
        var opts = new OpenTrackHeadTrackingOptions();
        Assert.Throws<ArgumentOutOfRangeException>(() => opts.Port = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => opts.Port = 70000);
        opts.Port = 1;
        Assert.Equal(1, opts.Port);
        opts.Port = 65535;
        Assert.Equal(65535, opts.Port);
    }

    [Fact]
    public void Options_BindAddressValidation()
    {
        var opts = new OpenTrackHeadTrackingOptions();
        Assert.Throws<ArgumentException>(() => opts.BindAddress = "");
        Assert.Throws<ArgumentException>(() => opts.BindAddress = "not-an-ip");
        opts.BindAddress = "192.168.1.1";
        Assert.Equal("192.168.1.1", opts.BindAddress);
    }

    // ── Provider lifecycle tests ─────────────────────────────────────────

    [Fact]
    public void Provider_NameIsOpenTrack()
    {
        var provider = CreateProvider();
        Assert.Equal("OpenTrack UDP", provider.ProviderName);
    }

    [Fact]
    public void Provider_AvailableBeforeStart()
    {
        var provider = CreateProvider();
        Assert.True(provider.IsAvailable);
        Assert.False(provider.IsTracking);
    }

    [Fact]
    public void Provider_StartReturnsTrue()
    {
        var provider = CreateProvider();
        Assert.True(provider.Start());
        Assert.True(provider.IsTracking);
    }

    [Fact]
    public void Provider_StopIsIdempotent()
    {
        var provider = CreateProvider();
        provider.Start();
        provider.Stop();
        provider.Stop(); // Should not throw
        Assert.False(provider.IsTracking);
    }

    [Fact]
    public void Provider_StartIsIdempotent()
    {
        var provider = CreateProvider();
        Assert.True(provider.Start());
        Assert.True(provider.Start()); // Should not throw or rebind
        Assert.True(provider.IsTracking);
    }

    [Fact]
    public void Provider_DisposePreventsRestart()
    {
        var provider = CreateProvider();
        provider.Dispose();
        Assert.False(provider.IsAvailable);
        Assert.False(provider.Start());
    }

    // ── Packet parsing tests ─────────────────────────────────────────────

    [Fact]
    public void ParsePacket_ValidData()
    {
        var data = BuildPacket(45.0, -10.5, 0.0);
        var (yaw, pitch, roll, valid) = OpenTrackHeadTrackingProvider.ParsePacketData(data);
        Assert.True(valid);
        Assert.Equal(45.0, yaw, 3);
        Assert.Equal(-10.5, pitch, 3);
        Assert.Equal(0.0, roll, 3);
    }

    [Fact]
    public void ParsePacket_NegativeAngles()
    {
        var data = BuildPacket(-179.9, -89.9, -180.0);
        var (yaw, pitch, roll, valid) = OpenTrackHeadTrackingProvider.ParsePacketData(data);
        Assert.True(valid);
        Assert.Equal(-179.9, yaw, 3);
        Assert.Equal(-89.9, pitch, 3);
        Assert.Equal(-180.0, roll, 3);
    }

    [Fact]
    public void ParsePacket_ZeroLength_ReturnsInvalid()
    {
        var result = OpenTrackHeadTrackingProvider.ParsePacketData(Array.Empty<byte>());
        Assert.False(result.Valid);
    }

    [Fact]
    public void ParsePacket_TooShort_ReturnsInvalid()
    {
        var result = OpenTrackHeadTrackingProvider.ParsePacketData(new byte[47]);
        Assert.False(result.Valid);
    }

    [Fact]
    public void ParsePacket_NaN_ReturnsInvalid()
    {
        var data = BuildPacket(double.NaN, 0, 0);
        var result = OpenTrackHeadTrackingProvider.ParsePacketData(data);
        Assert.False(result.Valid);
    }

    [Fact]
    public void ParsePacket_Infinity_ReturnsInvalid()
    {
        var data = BuildPacket(double.PositiveInfinity, 0, 0);
        var result = OpenTrackHeadTrackingProvider.ParsePacketData(data);
        Assert.False(result.Valid);
    }

    [Fact]
    public void ParsePacket_PitchNaN_ReturnsInvalid()
    {
        var data = BuildPacket(0, double.NaN, 0);
        var result = OpenTrackHeadTrackingProvider.ParsePacketData(data);
        Assert.False(result.Valid);
    }

    [Fact]
    public void ParsePacket_RollInfinity_ReturnsInvalid()
    {
        var data = BuildPacket(0, 0, double.NegativeInfinity);
        var result = OpenTrackHeadTrackingProvider.ParsePacketData(data);
        Assert.False(result.Valid);
    }

    // ── End-to-end UDP receive tests ─────────────────────────────────────

    [Fact]
    public async Task Provider_ReceivesPacket()
    {
        var port = GetAvailablePort();
        var provider = CreateProvider(port);
        var sender = CreateSender();

        provider.Start();
        await Task.Delay(50); // Let UDP bind

        var packet = BuildPacket(30.0, -15.0, 5.0);
        await sender.SendAsync(packet, new IPEndPoint(IPAddress.Loopback, port));
        await Task.Delay(100); // Let packet arrive

        var orientation = provider.GetOrientation();
        Assert.Equal(30.0, orientation.YawDeg, 1);
        Assert.Equal(-15.0, orientation.PitchDeg, 1);
        Assert.Equal(5.0, orientation.RollDeg, 1);
        Assert.True(provider.HasReceivedPacket);
    }

    [Fact]
    public async Task Provider_ReceivesMultiplePackets()
    {
        var port = GetAvailablePort();
        var provider = CreateProvider(port);
        var sender = CreateSender();

        provider.Start();
        await Task.Delay(50);

        // Send three packets
        for (int i = 1; i <= 3; i++)
        {
            var packet = BuildPacket(i * 10.0, i * -5.0, 0);
            await sender.SendAsync(packet, new IPEndPoint(IPAddress.Loopback, port));
            await Task.Delay(50);
        }

        var orientation = provider.GetOrientation();
        Assert.Equal(30.0, orientation.YawDeg, 1);
        Assert.Equal(-15.0, orientation.PitchDeg, 1);
    }

    [Fact]
    public async Task Provider_IgnoresCorruptedPacket()
    {
        var port = GetAvailablePort();
        var provider = CreateProvider(port);
        var sender = CreateSender();

        provider.Start();
        await Task.Delay(50);

        // Send valid first
        var good = BuildPacket(10.0, 5.0, 0);
        await sender.SendAsync(good, new IPEndPoint(IPAddress.Loopback, port));
        await Task.Delay(80);

        var before = provider.GetOrientation();
        Assert.Equal(10.0, before.YawDeg, 1);

        // Send corrupted (NaN in yaw)
        var bad = BuildPacket(double.NaN, 5.0, 0);
        await sender.SendAsync(bad, new IPEndPoint(IPAddress.Loopback, port));
        await Task.Delay(80);

        // Should still be the old values
        var after = provider.GetOrientation();
        Assert.Equal(10.0, after.YawDeg, 1);
        Assert.Equal(5.0, after.PitchDeg, 1);
    }

    [Fact]
    public async Task Provider_IgnoresWrongLengthPacket()
    {
        var port = GetAvailablePort();
        var provider = CreateProvider(port);
        var sender = CreateSender();

        provider.Start();
        await Task.Delay(50);

        // Send valid first
        var good = BuildPacket(20.0, 10.0, 0);
        await sender.SendAsync(good, new IPEndPoint(IPAddress.Loopback, port));
        await Task.Delay(80);

        // Send wrong length (too short)
        var bad = new byte[32];
        BitConverter.GetBytes(99.0).CopyTo(bad, 0);
        await sender.SendAsync(bad, new IPEndPoint(IPAddress.Loopback, port));
        await Task.Delay(80);

        var after = provider.GetOrientation();
        Assert.Equal(20.0, after.YawDeg, 1);
    }

    [Fact]
    public void Provider_DefaultOrientationIsZero()
    {
        var provider = CreateProvider();
        var orientation = provider.GetOrientation();
        Assert.Equal(0.0, orientation.YawDeg);
        Assert.Equal(0.0, orientation.PitchDeg);
        Assert.Equal(0.0, orientation.RollDeg);
    }

    // ── Factory tests ────────────────────────────────────────────────────

    [Fact]
    public void Factory_CreatesOpenTrack()
    {
        var factory = new HeadTrackingProviderFactory();
        var provider = factory.Create("opentrack");
        Assert.IsType<OpenTrackHeadTrackingProvider>(provider);
        provider.Dispose();
    }

    [Fact]
    public void Factory_CreatesNull()
    {
        var factory = new HeadTrackingProviderFactory();
        var provider = factory.Create("none");
        Assert.IsType<NullHeadTrackingProvider>(provider);
        provider.Dispose();
    }

    [Fact]
    public void Factory_FallbackForUnknown()
    {
        var factory = new HeadTrackingProviderFactory();
        var provider = factory.Create("webcam");
        Assert.IsType<NullHeadTrackingProvider>(provider);
        provider.Dispose();
    }

    [Fact]
    public void Factory_CaseInsensitive()
    {
        var factory = new HeadTrackingProviderFactory();
        var provider = factory.Create("OpenTrack");
        Assert.IsType<OpenTrackHeadTrackingProvider>(provider);
        provider.Dispose();
    }

    // ── Thread safety tests ──────────────────────────────────────────────

    [Fact]
    public async Task ConcurrentReadWrite_Orientation()
    {
        var port = GetAvailablePort();
        var provider = CreateProvider(port);
        var sender = CreateSender();

        provider.Start();
        await Task.Delay(30);

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        int readCount = 0;
        int writeCount = 0;

        var reader = Task.Run(() =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                var _ = provider.GetOrientation();
                Interlocked.Increment(ref readCount);
            }
        });

        var writer = Task.Run(async () =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                var packet = BuildPacket(
                    Random.Shared.NextDouble() * 360 - 180,
                    Random.Shared.NextDouble() * 180 - 90,
                    0);
                await sender.SendAsync(packet, new IPEndPoint(IPAddress.Loopback, port));
                Interlocked.Increment(ref writeCount);
                await Task.Delay(1);
            }
        });

        await Task.WhenAll(reader, writer);
        Assert.True(readCount > 100);
        Assert.True(writeCount > 10);
    }

    // ── Performance tests ────────────────────────────────────────────────

    [Fact]
    public void ParsePacket_Benchmark()
    {
        var data = BuildPacket(45.0, -10.5, 0.0);
        var sw = System.Diagnostics.Stopwatch.StartNew();

        const int iterations = 100_000;
        for (int i = 0; i < iterations; i++)
        {
            OpenTrackHeadTrackingProvider.ParsePacketData(data);
        }

        sw.Stop();
        var usPerCall = sw.Elapsed.TotalMilliseconds * 1000.0 / iterations;
        // Should be under 1µs per parse
        Assert.True(usPerCall < 1.0, $"Parse took {usPerCall:F3}µs per call, expected < 1µs");
    }

    [Fact]
    public void GetOrientation_Benchmark()
    {
        var provider = CreateProvider();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        const int iterations = 100_000;
        for (int i = 0; i < iterations; i++)
        {
            provider.GetOrientation();
        }

        sw.Stop();
        var nsPerCall = sw.Elapsed.TotalMilliseconds * 1_000_000.0 / iterations;
        // Should be under 100ns per call
        Assert.True(nsPerCall < 100, $"GetOrientation took {nsPerCall:F1}ns per call, expected < 100ns");
    }

    public void Dispose()
    {
        foreach (var p in _providers)
        {
            try { p.Dispose(); } catch { }
        }
        foreach (var s in _senders)
        {
            try { s.Close(); } catch { }
        }
    }
}
