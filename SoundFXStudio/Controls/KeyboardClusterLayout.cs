namespace SoundFXStudio.Controls;

public enum KeyboardCluster
{
    EscCluster,
    F1ToF4Cluster,
    F5ToF8Cluster,
    F9ToF12Cluster,
    PrintScrollPauseCluster,
    MainTypingCluster,
    NavigationCluster,
    ArrowCluster,
    NumpadCluster
}

public sealed class KeyboardClusterCalibration
{
    public double OffsetX { get; set; }
    public double OffsetY { get; set; }
}

public static class KeyboardClusterLayout
{
    private static readonly Dictionary<KeyboardCluster, KeyboardClusterCalibration> ClusterCalibrations = new();

    public static event Action? Changed;

    public static KeyboardClusterCalibration Get(KeyboardCluster cluster)
        => ClusterCalibrations.TryGetValue(cluster, out var calibration)
            ? calibration
            : new KeyboardClusterCalibration();

    public static void Set(KeyboardCluster cluster, double offsetX, double offsetY)
    {
        ClusterCalibrations[cluster] = new KeyboardClusterCalibration
        {
            OffsetX = offsetX,
            OffsetY = offsetY
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
        double numpadOffsetY = 0)
    {
        ClusterCalibrations[KeyboardCluster.EscCluster] = new KeyboardClusterCalibration { OffsetX = escOffsetX, OffsetY = escOffsetY };
        ClusterCalibrations[KeyboardCluster.F1ToF4Cluster] = new KeyboardClusterCalibration { OffsetX = f1ToF4OffsetX, OffsetY = f1ToF4OffsetY };
        ClusterCalibrations[KeyboardCluster.F5ToF8Cluster] = new KeyboardClusterCalibration { OffsetX = f5ToF8OffsetX, OffsetY = f5ToF8OffsetY };
        ClusterCalibrations[KeyboardCluster.F9ToF12Cluster] = new KeyboardClusterCalibration { OffsetX = f9ToF12OffsetX, OffsetY = f9ToF12OffsetY };
        ClusterCalibrations[KeyboardCluster.PrintScrollPauseCluster] = new KeyboardClusterCalibration { OffsetX = printScrollPauseOffsetX, OffsetY = printScrollPauseOffsetY };
        ClusterCalibrations[KeyboardCluster.MainTypingCluster] = new KeyboardClusterCalibration { OffsetX = mainTypingOffsetX, OffsetY = mainTypingOffsetY };
        ClusterCalibrations[KeyboardCluster.NavigationCluster] = new KeyboardClusterCalibration { OffsetX = navigationOffsetX, OffsetY = navigationOffsetY };
        ClusterCalibrations[KeyboardCluster.ArrowCluster] = new KeyboardClusterCalibration { OffsetX = arrowOffsetX, OffsetY = arrowOffsetY };
        ClusterCalibrations[KeyboardCluster.NumpadCluster] = new KeyboardClusterCalibration { OffsetX = numpadOffsetX, OffsetY = numpadOffsetY };
        Changed?.Invoke();
    }
}