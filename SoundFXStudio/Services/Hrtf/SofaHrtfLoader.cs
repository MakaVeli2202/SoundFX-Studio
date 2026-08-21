using System.Globalization;
using System.IO;
using System.Text;
using SoundFXStudio.Models;
using SwatRecords.NetCDF;
using SwatRecords.NetCDF.Model;

namespace SoundFXStudio.Services.Hrtf;

/// <summary>
/// Loads HRTF profiles from SOFA (SimpleFreeFieldHRIR convention) files
/// using SwatRecords.NetCDF for NetCDF4/HDF5 parsing.
///
/// Expected SOFA variables:
///   Data.IR[M, R, N]       — HRIR data (M=measurements, R=receivers, N=IR length)
///   Data.SamplingRate       — sample rate (scalar or 1-element array)
///   SourcePosition[M, C]   — source positions (C>=3: azimuth, elevation, distance)
///
/// Coordinate convention: spherical degrees (azimuth 0=front, positive=left).
/// Receiver 0 = left ear, Receiver 1 = right ear.
/// </summary>
public sealed class SofaHrtfLoader : ISofaHrtfLoader
{
    private const double MinSampleRate = 8000;
    private const double MaxSampleRate = 192000;

    public SofaHrtfLoadResult Load(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return SofaHrtfLoadResult.Fail("File path is empty.");

        if (!File.Exists(filePath))
            return SofaHrtfLoadResult.Fail($"File not found: {filePath}");

        INetCdfFile? file = null;
        try
        {
            file = NetCdfFile.Open(filePath);
            return ParseSofaFile(file, filePath);
        }
        catch (Exception ex)
        {
            return SofaHrtfLoadResult.Fail($"Failed to open SOFA file: {ex.Message}");
        }
        finally
        {
            (file as IDisposable)?.Dispose();
        }
    }

    private static SofaHrtfLoadResult ParseSofaFile(INetCdfFile file, string filePath)
    {
        // --- Validate required variables ---
        if (!file.Variables.TryGetValue("Data.IR", out var irVariable))
            return SofaHrtfLoadResult.Fail("Missing required variable: Data.IR");

        if (!file.Variables.TryGetValue("SourcePosition", out var sourcePosVariable))
            return SofaHrtfLoadResult.Fail("Missing required variable: SourcePosition");

        if (!file.Variables.TryGetValue("Data.SamplingRate", out var samplingRateVariable))
            return SofaHrtfLoadResult.Fail("Missing required variable: Data.SamplingRate");

        // --- Validate Data.IR shape ---
        if (irVariable.Rank != 3)
            return SofaHrtfLoadResult.Fail($"Data.IR must be 3-dimensional [M,R,N], got rank {irVariable.Rank}");

        int m = (int)irVariable.Shape[0]; // measurements
        int r = (int)irVariable.Shape[1]; // receivers
        int n = (int)irVariable.Shape[2]; // IR length

        if (m <= 0)
            return SofaHrtfLoadResult.Fail("Data.IR has zero measurements (M=0).");
        if (n <= 0)
            return SofaHrtfLoadResult.Fail("Data.IR has zero IR length (N=0).");
        if (r != 2)
            return SofaHrtfLoadResult.Fail($"Data.IR must have exactly 2 receivers (left/right ear), got {r}.");

        // --- Read sample rate ---
        var sampleRate = ReadSampleRate(samplingRateVariable);
        if (sampleRate is null)
            return SofaHrtfLoadResult.Fail("Cannot read Data.SamplingRate.");
        if (sampleRate < MinSampleRate || sampleRate > MaxSampleRate)
            return SofaHrtfLoadResult.Fail($"Data.SamplingRate={sampleRate} is outside supported range [{MinSampleRate}, {MaxSampleRate}].");

        // --- Read SourcePosition metadata ---
        var coordResult = ReadCoordinateMetadata(sourcePosVariable);
        if (coordResult is not null)
            return SofaHrtfLoadResult.Fail(coordResult);

        // --- Read all SourcePosition data ---
        double[] positions;
        try
        {
            positions = sourcePosVariable.Read<double>();
        }
        catch (Exception ex)
        {
            return SofaHrtfLoadResult.Fail($"Cannot read SourcePosition data: {ex.Message}");
        }

        if (positions.Length < m * 3)
            return SofaHrtfLoadResult.Fail($"SourcePosition has {positions.Length / 3} entries but Data.IR expects {m}.");

        // --- Read all IR data ---
        double[] irData;
        try
        {
            irData = irVariable.Read<double>();
        }
        catch (Exception ex)
        {
            return SofaHrtfLoadResult.Fail($"Cannot read Data.IR data: {ex.Message}");
        }

        if (irData.Length < m * r * n)
            return SofaHrtfLoadResult.Fail($"Data.IR has {irData.Length} elements but expected at least {m * r * n}.");

        // --- Read global metadata ---
        var title = ReadGlobalStringAttribute(file, "Title");
        var databaseName = ReadGlobalStringAttribute(file, "DatabaseName");
        var listenerShortName = ReadGlobalStringAttribute(file, "ListenerShortName");
        var license = ReadGlobalStringAttribute(file, "License");
        var origin = ReadGlobalStringAttribute(file, "Origin");

        // --- Build entries ---
        var entries = new HrtfEntry[m];
        int validCount = 0;

        for (int i = 0; i < m; i++)
        {
            // SOFA convention: azimuth 0-360 (positive=left). Normalize to (-180,180].
            var azimuth = positions[i * 3];
            if (azimuth > 180.0)
                azimuth -= 360.0;
            var elevation = positions[i * 3 + 1];

            var leftIr = new float[n];
            var rightIr = new float[n];

            // Data.IR layout: [measurement, receiver, sample]
            // Receiver 0 = left ear, Receiver 1 = right ear
            int leftBase = (i * r + 0) * n;
            int rightBase = (i * r + 1) * n;

            bool valid = true;
            for (int s = 0; s < n; s++)
            {
                var leftVal = irData[leftBase + s];
                var rightVal = irData[rightBase + s];

                if (!double.IsFinite(leftVal) || !double.IsFinite(rightVal))
                {
                    valid = false;
                    break;
                }

                leftIr[s] = (float)leftVal;
                rightIr[s] = (float)rightVal;
            }

            if (!valid)
                continue;

            entries[validCount++] = new HrtfEntry
            {
                AzimuthDeg = azimuth,
                ElevationDeg = elevation,
                LeftEarResponse = leftIr,
                RightEarResponse = rightIr
            };
        }

        if (validCount == 0)
            return SofaHrtfLoadResult.Fail("No valid HRIR entries found (all contained NaN or Infinity).");

        // Trim entries array if some were invalid
        if (validCount < m)
        {
            var trimmed = new HrtfEntry[validCount];
            Array.Copy(entries, trimmed, validCount);
            entries = trimmed;
        }

        // --- Build profile name ---
        var profileName = BuildProfileName(title, databaseName, listenerShortName, Path.GetFileNameWithoutExtension(filePath));

        var profile = new HrtfProfile
        {
            Id = $"sofa-{Guid.NewGuid():N}",
            Name = profileName,
            Manufacturer = databaseName ?? "Unknown",
            Description = $"Imported SOFA HRTF profile. {validCount} directions, {n} taps, {sampleRate} Hz."
                + (string.IsNullOrEmpty(listenerShortName) ? "" : $" Listener: {listenerShortName}."),
            SampleRate = (int)sampleRate,
            IrLength = n,
            Entries = entries,
            DataSource = $"SOFA Import — {origin ?? "Unknown source"}",
            License = license ?? "Unknown license"
        };

        return SofaHrtfLoadResult.Ok(profile);
    }

