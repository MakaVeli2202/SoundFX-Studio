namespace SoundFXStudio.Models;

public class KeyboardCalibrationSettings
{
    public double KeyUnit { get; set; } = 43;
    public double Gap { get; set; } = 3;
    public double GapX { get; set; }
    public double GapY { get; set; }
    public double OffsetX { get; set; } = 65;
    public double OffsetY { get; set; } = 72;
    public double ButtonScale { get; set; } = 1.0;
    public double InnerSectionInsetPercent { get; set; } = 20;
    public double InnerSectionInsetXPercent { get; set; }
    public double InnerSectionInsetYPercent { get; set; }
    public double InnerSectionOffsetXPercent { get; set; }
    public double InnerSectionOffsetYPercent { get; set; }
    public double KeyboardWindowScale { get; set; } = 0.8;
    public double OpenKeyboardButtonX { get; set; } = 36;
    public double OpenKeyboardButtonY { get; set; } = 488;
    public double OpenKeyboardButtonWidth { get; set; } = 220;
    public double OpenKeyboardButtonHeight { get; set; } = 48;

    public double ReferenceHeroWidth { get; set; }
    public double ReferenceHeroHeight { get; set; }

    public void RescaleForHeroSize(double currentHeroWidth, double currentHeroHeight)
    {
        if (ReferenceHeroWidth <= 0 || ReferenceHeroHeight <= 0) return;
        if (currentHeroWidth <= 0 || currentHeroHeight <= 0) return;
        if (Math.Abs(ReferenceHeroWidth - currentHeroWidth) < 1 &&
            Math.Abs(ReferenceHeroHeight - currentHeroHeight) < 1) return;

        double sx = currentHeroWidth / ReferenceHeroWidth;
        double sy = currentHeroHeight / ReferenceHeroHeight;

        OpenKeyboardButtonX = Math.Round(OpenKeyboardButtonX * sx);
        OpenKeyboardButtonY = Math.Round(OpenKeyboardButtonY * sy);
        OpenKeyboardButtonWidth = Math.Round(OpenKeyboardButtonWidth * sx);
        OpenKeyboardButtonHeight = Math.Round(OpenKeyboardButtonHeight * sy);

        ReferenceHeroWidth = currentHeroWidth;
        ReferenceHeroHeight = currentHeroHeight;
    }

    public bool DebugCalibration { get; set; }

    public double CapsLockIndicatorOffsetX { get; set; } = 1235;
    public double CapsLockIndicatorOffsetY { get; set; } = 252;
    public double NumLockIndicatorOffsetX { get; set; } = 1297;
    public double NumLockIndicatorOffsetY { get; set; } = 252;
    public double ScrollLockIndicatorOffsetX { get; set; } = 1359;
    public double ScrollLockIndicatorOffsetY { get; set; } = 252;

    public double CapsLockIndicatorSize { get; set; } = 34;
    public double NumLockIndicatorSize { get; set; } = 34;
    public double ScrollLockIndicatorSize { get; set; } = 34;

    // User-placed positions for the keyboard window floating panels (0 = auto-anchor).
    public double SoundboardStatusPanelX { get; set; }
    public double SoundboardStatusPanelY { get; set; }
    public double CloseButtonPanelX { get; set; }
    public double CloseButtonPanelY { get; set; }

    // User-set sizes for the keyboard window floating panels (0 = auto-size).
    public double SoundboardStatusPanelWidth { get; set; }
    public double SoundboardStatusPanelHeight { get; set; }
    public double CloseButtonPanelWidth { get; set; }
    public double CloseButtonPanelHeight { get; set; }

    // Legacy cluster offsets kept for migration compatibility.
    public double EscOffsetX { get; set; }
    public double EscOffsetY { get; set; }
    public double F1ToF4OffsetX { get; set; }
    public double F1ToF4OffsetY { get; set; }
    public double F5ToF8OffsetX { get; set; }
    public double F5ToF8OffsetY { get; set; }
    public double F9ToF12OffsetX { get; set; }
    public double F9ToF12OffsetY { get; set; }
    public double PrintScrollPauseOffsetX { get; set; }
    public double PrintScrollPauseOffsetY { get; set; }
    public double MainTypingOffsetX { get; set; }
    public double MainTypingOffsetY { get; set; }
    public double NavigationOffsetX { get; set; }
    public double NavigationOffsetY { get; set; }
    public double ArrowOffsetX { get; set; }
    public double ArrowOffsetY { get; set; }
    public double NumpadOffsetX { get; set; }
    public double NumpadOffsetY { get; set; }
    public double MainLettersOffsetX { get; set; }
    public double MainLettersOffsetY { get; set; }

