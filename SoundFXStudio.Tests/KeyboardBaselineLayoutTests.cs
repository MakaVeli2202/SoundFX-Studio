using SoundFXStudio.Controls;
using SoundFXStudio.Models;
using SoundFXStudio.Services;
using Xunit;

namespace SoundFXStudio.Tests;

[Collection("KeyboardLayoutStatics")]
public class KeyboardBaselineLayoutTests : IDisposable
{
    private readonly KeyboardKey _key = new() { Id = "A-0-0", KeyName = "A", RowIndex = 1, ColumnIndex = 0, WidthUnits = 1, HeightUnits = 1 };

    public KeyboardBaselineLayoutTests() => ResetStatics();

    public void Dispose()
    {
        ResetStatics();
    }

    private static void ResetStatics()
    {
        KeyboardLayoutPanel.ClearAllPerKeyOverrides();
        KeyboardLayoutPanel.ClearClusterGapCalibration();
        KeyboardLayoutPanel.ClearRowGapCalibration();
        KeyboardLayoutPanel.ResetRowCalibration();
        KeyboardLayoutPanel.SetLayoutCalibration(43, 3, 3, 65, 72);
        KeyboardLayoutPanel.ButtonScale = 1.0;
        KeyboardLayoutPanel.ClearKeyBaselines();
    }

    [Fact]
    public void CapturingBaselines_LeavesEveryKeyExactlyWhereItWas()
    {
        var before = KeyboardLayoutPanel.GetKeyGeometry(_key);
        Assert.NotNull(before);

        foreach (var key in new[] { _key })
        {
            var geometry = KeyboardLayoutPanel.GetKeyGeometry(key)!.Value;
            KeyboardLayoutPanel.SetKeyBaseline(key.Id, geometry.X, geometry.Y, geometry.Width, geometry.Height);
        }

        var after = KeyboardLayoutPanel.GetKeyGeometry(_key);

        Assert.Equal(before!.Value.X, after!.Value.X, 6);
        Assert.Equal(before.Value.Y, after.Value.Y, 6);
        Assert.Equal(before.Value.Width, after.Value.Width, 6);
        Assert.Equal(before.Value.Height, after.Value.Height, 6);
    }

    [Fact]
    public void BaselineButtonScaleOfOne_KeepsButtonScaleSliderNeutralAtOne()
    {
        KeyboardLayoutPanel.SetKeyBaseline(_key.Id, 100, 200, 40, 40);
        KeyboardLayoutPanel.BaselineButtonScale = 1.0;
        KeyboardLayoutPanel.ButtonScale = 1.0;

        var geometry = KeyboardLayoutPanel.GetKeyGeometry(_key)!.Value;

        Assert.Equal(100, geometry.X, 6);
        Assert.Equal(200, geometry.Y, 6);
        Assert.Equal(40, geometry.Width, 6);
        Assert.Equal(40, geometry.Height, 6);
    }

    [Fact]
    public void BaselineButtonScale_RescalesAroundKeyCentre()
    {
        KeyboardLayoutPanel.SetKeyBaseline(_key.Id, 100, 200, 40, 40);
        KeyboardLayoutPanel.BaselineButtonScale = 1.0;
        KeyboardLayoutPanel.ButtonScale = 0.5;

        var geometry = KeyboardLayoutPanel.GetKeyGeometry(_key)!.Value;

        Assert.Equal(20, geometry.Width, 6);
        Assert.Equal(20, geometry.Height, 6);
        Assert.Equal(110, geometry.X, 6);
        Assert.Equal(210, geometry.Y, 6);
    }

    [Fact]
    public void BaselineButtonScale_RelativeToCapturedScale_LeavesLegacyLayoutsUnchanged()
    {
        KeyboardLayoutPanel.SetKeyBaseline(_key.Id, 100, 200, 40, 40);
        KeyboardLayoutPanel.BaselineButtonScale = 1.3;
        KeyboardLayoutPanel.ButtonScale = 1.3;

        var geometry = KeyboardLayoutPanel.GetKeyGeometry(_key)!.Value;

        Assert.Equal(40, geometry.Width, 6);
        Assert.Equal(100, geometry.X, 6);
    }

    [Fact]
    public void ClearKeyBaselines_ResetsCapturedButtonScale()
    {
        KeyboardLayoutPanel.SetKeyBaseline(_key.Id, 100, 200, 40, 40);
        KeyboardLayoutPanel.BaselineButtonScale = 1.0;
        Assert.True(KeyboardLayoutPanel.HasKeyBaselines);

        KeyboardLayoutPanel.ClearKeyBaselines();

        Assert.False(KeyboardLayoutPanel.HasKeyBaselines);
        Assert.Equal(0d, KeyboardLayoutPanel.BaselineButtonScale);
    }

    [Fact]
    public void BaselineRoundTrip_ThroughSnapshot_PreservesGeometry()
    {
        var original = KeyboardLayoutPanel.GetKeyGeometry(_key)!.Value;
        KeyboardLayoutPanel.SetKeyBaseline(_key.Id, original.X, original.Y, original.Width, original.Height);
        KeyboardLayoutPanel.BaselineButtonScale = 1.0;

        var snapshot = KeyboardLayoutPanel.GetKeyBaselinesSnapshot();
        KeyboardLayoutPanel.ClearKeyBaselines();
        KeyboardLayoutPanel.BaselineButtonScale = 1.0;
        foreach (var entry in snapshot)
        {
            KeyboardLayoutPanel.SetKeyBaseline(entry.Key, entry.Value.X, entry.Value.Y, entry.Value.Width, entry.Value.Height);
        }

        var restored = KeyboardLayoutPanel.GetKeyGeometry(_key)!.Value;

        Assert.Equal(original.X, restored.X, 6);
        Assert.Equal(original.Y, restored.Y, 6);
        Assert.Equal(original.Width, restored.Width, 6);
        Assert.Equal(original.Height, restored.Height, 6);
    }
}
