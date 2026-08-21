using System;
using System.Collections.Generic;
using SoundFXStudio.Models;

namespace SoundFXStudio.Services.Hrtf;

/// <summary>
/// Prepares HrtfProfile instances for a target DSP sample rate by resampling HRIRs as needed.
/// Original imported profiles are never modified.
///
/// Cache: keyed by (profileId, sourceSampleRate, targetSampleRate).
/// Same profile + same target rate → cached result (no redundant resampling).
/// Changing target rate or profile → new cache entry.
/// </summary>
public sealed class HrtfProfilePreparer
{
    private readonly Dictionary<CacheKey, HrtfProfile> _cache = new();

    /// <summary>
    /// Prepares an HrtfProfile for the target DSP sample rate.
    /// If profile.SampleRate == targetSampleRate, returns a safe deep clone.
    /// If they differ, resamples all HRIRs and returns a new profile.
    /// The original profile is never modified.
    /// </summary>
    public HrtfProfile Prepare(HrtfProfile profile, int targetSampleRate)
    {
        if (profile is null)
            throw new ArgumentNullException(nameof(profile));

        var key = new CacheKey(profile.Id, profile.SampleRate, targetSampleRate);

        if (_cache.TryGetValue(key, out var cached))
            return cached;

        HrtfProfile prepared;

        if (profile.SampleRate == targetSampleRate)
        {
            // No resampling needed — deep clone preserves isolation
            prepared = profile.Clone();
        }
        else
        {
            prepared = ResampleProfile(profile, targetSampleRate);
        }

        _cache[key] = prepared;
        return prepared;
    }

    /// <summary>
    /// Clears the preparation cache. Call when profiles are deleted or sample rates change globally.
    /// </summary>
    public void ClearCache()
    {
        _cache.Clear();
    }

    /// <summary>
    /// Removes a specific profile from the cache.
    /// </summary>
    public void Invalidate(string profileId)
    {
        var keysToRemove = new List<CacheKey>();
        foreach (var key in _cache.Keys)
        {
            if (string.Equals(key.ProfileId, profileId, StringComparison.Ordinal))
                keysToRemove.Add(key);
        }
        foreach (var key in keysToRemove)
            _cache.Remove(key);
    }

    private static HrtfProfile ResampleProfile(HrtfProfile source, int targetSampleRate)
    {
        var sourceRate = source.SampleRate;
        var entries = new HrtfEntry[source.Entries.Length];

        for (int i = 0; i < source.Entries.Length; i++)
        {
            var srcEntry = source.Entries[i];
            var leftResampled = HrirResampler.Resample(srcEntry.LeftEarResponse, sourceRate, targetSampleRate);
            var rightResampled = HrirResampler.Resample(srcEntry.RightEarResponse, sourceRate, targetSampleRate);

            entries[i] = new HrtfEntry
            {
                AzimuthDeg = srcEntry.AzimuthDeg,
                ElevationDeg = srcEntry.ElevationDeg,
                LeftEarResponse = leftResampled,
                RightEarResponse = rightResampled
            };
        }

        // New IR length from the first entry (all entries have same length by convention)
        var newIrLength = entries.Length > 0 ? entries[0].LeftEarResponse.Length : 0;

        return new HrtfProfile
        {
            Id = source.Id,
            Name = source.Name,
            Manufacturer = source.Manufacturer,
            Description = source.Description,
            SampleRate = targetSampleRate,
            IrLength = newIrLength,
            Entries = entries,
            DataSource = source.DataSource,
            License = source.License
        };
    }

    private readonly record struct CacheKey(string ProfileId, int SourceRate, int TargetRate);
}