    private static double? ReadSampleRate(INetCdfVariable variable)
    {
        try
        {
            var data = variable.Read<double>();
            return data.Length > 0 ? data[0] : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? ReadCoordinateMetadata(INetCdfVariable sourcePosVariable)
    {
        string? typeValue = null;
        string? unitsValue = null;

        foreach (var attr in sourcePosVariable.Attributes)
        {
            if (string.Equals(attr.Key, "Type", StringComparison.OrdinalIgnoreCase))
            {
                typeValue = ReadAttributeStringValue(attr.Value);
            }
            else if (string.Equals(attr.Key, "Units", StringComparison.OrdinalIgnoreCase))
            {
                unitsValue = ReadAttributeStringValue(attr.Value);
            }
        }

        if (!string.IsNullOrEmpty(typeValue))
        {
            var typeLower = typeValue.ToLowerInvariant();
            if (!typeLower.Contains("spherical") && !typeLower.Contains("deg"))
            {
                return $"Unsupported SourcePosition Type: '{typeValue}'. Only spherical coordinates in degrees are supported.";
            }
        }

        if (!string.IsNullOrEmpty(unitsValue))
        {
            var unitsLower = unitsValue.ToLowerInvariant();
            if (!unitsLower.Contains("deg") && !unitsLower.Contains("degree"))
            {
                return $"Unsupported SourcePosition Units: '{unitsValue}'. Only degrees are supported.";
            }
        }

        return null;
    }

    private static string? ReadAttributeStringValue(INetCdfAttribute? attr)
    {
        if (attr is null) return null;

        try
        {
            var val = attr.GetValue<string>();
            if (!string.IsNullOrEmpty(val)) return val;
        }
        catch { }

        try
        {
            var val = attr.GetValue();
            if (val is null) return null;
            var str = val.ToString();
            if (!string.IsNullOrEmpty(str) && !str.Contains("Attribute")) return str;
        }
        catch { }

        return null;
    }

    private static string? ReadGlobalStringAttribute(INetCdfFile file, string attributeName)
    {
        if (!file.Attributes.TryGetValue(attributeName, out var attr))
            return null;

        return ReadAttributeStringValue(attr);
    }

    private static string BuildProfileName(string? title, string? databaseName, string? listenerName, string fallbackName)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(title))
            parts.Add(title.Trim());
        else if (!string.IsNullOrWhiteSpace(databaseName))
            parts.Add(databaseName.Trim());

        if (!string.IsNullOrWhiteSpace(listenerName))
            parts.Add($"({listenerName.Trim()})");

        return parts.Count > 0
            ? string.Join(" ", parts)
            : fallbackName;
    }
}
