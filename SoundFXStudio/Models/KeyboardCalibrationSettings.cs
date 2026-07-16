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
    public double KeyboardWindowScale { get; set; } = 1.0;
    public bool DebugCalibration { get; set; }

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
