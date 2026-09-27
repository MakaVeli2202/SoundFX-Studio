using SoundFXStudio.Services;
using Xunit;

namespace SoundFXStudio.Tests;

public class ConfigCalibrationSeedTests : IDisposable
{
    private readonly string _appFolder = Path.Combine(Path.GetTempPath(), "sfx-cal-seed-" + Guid.NewGuid().ToString("N"));

    public ConfigCalibrationSeedTests() => Directory.CreateDirectory(_appFolder);

    public void Dispose()
    {
        if (Directory.Exists(_appFolder))
        {
            Directory.Delete(_appFolder, true);
        }
    }

    private static string? FindProjectSeed()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "SoundFXStudio", "keyboard-calibration.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            if (File.Exists(Path.Combine(dir.FullName, "SoundFXStudio.sln")))
            {
                return null;
            }

            dir = dir.Parent;
        }

        return null;
    }

    [Fact]
    public void FreshConfig_ReceivesShippedSeedCalibration()
    {
        var seed = FindProjectSeed();
        if (seed is null)
        {
            return;
        }

        using var seedDoc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(seed));
        var seedKeyUnit = seedDoc.RootElement.GetProperty("KeyUnit").GetDouble();

        var config = new ConfigService(appFolder: _appFolder).Load();

        Assert.Equal(seedKeyUnit, config.Settings.KeyboardCalibration.KeyUnit, 6);
        Assert.False(config.Settings.KeyboardCalibration.IsUserCalibrated);
    }

    [Fact]
    public void SavedUserCalibration_IsNotOverwrittenByShippedSeedOnReload()
    {
        if (FindProjectSeed() is null)
        {
            return;
        }

        var service = new ConfigService(appFolder: _appFolder);
        service.Save(new Models.AppConfig
        {
            Settings = new Models.AppSettings
            {
                KeyboardCalibration = new Models.KeyboardCalibrationSettings
                {
                    IsUserCalibrated = true,
                    KeyUnit = 51.5,
                    GapX = 7.25,
                    GapY = 8.5,
                    OffsetX = 11,
                    OffsetY = 22
                }
            }
        });

        var reloaded = service.Load();
        var calibration = reloaded.Settings.KeyboardCalibration;

        Assert.True(calibration.IsUserCalibrated);
        Assert.Equal(51.5, calibration.KeyUnit, 6);
        Assert.Equal(7.25, calibration.GapX, 6);
        Assert.Equal(8.5, calibration.GapY, 6);
        Assert.Equal(11, calibration.OffsetX, 6);
        Assert.Equal(22, calibration.OffsetY, 6);
    }

    [Fact]
    public void SavedUserCalibrationWithoutPerKeyEntries_IsStillHonoured()
    {
        if (FindProjectSeed() is null)
        {
            return;
        }

        var service = new ConfigService(appFolder: _appFolder);
        service.Save(new Models.AppConfig
        {
            Settings = new Models.AppSettings
            {
                KeyboardCalibration = new Models.KeyboardCalibrationSettings
                {
                    IsUserCalibrated = true,
                    MainLettersOffsetX = -37,
                    MainRowOffsetX2 = 9
                }
            }
        });

        var calibration = service.Load().Settings.KeyboardCalibration;

        Assert.Equal(-37, calibration.MainLettersOffsetX, 6);
        Assert.Equal(9, calibration.MainRowOffsetX2, 6);
        Assert.Empty(calibration.KeyOverrides);
    }
}
