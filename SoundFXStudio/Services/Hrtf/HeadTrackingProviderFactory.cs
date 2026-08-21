namespace SoundFXStudio.Services.Hrtf;

/// <summary>
/// Creates head-tracking providers based on configuration.
/// Ensures NullHeadTrackingProvider is always available as fallback.
/// </summary>
public sealed class HeadTrackingProviderFactory
{
    /// <summary>
    /// Known provider identifiers.
    /// </summary>
    public const string OpenTrackProviderId = "opentrack";
    public const string NullProviderId = "none";

    /// <summary>
    /// Creates a provider for the given ID.
    /// Falls back to NullHeadTrackingProvider for unrecognized IDs.
    /// </summary>
    public IHeadTrackingProvider Create(string providerId, OpenTrackHeadTrackingOptions? options = null)
    {
        return providerId?.ToLowerInvariant() switch
        {
            OpenTrackProviderId => new OpenTrackHeadTrackingProvider(options),
            NullProviderId or "null" => new NullHeadTrackingProvider(),
            _ => new NullHeadTrackingProvider()
        };
    }

    /// <summary>
    /// Gets the default provider ID.
    /// </summary>
    public static string DefaultProviderId => OpenTrackProviderId;
}
