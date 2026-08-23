using SoundFXStudio.Models;
using SoundFXStudio.Services;
using SoundFXStudio.Services.DSP;
using SoundFXStudio.ViewModels;
using Xunit;

namespace SoundFXStudio.Tests;

public class HeadphoneProfileTests
{
    // ── HeadphoneProfile model tests ───────────────────────────────────────

    [Fact]
    public void HeadphoneProfile_DefaultValues()
    {
        var profile = new HeadphoneProfile();

        Assert.Equal(string.Empty, profile.Id);
        Assert.Equal(string.Empty, profile.Name);
        Assert.Equal(string.Empty, profile.Manufacturer);
        Assert.Equal(string.Empty, profile.Model);
        Assert.Equal(string.Empty, profile.Description);
        Assert.Equal(0, profile.PreampDb);
        Assert.Empty(profile.Filters);
    }

    [Fact]
    public void HeadphoneProfile_Clone_CreatesIndependentCopy()
    {
        var source = HeadphoneProfilePresets.GetById("flat-test");
        source.PreampDb = -3;

        var clone = source.Clone();

        Assert.Equal(source.Id, clone.Id);
        Assert.Equal(source.Name, clone.Name);
        Assert.Equal(-3, clone.PreampDb);
        Assert.False(ReferenceEquals(source, clone));
    }

    [Fact]
    public void HeadphoneProfile_CloneFiltersAreIndependent()
    {
        var source = HeadphoneProfilePresets.GetById("flat-test");
        var clone = source.Clone();

        Assert.Equal(source.Filters.Count, clone.Filters.Count);
        Assert.False(ReferenceEquals(source.Filters, clone.Filters));

        clone.Filters.Clear();
        Assert.NotEqual(source.Filters.Count, clone.Filters.Count);
    }

    [Fact]
    public void HeadphoneProfile_CloneFilterObjectsAreIndependent()
    {
        var source = HeadphoneProfilePresets.GetById("flat-test");
        var clone = source.Clone();

        if (source.Filters.Count > 0)
        {
            source.Filters[0].GainDb = -999;
            Assert.NotEqual(source.Filters[0].GainDb, clone.Filters[0].GainDb);
        }
    }

    [Fact]
    public void HeadphoneProfile_ToString_UsesName()
    {
        var profile = new HeadphoneProfile { Name = "HD600", Id = "hd600" };
        Assert.Equal("HD600", profile.ToString());
    }

    [Fact]
    public void HeadphoneProfile_ToString_FallsBackToId()
    {
        var profile = new HeadphoneProfile { Id = "hd600" };
        Assert.Equal("hd600", profile.ToString());
    }

    // ── HeadphoneProfilePresets tests ──────────────────────────────────────

    [Fact]
    public void HeadphonePresets_NonEmpty_UniqueIds()
    {
        Assert.NotEmpty(HeadphoneProfilePresets.Profiles);
        Assert.Equal(
            HeadphoneProfilePresets.Profiles.Count,
            HeadphoneProfilePresets.Profiles.Select(p => p.Id).Distinct().Count());
    }

    [Fact]
    public void HeadphonePresets_AllHaveNames()
    {
        Assert.All(HeadphoneProfilePresets.Profiles, p =>
            Assert.False(string.IsNullOrWhiteSpace(p.Name)));
    }

    [Fact]
    public void HeadphonePresets_AllHaveDescriptions()
    {
        Assert.All(HeadphoneProfilePresets.Profiles, p =>
            Assert.False(string.IsNullOrWhiteSpace(p.Description)));
    }

