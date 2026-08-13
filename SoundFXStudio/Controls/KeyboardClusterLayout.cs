namespace SoundFXStudio.Controls;

public enum KeyboardCluster
{
    EscCluster,
    F1ToF4Cluster,
    F5ToF8Cluster,
    F9ToF12Cluster,
    PrintScrollPauseCluster,
    MainLettersCluster,
    MainTypingCluster,
    NavigationCluster,
    ArrowCluster,
    NumpadCluster
}

public sealed class KeyboardClusterCalibration
{
    public double OffsetX { get; set; }
    public double OffsetY { get; set; }
    public double WidthAdjustment { get; set; }
    public double HeightAdjustment { get; set; }
}

public static class KeyboardClusterLayout
{
    private static readonly Dictionary<KeyboardCluster, KeyboardClusterCalibration> ClusterCalibrations = new();

    public static event Action? Changed;

    public static KeyboardClusterCalibration Get(KeyboardCluster cluster)
        => ClusterCalibrations.TryGetValue(cluster, out var calibration)
            ? calibration
            : new KeyboardClusterCalibration();

    public static void Set(KeyboardCluster cluster, double offsetX, double offsetY, double widthAdjustment = 0, double heightAdjustment = 0)
    {
        ClusterCalibrations[cluster] = new KeyboardClusterCalibration
        {
            OffsetX = offsetX,
            OffsetY = offsetY,
            WidthAdjustment = widthAdjustment,
            HeightAdjustment = heightAdjustment
        };

        Changed?.Invoke();
    }

    public static void Reset()
    {
        ClusterCalibrations.Clear();
        Changed?.Invoke();
    }

    public static void ApplyPreset(
        double escOffsetX,
        double escOffsetY,
        double f1ToF4OffsetX,
        double f1ToF4OffsetY,
        double f5ToF8OffsetX,
        double f5ToF8OffsetY,
        double f9ToF12OffsetX,
        double f9ToF12OffsetY,
        double printScrollPauseOffsetX = 0,
        double printScrollPauseOffsetY = 0,
        double mainTypingOffsetX = 0,
        double mainTypingOffsetY = 0,
        double navigationOffsetX = 0,
        double navigationOffsetY = 0,
        double arrowOffsetX = 0,
        double arrowOffsetY = 0,
        double numpadOffsetX = 0,
        double numpadOffsetY = 0,
        double mainLettersOffsetX = 0,
        double mainLettersOffsetY = 0,
        double mainLettersWidthAdjustment = 0,
        double mainLettersHeightAdjustment = 0,
        double escWidthAdjustment = 0,
        double escHeightAdjustment = 0,
        double f1ToF4WidthAdjustment = 0,
        double f1ToF4HeightAdjustment = 0,
        double f5ToF8WidthAdjustment = 0,
        double f5ToF8HeightAdjustment = 0,
        double f9ToF12WidthAdjustment = 0,
        double f9ToF12HeightAdjustment = 0,
        double printScrollPauseWidthAdjustment = 0,
        double printScrollPauseHeightAdjustment = 0,
        double mainTypingWidthAdjustment = 0,
        double mainTypingHeightAdjustment = 0,
        double navigationWidthAdjustment = 0,
        double navigationHeightAdjustment = 0,
        double arrowWidthAdjustment = 0,
        double arrowHeightAdjustment = 0,
        double numpadWidthAdjustment = 0,
        double numpadHeightAdjustment = 0)
    {
        ClusterCalibrations[KeyboardCluster.EscCluster] = new KeyboardClusterCalibration { OffsetX = escOffsetX, OffsetY = escOffsetY, WidthAdjustment = escWidthAdjustment, HeightAdjustment = escHeightAdjustment };
        ClusterCalibrations[KeyboardCluster.F1ToF4Cluster] = new KeyboardClusterCalibration { OffsetX = f1ToF4OffsetX, OffsetY = f1ToF4OffsetY, WidthAdjustment = f1ToF4WidthAdjustment, HeightAdjustment = f1ToF4HeightAdjustment };
        ClusterCalibrations[KeyboardCluster.F5ToF8Cluster] = new KeyboardClusterCalibration { OffsetX = f5ToF8OffsetX, OffsetY = f5ToF8OffsetY, WidthAdjustment = f5ToF8WidthAdjustment, HeightAdjustment = f5ToF8HeightAdjustment };
        ClusterCalibrations[KeyboardCluster.F9ToF12Cluster] = new KeyboardClusterCalibration { OffsetX = f9ToF12OffsetX, OffsetY = f9ToF12OffsetY, WidthAdjustment = f9ToF12WidthAdjustment, HeightAdjustment = f9ToF12HeightAdjustment };
        ClusterCalibrations[KeyboardCluster.PrintScrollPauseCluster] = new KeyboardClusterCalibration { OffsetX = printScrollPauseOffsetX, OffsetY = printScrollPauseOffsetY, WidthAdjustment = printScrollPauseWidthAdjustment, HeightAdjustment = printScrollPauseHeightAdjustment };
        ClusterCalibrations[KeyboardCluster.MainTypingCluster] = new KeyboardClusterCalibration { OffsetX = mainTypingOffsetX, OffsetY = mainTypingOffsetY, WidthAdjustment = mainTypingWidthAdjustment, HeightAdjustment = mainTypingHeightAdjustment };
        ClusterCalibrations[KeyboardCluster.NavigationCluster] = new KeyboardClusterCalibration { OffsetX = navigationOffsetX, OffsetY = navigationOffsetY, WidthAdjustment = navigationWidthAdjustment, HeightAdjustment = navigationHeightAdjustment };
        ClusterCalibrations[KeyboardCluster.ArrowCluster] = new KeyboardClusterCalibration { OffsetX = arrowOffsetX, OffsetY = arrowOffsetY, WidthAdjustment = arrowWidthAdjustment, HeightAdjustment = arrowHeightAdjustment };
        ClusterCalibrations[KeyboardCluster.NumpadCluster] = new KeyboardClusterCalibration { OffsetX = numpadOffsetX, OffsetY = numpadOffsetY, WidthAdjustment = numpadWidthAdjustment, HeightAdjustment = numpadHeightAdjustment };
        ClusterCalibrations[KeyboardCluster.MainLettersCluster] = new KeyboardClusterCalibration { OffsetX = mainLettersOffsetX, OffsetY = mainLettersOffsetY, WidthAdjustment = mainLettersWidthAdjustment, HeightAdjustment = mainLettersHeightAdjustment };
        Changed?.Invoke();
    }
}