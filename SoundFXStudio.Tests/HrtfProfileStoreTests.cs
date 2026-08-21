using SoundFXStudio.Models;
using SoundFXStudio.Services.Hrtf;
using Xunit;

namespace SoundFXStudio.Tests;

public class HrtfProfileStoreTests : IDisposable
{
    // ── Helpers ────────────────────────────────────────────────────────────

    private readonly string _testFolder;

    public HrtfProfileStoreTests()
    {
        _testFolder = Path.Combine(Path.GetTempPath(), $"sfxs_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testFolder);
    }

    public void Dispose()
    {
        try { Directory.Delete(_testFolder, true); } catch { }
    }

    private HrtfProfileStore CreateStore() => new(_testFolder);

    private static HrtfProfile CreateTestProfile(string id = "test-1", string name = "Test Profile")
    {
        return new HrtfProfile
        {
            Id = id,
            Name = name,
            Manufacturer = "Test Manufacturer",
            Description = "A test profile",
            SampleRate = 48000,
            IrLength = 128,
            Entries = new[]
            {
                new HrtfEntry
                {
                    AzimuthDeg = 0,
                    ElevationDeg = 0,
                    LeftEarResponse = new float[] { 1.0f, 0.5f, 0.25f },
                    RightEarResponse = new float[] { 0.9f, 0.45f, 0.22f }
                }
            },
            DataSource = "Test",
            License = "Test License"
        };
    }

    // ── LoadAll ──────────────────────────────────────────────────────────

    [Fact]
    public void LoadAll_NoStorage_ReturnsEmpty()
    {
        var store = CreateStore();
        var profiles = store.LoadAll();
        Assert.Empty(profiles);
    }

    [Fact]
    public void LoadAll_AfterSave_ReturnsSavedProfile()
    {
        var store = CreateStore();
        var profile = CreateTestProfile();

        store.Save(profile);
        var loaded = store.LoadAll();

        Assert.Single(loaded);
        Assert.Equal(profile.Id, loaded[0].Id);
        Assert.Equal(profile.Name, loaded[0].Name);
    }

    [Fact]
    public void LoadAll_MultipleProfiles()
    {
        var store = CreateStore();
        store.Save(CreateTestProfile("p1", "Profile 1"));
        store.Save(CreateTestProfile("p2", "Profile 2"));
        store.Save(CreateTestProfile("p3", "Profile 3"));

        var loaded = store.LoadAll();
        Assert.Equal(3, loaded.Count);
    }

    // ── Save ─────────────────────────────────────────────────────────────

    [Fact]
    public void Save_PreservesHrirData()
    {
        var store = CreateStore();
        var profile = CreateTestProfile();

        store.Save(profile);
        var loaded = store.LoadAll();

        Assert.Single(loaded);
        Assert.Equal(3, loaded[0].Entries[0].LeftEarResponse.Length);
        Assert.Equal(1.0f, loaded[0].Entries[0].LeftEarResponse[0]);
        Assert.Equal(0.5f, loaded[0].Entries[0].LeftEarResponse[1]);
    }

    [Fact]
    public void Save_UpdatesExistingProfile()
    {
        var store = CreateStore();
        var profile = CreateTestProfile();
        store.Save(profile);

        var updated = CreateTestProfile(name: "Updated Name");
        store.Save(updated);

        var loaded = store.LoadAll();
        Assert.Single(loaded);
        Assert.Equal("Updated Name", loaded[0].Name);
    }

    [Fact]
    public void Save_DoesNotShareReferences()
    {
        var store = CreateStore();
        var profile = CreateTestProfile();
        store.Save(profile);

        var loaded = store.LoadAll();
        loaded[0].Entries[0].LeftEarResponse[0] = -999f;

        var reloaded = store.LoadAll();
        Assert.Equal(1.0f, reloaded[0].Entries[0].LeftEarResponse[0]);
    }

    // ── Delete ───────────────────────────────────────────────────────────

    [Fact]
    public void Delete_RemovesProfile()
    {
        var store = CreateStore();
        var profile = CreateTestProfile();
        store.Save(profile);

        store.Delete(profile.Id);
        var loaded = store.LoadAll();
        Assert.Empty(loaded);
    }

    [Fact]
    public void Delete_NonexistentId_NoOp()
    {
        var store = CreateStore();
        store.Save(CreateTestProfile());

        store.Delete("nonexistent-id");
        var loaded = store.LoadAll();
        Assert.Single(loaded);
    }

    [Fact]
    public void Delete_EmptyId_NoOp()
    {
        var store = CreateStore();
        store.Save(CreateTestProfile());

        store.Delete("");
        store.Delete(null!);
        var loaded = store.LoadAll();
        Assert.Single(loaded);
    }

    // ── Corruption resilience ────────────────────────────────────────────

    [Fact]
    public void LoadAll_CorruptFile_ReturnsEmpty()
    {
        var store = CreateStore();
        // Write garbage to the storage file
        var path = Path.Combine(_testFolder, "imported_hrtf_profiles.json");
        File.WriteAllText(path, "{ invalid json !!!");

        var loaded = store.LoadAll();
        Assert.Empty(loaded);
    }

    [Fact]
    public void Save_CorruptFile_Recovers()
    {
        var store = CreateStore();
        var path = Path.Combine(_testFolder, "imported_hrtf_profiles.json");
        File.WriteAllText(path, "garbage");

        // Save should overwrite the corrupt file
        store.Save(CreateTestProfile());
        var loaded = store.LoadAll();
        Assert.Single(loaded);
    }

    // ── Deep clone safety ────────────────────────────────────────────────

    [Fact]
    public void Save_ProfileEntriesAreDeepCloned()
    {
        var store = CreateStore();
        var profile = CreateTestProfile();
        store.Save(profile);

        // Mutate original after save
        profile.Entries[0].LeftEarResponse[0] = -999f;

        var loaded = store.LoadAll();
        Assert.Equal(1.0f, loaded[0].Entries[0].LeftEarResponse[0]);
    }

    [Fact]
    public void LoadAll_ReturnsDeepClones()
    {
        var store = CreateStore();
        store.Save(CreateTestProfile());

        var load1 = store.LoadAll();
        var load2 = store.LoadAll();

        load1[0].Entries[0].LeftEarResponse[0] = -999f;
        Assert.Equal(1.0f, load2[0].Entries[0].LeftEarResponse[0]);
    }
}