    [Fact]
    public void HeadphonePresets_AllDescriptionsMarkedAsTest()
    {
        Assert.All(HeadphoneProfilePresets.Profiles, p =>
            Assert.Contains("TEST", p.Description, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GetById_UnknownId_ReturnsFirstProfile()
    {
        var profile = HeadphoneProfilePresets.GetById("does-not-exist");
        Assert.Equal(HeadphoneProfilePresets.Profiles[0].Id, profile.Id);
    }

    [Fact]
    public void GetById_ReturnsClone()
    {
        var a = HeadphoneProfilePresets.GetById("flat-test");
        var b = HeadphoneProfilePresets.GetById("flat-test");

        Assert.False(ReferenceEquals(a, b));
        a.PreampDb = -999;
        Assert.NotEqual(a.PreampDb, b.PreampDb);
    }

    [Theory]
    [InlineData("flat-test")]
    [InlineData("gentle-bass-test")]
    [InlineData("gentle-treble-test")]
    [InlineData("neutral-test")]
    public void HeadphonePresets_Exists(string id)
    {
        var profile = HeadphoneProfilePresets.GetById(id);
        Assert.Equal(id, profile.Id);
    }

    [Fact]
    public void GetNone_ReturnsDisabledProfile()
    {
        var none = HeadphoneProfilePresets.GetNone();
        Assert.Equal("none", none.Id);
        Assert.Contains("Disabled", none.Name);
        Assert.Empty(none.Filters);
    }

    // ── GamingEnhancementService headphone EQ tests ────────────────────────

    [Fact]
    public void GamingService_HasHeadphoneEqualizer()
    {
        var service = new GamingEnhancementService();

        Assert.NotNull(service.HeadphoneEqualizer);
        Assert.False(service.HeadphoneEqualizer.IsEnabled);
        Assert.Null(service.ActiveHeadphoneProfile);
    }

    [Fact]
    public void ApplyHeadphoneProfile_ConfigureHeadphoneEqualizer()
    {
        var service = new GamingEnhancementService();
        var profile = HeadphoneProfilePresets.GetById("gentle-bass-test");

        service.ApplyHeadphoneProfile(profile);

        Assert.True(service.HeadphoneEqualizer.IsEnabled);
        Assert.Equal(profile.PreampDb, service.HeadphoneEqualizer.PreampDb);
        Assert.Same(profile, service.ActiveHeadphoneProfile);
    }

    [Fact]
    public void ApplyHeadphoneProfile_Null_DisablesHeadphoneEqualizer()
    {
        var service = new GamingEnhancementService();
        var profile = HeadphoneProfilePresets.GetById("gentle-bass-test");

        service.ApplyHeadphoneProfile(profile);
        Assert.True(service.HeadphoneEqualizer.IsEnabled);

        service.ApplyHeadphoneProfile(null);
        Assert.False(service.HeadphoneEqualizer.IsEnabled);
        Assert.Null(service.ActiveHeadphoneProfile);
    }

    [Fact]
    public void ApplyHeadphoneProfile_DoesNotModifyGamingEqualizer()
    {
        var service = new GamingEnhancementService();
        var gamingProfile = GamingProfilePresets.GetById("footstep-focus");
        var hpProfile = HeadphoneProfilePresets.GetById("gentle-bass-test");

        service.Apply(gamingProfile);
        var gamingEqEnabled = service.Equalizer.IsEnabled;
        var gamingEqPreamp = service.Equalizer.PreampDb;

        service.ApplyHeadphoneProfile(hpProfile);

        Assert.Equal(gamingEqEnabled, service.Equalizer.IsEnabled);
        Assert.Equal(gamingEqPreamp, service.Equalizer.PreampDb);
    }

    [Fact]
    public void ApplyGamingProfile_DoesNotModifyHeadphoneEqualizer()
    {
        var service = new GamingEnhancementService();
        var hpProfile = HeadphoneProfilePresets.GetById("gentle-bass-test");

        service.ApplyHeadphoneProfile(hpProfile);
        var hpEqEnabled = service.HeadphoneEqualizer.IsEnabled;
        var hpEqPreamp = service.HeadphoneEqualizer.PreampDb;

        service.Apply(GamingProfilePresets.GetById("footstep-focus"));

        Assert.Equal(hpEqEnabled, service.HeadphoneEqualizer.IsEnabled);
        Assert.Equal(hpEqPreamp, service.HeadphoneEqualizer.PreampDb);
    }

    [Fact]
    public void Bypass_DisablesHeadphoneEqualizer()
    {
        var service = new GamingEnhancementService();
        var hpProfile = HeadphoneProfilePresets.GetById("gentle-bass-test");

        service.ApplyHeadphoneProfile(hpProfile);
        Assert.True(service.HeadphoneEqualizer.IsEnabled);

        service.Bypass();
        Assert.False(service.HeadphoneEqualizer.IsEnabled);
    }

    [Fact]
    public void HeadphoneChain_Process_ProducesOutput()
    {
        var service = new GamingEnhancementService();
        var profile = HeadphoneProfilePresets.GetById("gentle-bass-test");
        service.ApplyHeadphoneProfile(profile);
        service.SetSampleRate(48000);

        var data = Sine(0.5f, 48000, freq: 1000);
        service.Chain.Process(data);

        var tail = data.AsSpan(24000);
        Assert.True(MaxAbs(tail) > 0.01, "headphone chain produced no output");
    }

    [Fact]
    public void HeadphoneEq_Disabled_PassesThrough()
    {
        var service = new GamingEnhancementService();
        var profile = HeadphoneProfilePresets.GetById("gentle-bass-test");
        service.ApplyHeadphoneProfile(profile);
        service.HeadphoneEqualizer.IsEnabled = false;
        service.SetSampleRate(48000);

        var data = Sine(0.5f, 48000, freq: 1000);
        service.Chain.Process(data);

        var tail = data.AsSpan(24000);
        Assert.True(MaxAbs(tail) > 0.1, "disabled headphone EQ should pass through");
    }

    [Fact]
    public void FlatHeadphoneProfile_PassesThrough()
    {
        var service = new GamingEnhancementService();
        var profile = HeadphoneProfilePresets.GetById("flat-test");
        service.ApplyHeadphoneProfile(profile);
        service.SetSampleRate(48000);

        var data = Sine(0.5f, 48000, freq: 1000);
        service.Chain.Process(data);

        var tail = data.AsSpan(24000);
        Assert.InRange(MaxAbs(tail), 0.45, 0.55);
    }

    [Fact]
    public void BassBoostHeadphoneProfile_AffectsLowFreq()
    {
        var service = new GamingEnhancementService();
        var profile = HeadphoneProfilePresets.GetById("gentle-bass-test");
        service.ApplyHeadphoneProfile(profile);
        service.SetSampleRate(48000);

        var data = Sine(0.5f, 48000, freq: 60);
        service.Chain.Process(data);

        var tail = data.AsSpan(24000);
        Assert.True(MaxAbs(tail) > 0.55, "bass boost should increase low frequency amplitude");
    }

    // ── VoiceChangerService isolation ──────────────────────────────────────

    [Fact]
    public void ApplyHeadphoneProfile_DoesNotModifyVoiceChangerChain()
    {
        var vcService = new VoiceChangerService();
        var gamingService = new GamingEnhancementService();
        var hpProfile = HeadphoneProfilePresets.GetById("gentle-bass-test");

        var origGateEnabled = vcService.Chain.Get<NoiseGateEffect>()!.IsEnabled;
        var origCompEnabled = vcService.Chain.Get<CompressorEffect>()!.IsEnabled;

        gamingService.ApplyHeadphoneProfile(hpProfile);

        Assert.Equal(origGateEnabled, vcService.Chain.Get<NoiseGateEffect>()!.IsEnabled);
        Assert.Equal(origCompEnabled, vcService.Chain.Get<CompressorEffect>()!.IsEnabled);
        Assert.NotNull(vcService.Chain.Get<EqualizerEffect>());
    }

    // ── Persistence tests ──────────────────────────────────────────────────

    [Fact]
    public void ConfigService_PreservesHeadphoneSettings()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "SoundFXStudio-Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var cs = new ConfigService(appFolder: tempDir);
            var config = new AppConfig();
            config.Settings.HeadphoneEqEnabled = true;
            config.Settings.ActiveHeadphoneProfileId = "gentle-bass-test";

            cs.Save(config);
            var loaded = cs.Load();

            Assert.True(loaded.Settings.HeadphoneEqEnabled);
            Assert.Equal("gentle-bass-test", loaded.Settings.ActiveHeadphoneProfileId);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void AppSettings_HeadphoneDefaults_AreValid()
    {
        var settings = new AppSettings();

        Assert.False(settings.HeadphoneEqEnabled);
        Assert.Equal(string.Empty, settings.ActiveHeadphoneProfileId);
    }

    // ── GamingViewModel headphone tests ────────────────────────────────────

    [Fact]
    public void GamingViewModel_HeadphoneProfiles_Populated()
    {
        using var vm = new GamingViewModel();
        Assert.Equal(HeadphoneProfilePresets.Profiles.Count + 1, vm.AvailableHeadphoneProfiles.Count);
    }

    [Fact]
    public void GamingViewModel_FirstHeadphoneProfileIsNone()
    {
        using var vm = new GamingViewModel();
        Assert.NotNull(vm.SelectedHeadphoneProfile);
        Assert.Equal("none", vm.SelectedHeadphoneProfile!.Id);
    }

    [Fact]
    public void GamingViewModel_HeadphoneEq_DefaultDisabled()
    {
        using var vm = new GamingViewModel();
        Assert.False(vm.IsHeadphoneEqEnabled);
        Assert.Equal("DISABLED", vm.HeadphoneEqToggleText);
        Assert.Equal("#E85555", vm.HeadphoneEqStatusColor);
    }

    [Fact]
    public void GamingViewModel_ToggleHeadphoneEq_TogglesState()
    {
        using var vm = new GamingViewModel();
        Assert.False(vm.IsHeadphoneEqEnabled);

        vm.IsHeadphoneEqEnabled = true;
        Assert.True(vm.IsHeadphoneEqEnabled);
        Assert.Equal("ENABLED", vm.HeadphoneEqToggleText);
        Assert.Equal("#22C55E", vm.HeadphoneEqStatusColor);

        vm.IsHeadphoneEqEnabled = false;
        Assert.False(vm.IsHeadphoneEqEnabled);
    }

    [Fact]
    public void GamingViewModel_ToggleHeadphoneEqCommand_Toggles()
    {
        using var vm = new GamingViewModel();
        Assert.False(vm.IsHeadphoneEqEnabled);

        vm.ToggleHeadphoneEqCommand.Execute(null);
        Assert.True(vm.IsHeadphoneEqEnabled);

        vm.ToggleHeadphoneEqCommand.Execute(null);
        Assert.False(vm.IsHeadphoneEqEnabled);
    }

    [Fact]
    public void GamingViewModel_SaveCallback_InvokedOnHeadphoneEqToggle()
    {
        bool saved = false;
        using var vm = new GamingViewModel(saveAction: () => saved = true);
        vm.IsHeadphoneEqEnabled = true;
        Assert.True(saved);
    }

    [Fact]
    public void GamingViewModel_SaveCallback_InvokedOnHeadphoneProfileChange()
    {
        bool saved = false;
        using var vm = new GamingViewModel(saveAction: () => saved = true);
        vm.SelectedHeadphoneProfile = HeadphoneProfilePresets.GetById("gentle-bass-test");
        Assert.True(saved);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static float[] Sine(float amplitude, int length, float freq = 220f, int sampleRate = 48000)
    {
        var data = new float[length];
        for (int i = 0; i < length; i++)
        {
            data[i] = (float)(amplitude * Math.Sin(2 * Math.PI * freq * i / sampleRate));
        }
        return data;
    }

    private static float MaxAbs(Span<float> data)
    {
        float max = 0;
        for (int i = 0; i < data.Length; i++)
        {
            max = Math.Max(max, Math.Abs(data[i]));
        }
        return max;
    }
}