    // Per-cluster key size adjustments in pixels.
    public double EscWidthAdjustment { get; set; }
    public double EscHeightAdjustment { get; set; }
    public double F1ToF4WidthAdjustment { get; set; }
    public double F1ToF4HeightAdjustment { get; set; }
    public double F5ToF8WidthAdjustment { get; set; }
    public double F5ToF8HeightAdjustment { get; set; }
    public double F9ToF12WidthAdjustment { get; set; }
    public double F9ToF12HeightAdjustment { get; set; }
    public double PrintScrollPauseWidthAdjustment { get; set; }
    public double PrintScrollPauseHeightAdjustment { get; set; }
    public double MainTypingWidthAdjustment { get; set; }
    public double MainTypingHeightAdjustment { get; set; }
    public double NavigationWidthAdjustment { get; set; }
    public double NavigationHeightAdjustment { get; set; }
    public double ArrowWidthAdjustment { get; set; }
    public double ArrowHeightAdjustment { get; set; }
    public double NumpadWidthAdjustment { get; set; }
    public double NumpadHeightAdjustment { get; set; }
    public double MainLettersWidthAdjustment { get; set; }
    public double MainLettersHeightAdjustment { get; set; }

    public double MainRowOffsetX1 { get; set; }
    public double MainRowOffsetY1 { get; set; }
    public double MainRowOffsetX2 { get; set; }
    public double MainRowOffsetY2 { get; set; }
    public double MainRowOffsetX3 { get; set; }
    public double MainRowOffsetY3 { get; set; }
    public double MainRowOffsetX4 { get; set; }
    public double MainRowOffsetY4 { get; set; }

    // Legacy special-key width adjustments kept for migration compatibility.
    public double SpacebarWidthAdjustment { get; set; }
    public double BackspaceWidthAdjustment { get; set; }
    public double EnterWidthAdjustment { get; set; }
    public double IsoEnterWidthAdjustment { get; set; }
    public double LeftShiftWidthAdjustment { get; set; }
    public double RightShiftWidthAdjustment { get; set; }
    public double NumpadEnterWidthAdjustment { get; set; }
    public double TabWidthAdjustment { get; set; }
    public double CapsLockWidthAdjustment { get; set; }

    public Dictionary<string, KeyCalibrationOverrideSettings> KeyOverrides { get; set; } = new();

    // Additive gap overrides per cluster, keyed by (int)KeyboardCluster.
    public Dictionary<int, double> ClusterGapOverridesX { get; set; } = new();
    public Dictionary<int, double> ClusterGapOverridesY { get; set; } = new();

    // Additive gap overrides per letter row, keyed by row index (1..4).
    public Dictionary<int, double> RowGapOverridesX { get; set; } = new();
    public Dictionary<int, double> RowGapOverridesY { get; set; } = new();

    // Absolute per-key baseline captured by "Set as Default". When present,
    // the key is placed at this absolute position plus any current deltas.
    public Dictionary<string, KeyBaselineSettings> KeyBaselines { get; set; } = new();
}

public class KeyBaselineSettings
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }

    public KeyBaselineSettings Clone()
    {
        return new KeyBaselineSettings { X = X, Y = Y, Width = Width, Height = Height };
    }
}

public class KeyCalibrationOverrideSettings
{
    public double OffsetX { get; set; }
    public double OffsetY { get; set; }
    public double WidthAdjustment { get; set; }
    public double HeightAdjustment { get; set; }
    public double InnerInsetAdjustmentPercent { get; set; }
    public double InnerInsetXAdjustmentPercent { get; set; }
    public double InnerInsetYAdjustmentPercent { get; set; }
    public double InnerOffsetXAdjustmentPercent { get; set; }
    public double InnerOffsetYAdjustmentPercent { get; set; }

    public KeyCalibrationOverrideSettings Clone()
    {
        return new KeyCalibrationOverrideSettings
        {
            OffsetX = OffsetX,
            OffsetY = OffsetY,
            WidthAdjustment = WidthAdjustment,
            HeightAdjustment = HeightAdjustment,
            InnerInsetAdjustmentPercent = InnerInsetAdjustmentPercent,
            InnerInsetXAdjustmentPercent = InnerInsetXAdjustmentPercent,
            InnerInsetYAdjustmentPercent = InnerInsetYAdjustmentPercent,
            InnerOffsetXAdjustmentPercent = InnerOffsetXAdjustmentPercent,
            InnerOffsetYAdjustmentPercent = InnerOffsetYAdjustmentPercent
        };
    }
}
