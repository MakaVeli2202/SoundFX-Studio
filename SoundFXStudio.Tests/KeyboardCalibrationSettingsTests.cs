using SoundFXStudio.Models;
using Xunit;

namespace SoundFXStudio.Tests;

public class KeyboardCalibrationSettingsTests
{
    [Fact]
    public void RescaleForHeroSize_ScalesCoordinatesAndSizeRelativeToReference() {
        var settings = new KeyboardCalibrationSettings
        {
            OpenKeyboardButtonX = 36,
            OpenKeyboardButtonY = 488,
            OpenKeyboardButtonWidth = 220,
            OpenKeyboardButtonHeight = 48,
            ReferenceHeroWidth = 1920,
            ReferenceHeroHeight = 1080
        };

        settings.RescaleForHeroSize(1280, 720);

        Assert.Equal(24, settings.OpenKeyboardButtonX);
        Assert.Equal(325, settings.OpenKeyboardButtonY);
        Assert.Equal(147, settings.OpenKeyboardButtonWidth);
        Assert.Equal(32, settings.OpenKeyboardButtonHeight);
        Assert.Equal(1280, settings.ReferenceHeroWidth);
        Assert.Equal(720, settings.ReferenceHeroHeight);
    }

    [Fact]
    public void RescaleForHeroSize_WhenAlreadyAtReferenceSize_LeavesValuesUntouched()
    {
        var settings = new KeyboardCalibrationSettings
        {
            OpenKeyboardButtonX = 45,
            OpenKeyboardButtonY = 120,
            OpenKeyboardButtonWidth = 200,
            OpenKeyboardButtonHeight = 50,
            ReferenceHeroWidth = 1600,
            ReferenceHeroHeight = 900
        };

        settings.RescaleForHeroSize(1600, 900);

        Assert.Equal(45, settings.OpenKeyboardButtonX);
        Assert.Equal(120, settings.OpenKeyboardButtonY);
        Assert.Equal(200, settings.OpenKeyboardButtonWidth);
        Assert.Equal(50, settings.OpenKeyboardButtonHeight);
    }
}
