using SoundFXStudio.Models;
using SoundFXStudio.Services.Hrtf;
using SoundFXStudio.ViewModels;
using Xunit;

namespace SoundFXStudio.Tests;

public class GamingViewModelHrtfImportTests
{
    // ── Helpers ────────────────────────────────────────────────────────────

    private static HrtfProfile CreateImportedProfile(string id = "imported-1", string name = "Imported Profile")
    {
        return new HrtfProfile
        {
            Id = id,
            Name = name,
            Manufacturer = "Test",
            Description = "Test imported profile",
            SampleRate = 48000,
            IrLength = 128,
            Entries = new[]
            {
                new HrtfEntry
                {
                    AzimuthDeg = 0,
                    ElevationDeg = 0,
                    LeftEarResponse = new float[] { 1.0f, 0.5f },
                    RightEarResponse = new float[] { 0.9f, 0.45f }
                }
            },
            DataSource = "Test",
            License = "Test"
        };
    }

    private class FakeProfileStore : IHrtfProfileStore
    {
        private readonly List<HrtfProfile> _profiles;

        public FakeProfileStore(IEnumerable<HrtfProfile>? initial = null)
        {
            _profiles = initial?.ToList() ?? new List<HrtfProfile>();
        }

        public IReadOnlyList<HrtfProfile> LoadAll() => _profiles.AsReadOnly();

        public void Save(HrtfProfile profile)
        {
            var idx = _profiles.FindIndex(p => p.Id == profile.Id);
            if (idx >= 0) _profiles[idx] = profile;
            else _profiles.Add(profile);
        }

        public void Delete(string profileId)
        {
            _profiles.RemoveAll(p => p.Id == profileId);
        }
    }

    private class FakeSofaLoader : ISofaHrtfLoader
    {
        public SofaHrtfLoadResult? LastResult { get; set; }

        public SofaHrtfLoadResult Load(string filePath)
        {
            return LastResult ?? SofaHrtfLoadResult.Fail("No result configured");
        }
    }

    // ── Constructor loads imported profiles ───────────────────────────────

    [Fact]
    public void Constructor_LoadsImportedProfilesFromStore()
    {
        var store = new FakeProfileStore(new[] { CreateImportedProfile() });
        var vm = new GamingViewModel(profileStore: store);

        var imported = vm.AvailableHrtfProfiles
            .Where(p => p.Id == "imported-1")
            .ToList();
        Assert.Single(imported);
        Assert.Equal("Imported Profile", imported[0].Name);
    }

    [Fact]
    public void Constructor_ImportedProfilesAppearAfterPresets()
    {
        var store = new FakeProfileStore(new[] { CreateImportedProfile() });
        var vm = new GamingViewModel(profileStore: store);

        var presetIndex = vm.AvailableHrtfProfiles
            .ToList()
            .FindIndex(p => p.Id == "synthetic-front");
        var importedIndex = vm.AvailableHrtfProfiles
            .ToList()
            .FindIndex(p => p.Id == "imported-1");

        Assert.True(presetIndex >= 0);
        Assert.True(importedIndex > presetIndex);
    }

    // ── CanDeleteHrtfProfile ─────────────────────────────────────────────

    [Fact]
    public void CanDelete_PresetProfile_ReturnsFalse()
    {
        var vm = new GamingViewModel();
        vm.SelectedHrtfProfile = vm.AvailableHrtfProfiles
            .First(p => p.Id == "synthetic-front");
        Assert.False(vm.CanDeleteHrtfProfile);
    }

    [Fact]
    public void CanDelete_NoneProfile_ReturnsFalse()
    {
        var vm = new GamingViewModel();
        vm.SelectedHrtfProfile = vm.AvailableHrtfProfiles
            .First(p => p.Id == "none");
        Assert.False(vm.CanDeleteHrtfProfile);
    }

    [Fact]
    public void CanDelete_ImportedProfile_ReturnsTrue()
    {
        var store = new FakeProfileStore(new[] { CreateImportedProfile() });
        var vm = new GamingViewModel(profileStore: store);
        vm.SelectedHrtfProfile = vm.AvailableHrtfProfiles
            .First(p => p.Id == "imported-1");
        Assert.True(vm.CanDeleteHrtfProfile);
    }

    [Fact]
    public void CanDelete_NullSelection_ReturnsFalse()
    {
        var vm = new GamingViewModel();
        vm.SelectedHrtfProfile = null;
        Assert.False(vm.CanDeleteHrtfProfile);
    }

    // ── DeleteHrtfProfile ────────────────────────────────────────────────

    [Fact]
    public void Delete_ImportedProfile_RemovesFromCollection()
    {
        var store = new FakeProfileStore(new[] { CreateImportedProfile() });
        var vm = new GamingViewModel(profileStore: store);
        vm.SelectedHrtfProfile = vm.AvailableHrtfProfiles
            .First(p => p.Id == "imported-1");

        vm.DeleteHrtfProfileCommand.Execute(null);

        Assert.DoesNotContain(vm.AvailableHrtfProfiles,
            p => p.Id == "imported-1");
    }

    [Fact]
    public void Delete_ImportedProfile_RemovesFromStore()
    {
        var store = new FakeProfileStore(new[] { CreateImportedProfile() });
        var vm = new GamingViewModel(profileStore: store);
        vm.SelectedHrtfProfile = vm.AvailableHrtfProfiles
            .First(p => p.Id == "imported-1");

        vm.DeleteHrtfProfileCommand.Execute(null);

        Assert.Empty(store.LoadAll());
    }

    [Fact]
    public void Delete_ImportedProfile_SelectsNearestProfile()
    {
        var store = new FakeProfileStore(new[] { CreateImportedProfile() });
        var vm = new GamingViewModel(profileStore: store);
        vm.SelectedHrtfProfile = vm.AvailableHrtfProfiles
            .First(p => p.Id == "imported-1");

        vm.DeleteHrtfProfileCommand.Execute(null);

        Assert.NotNull(vm.SelectedHrtfProfile);
    }

    [Fact]
    public void Delete_PresetProfile_DoesNothing()
    {
        var store = new FakeProfileStore();
        var vm = new GamingViewModel(profileStore: store);
        var countBefore = vm.AvailableHrtfProfiles.Count;
        vm.SelectedHrtfProfile = vm.AvailableHrtfProfiles
            .First(p => p.Id == "synthetic-front");

        vm.DeleteHrtfProfileCommand.Execute(null);

        Assert.Equal(countBefore, vm.AvailableHrtfProfiles.Count);
    }

    // ── RestoreSettings ──────────────────────────────────────────────────

    [Fact]
    public void RestoreSettings_WithImportedProfileId_SelectsIt()
    {
        var imported = CreateImportedProfile();
        var store = new FakeProfileStore(new[] { imported });
        var settings = new AppSettings { ActiveHrtfProfileId = "imported-1" };
        var vm = new GamingViewModel(settings: settings, profileStore: store);

        Assert.Equal("imported-1", vm.SelectedHrtfProfile?.Id);
    }

    [Fact]
    public void RestoreSettings_WithMissingProfileId_FallsBackToNone()
    {
        var store = new FakeProfileStore();
        var settings = new AppSettings { ActiveHrtfProfileId = "nonexistent" };
        var vm = new GamingViewModel(settings: settings, profileStore: store);

        Assert.Equal("none", vm.SelectedHrtfProfile?.Id);
    }
}
