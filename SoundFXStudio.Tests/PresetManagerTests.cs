using SoundFXStudio.Models;
using SoundFXStudio.Services;
using SoundFXStudio.Services.DSP;
using Xunit;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SoundFXStudio.Tests;

public class PresetManagerTests
{
    [Fact]
    public void Presets_NonEmpty_UniqueIds()
    {
        Assert.NotEmpty(PresetManager.Presets);
        Assert.Equal(PresetManager.Presets.Count, PresetManager.Presets.Select(p => p.Id).Distinct().Count());
    }

    [Fact]
    public void GetById_UnknownId_ReturnsNormal()
    {
        var preset = PresetManager.GetById("does-not-exist");
        Assert.Equal("normal", preset.Id);
    }

    [Fact]
    public void Apply_CaveEcho_SetsReverbAndPitch()
    {
        var service = new VoiceChangerService();
        var preset = PresetManager.GetById("caveecho");

        PresetManager.Apply(preset, service);

        Assert.Equal(1f, service.PitchSemitones);
        Assert.Equal(1f, service.FormantShift);
        Assert.True(service.Chain.Get<ReverbEffect>()!.IsEnabled);
        Assert.Equal(0.45f, service.Chain.Get<ReverbEffect>()!.Mix);
        Assert.Equal(0.85f, service.Chain.Get<ReverbEffect>()!.RoomSize);
        Assert.False(service.Chain.Get<RobotEffect>()!.IsEnabled);
    }

    [Fact]
    public void Apply_Chipmunk_DisablesExtraEffects()
    {
        var service = new VoiceChangerService();
        var preset = PresetManager.GetById("chipmunk");

        PresetManager.Apply(preset, service);

        Assert.Equal(8f, service.PitchSemitones);
        Assert.Equal(1.45f, service.FormantShift);
        Assert.False(service.Chain.Get<ReverbEffect>()!.IsEnabled);
        Assert.False(service.Chain.Get<RobotEffect>()!.IsEnabled);
        Assert.False(service.Chain.Get<DistortionEffect>()!.IsEnabled);
        Assert.True(service.Chain.Get<LimiterEffect>()!.IsEnabled);
    }

    [Fact]
    public void Apply_Demon_EnablesDistortion()
    {
        var service = new VoiceChangerService();
        var preset = PresetManager.GetById("demon");

        PresetManager.Apply(preset, service);

        Assert.True(service.Chain.Get<DistortionEffect>()!.IsEnabled);
        Assert.Equal(5f, service.Chain.Get<DistortionEffect>()!.Drive);
        Assert.Equal(-5f, service.PitchSemitones);
        Assert.Equal(0.7f, service.FormantShift);
    }

    [Fact]
    public void ConfigService_PreservesVoiceChangerSettings()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "SoundFXStudio-Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var service = new ConfigService(appFolder: tempDir);
            var config = new AppConfig();
            config.Settings.VoiceChangerPresetId = "demon";
            config.Settings.FormantShift = 1.25f;
            config.Settings.PitchShift = -2f;
            config.Settings.VoiceChangerToggleKey = "F9";

            service.Save(config);
            var loaded = service.Load();

            Assert.Equal("demon", loaded.Settings.VoiceChangerPresetId);
            Assert.Equal(1.25f, loaded.Settings.FormantShift);
            Assert.Equal(-2f, loaded.Settings.PitchShift);
            Assert.Equal("F9", loaded.Settings.VoiceChangerToggleKey);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void AppSettings_Defaults_AreValid()
    {
        var settings = new AppSettings();
        Assert.Equal("normal", settings.VoiceChangerPresetId);
        Assert.Equal(1f, settings.FormantShift);
    }
}
