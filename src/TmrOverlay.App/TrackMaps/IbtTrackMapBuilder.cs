using System.Buffers.Binary;
using TmrOverlay.App.Telemetry;
using TmrOverlay.Core.History;
using TmrOverlay.Core.TrackMaps;

namespace TmrOverlay.App.TrackMaps;

internal sealed class IbtTrackMapBuilder
{
    private const double EarthRadiusMeters = 6_371_000d;
    private const double MinimumMovingMetersPerSecond = 2.0d;
    private const int MinimumCompleteLapSamples = 160;
    private const int MinimumGeneratedBins = 400;
    private const int MaximumGeneratedBins = 5_500;

    public HistoricalTrackIdentity ReadTrackIdentity(string ibtPath, CancellationToken cancellationToken)
    {
        var ibt = IbtTelemetryFile.Read(ibtPath, cancellationToken);
        return string.IsNullOrWhiteSpace(ibt.SessionInfoYaml)
            ? HistoricalTrackIdentityFromPath(ibtPath)
            : SessionInfoSummaryParser.Parse(ibt.SessionInfoYaml).Track;
    }

    public TrackMapBuildResult BuildFromIbt(
        string ibtPath,
        string? captureId,
        CancellationToken cancellationToken)
    {
        var fileInfo = new FileInfo(ibtPath);
        var ibt = IbtTelemetryFile.Read(ibtPath, cancellationToken);
        var context = string.IsNullOrWhiteSpace(ibt.SessionInfoYaml)
            ? HistoricalSessionContext.Empty
            : SessionInfoSummaryParser.Parse(ibt.SessionInfoYaml);
        var fields = RequiredFields.From(ibt.Fields);
        if (fields.MissingReasons.Count > 0)
        {
            return TrackMapBuildResult.Rejected(fields.MissingReasons);
        }

        var samples = ReadSamples(ibtPath, ibt, fields, cancellationToken);
        return BuildFromSamples(
            samples,
            string.IsNullOrWhiteSpace(ibt.SessionInfoYaml) ? HistoricalTrackIdentityFromPath(ibtPath) : context.Track,
            new TrackMapProvenance(
                SourceKind: "ibt",
                SourcePath: ibtPath,
                SourceBytes: fileInfo.Exists ? fileInfo.Length : null,
                SourceRecordCount: ibt.DiskHeader.RecordCount,
                CaptureId: captureId));
    }

    private static HistoricalTrackIdentity HistoricalTrackIdentityFromPath(string path)
    {
        return new HistoricalTrackIdentity
        {
            TrackName = Path.GetFileNameWithoutExtension(path)
        };
    }

    internal TrackMapBuildResult BuildFromSamples(
        IReadOnlyList<IbtTrackMapSample> samples,
        HistoricalTrackIdentity track,
        TrackMapProvenance provenance)
    {
        var identity = TrackMapIdentity.From(track);
        var racingSamples = samples
            .Where(sample => sample.LapNumber > 0
                && !sample.OnPitRoad
                && IsFinite(sample.Latitude)
                && IsFinite(sample.Longitude)
                && IsValidLapDistPct(sample.LapDistPct)
                && sample.SpeedMetersPerSecond >= MinimumMovingMetersPerSecond)
            .OrderBy(sample => sample.Sequence)
            .ToArray();
        if (racingSamples.Length == 0)
        {
            return TrackMapBuildResult.Rejected(["no_valid_moving_racing_line_samples"]);
        }

        var completeLaps = racingSamples
            .GroupBy(sample => sample.LapNumber)
            .Select(group => new CompleteLapCandidate(
                LapNumber: group.Key,
                Samples: group.OrderBy(sample => sample.Sequence).ToArray(),
                MinPct: group.Min(sample => sample.LapDistPct),
                MaxPct: group.Max(sample => sample.LapDistPct),
                LapDistanceMeters: LapDistanceMeters(group)))
            .Where(lap => lap.Samples.Count >= MinimumCompleteLapSamples
                && lap.MinPct <= 0.06d
                && lap.MaxPct >= 0.94d)
            .OrderBy(lap => lap.LapNumber)
            .ToArray();
        if (completeLaps.Length == 0)
        {
            return TrackMapBuildResult.Rejected(["no_complete_positive_lap"]);
        }

        var selectedSamples = completeLaps.SelectMany(lap => lap.Samples).ToArray();
        var origin = ProjectionOrigin.From(selectedSamples);
        var projected = selectedSamples
            .Select(sample => ProjectedSample.From(sample, origin))
            .ToArray();
        var binCount = BinCount(track, completeLaps);
        var binned = BuildBinnedLine(projected, binCount);
        var missingBinCount = binned.Count(point => point is null);
        var interpolated = InterpolateMissingBins(binned);
        if (interpolated.Count < MinimumGeneratedBins)
        {
            return TrackMapBuildResult.Rejected(["insufficient_generated_bins"]);
        }

        var closureMeters = Distance(interpolated[0], interpolated[^1]);
        var lineLengthMeters = PolylineLength(interpolated, closed: true);
        var targetLengthMeters = TargetLengthMeters(track, completeLaps);
        var lengthDeltaPercent = targetLengthMeters is { } target && target > 0d
            ? (lineLengthMeters - target) / target
            : (double?)null;
        var spread = Repeatability(projected, interpolated, binCount);
        var pitLane = BuildPitLane(samples, origin);
        var confidence = TrackMapQualityClassifier.Classify(
            completeLaps.Length,
            missingBinCount / (double)binCount,
            closureMeters,
            lengthDeltaPercent,
            spread.P95Meters);
        var reasons = QualityReasons(confidence, completeLaps.Length, missingBinCount, closureMeters, lengthDeltaPercent, spread.P95Meters);

        var document = new TrackMapDocument(
            SchemaVersion: TrackMapDocument.CurrentSchemaVersion,
            GenerationVersion: TrackMapDocument.CurrentGenerationVersion,
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            Identity: identity,
            RacingLine: new TrackMapGeometry(
                Points: interpolated.Select((point, index) => new TrackMapPoint(
                    LapDistPct: index / (double)interpolated.Count,
                    X: Math.Round(point.X, 3),
                    Y: Math.Round(point.Y, 3),
                    DistanceMeters: Math.Round(lineLengthMeters * index / interpolated.Count, 3))).ToArray(),
                Closed: true),
            PitLane: pitLane,
            Quality: new TrackMapQuality(
                Confidence: confidence,
                CompleteLapCount: completeLaps.Length,
                SelectedPointCount: selectedSamples.Length,
                BinCount: binCount,
                MissingBinCount: missingBinCount,
                MissingBinPercent: Math.Round(missingBinCount / (double)binCount, 5),
                ClosureMeters: Math.Round(closureMeters, 3),
                LengthDeltaPercent: lengthDeltaPercent is null ? null : Math.Round(lengthDeltaPercent.Value, 5),
                RepeatabilityMedianMeters: spread.MedianMeters is null ? null : Math.Round(spread.MedianMeters.Value, 3),
                RepeatabilityP95Meters: spread.P95Meters is null ? null : Math.Round(spread.P95Meters.Value, 3),
                PitLaneSampleCount: pitLane?.Points.Count ?? 0,
                PitLanePassCount: PitLanePassCount(samples),
                PitLaneRepeatabilityP95Meters: null,
                Reasons: reasons),
            Provenance: provenance);
        return new TrackMapBuildResult(document, []);
    }

    private static IReadOnlyList<IbtTrackMapSample> ReadSamples(
        string path,
        IbtTelemetryFile ibt,
        RequiredFields fields,
        CancellationToken cancellationToken)
    {
        var samples = new List<IbtTrackMapSample>(Math.Max(0, ibt.DiskHeader.RecordCount - 1));
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var payload = new byte[ibt.Header.BufferLength];
        for (var recordIndex = 1; recordIndex < ibt.DiskHeader.RecordCount; recordIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var offset = ibt.Header.BufferOffset + (long)recordIndex * ibt.Header.BufferLength;
            if (offset < 0 || offset + ibt.Header.BufferLength > stream.Length)
            {
                break;
            }

            stream.Position = offset;
            ReadExact(stream, payload);
            if (ReadDouble(payload, fields.Latitude) is not { } latitude
                || ReadDouble(payload, fields.Longitude) is not { } longitude
                || ReadDouble(payload, fields.LapDistPct) is not { } lapDistPct)
            {
                continue;
            }

            var lapNumber = ReadInt(payload, fields.LapCompleted) ?? ReadInt(payload, fields.Lap) ?? -1;
            samples.Add(new IbtTrackMapSample(
                Sequence: recordIndex,
                LapNumber: lapNumber,
                LapDistPct: lapDistPct,
                LapDistMeters: ReadDouble(payload, fields.LapDist),
                Latitude: latitude,
                Longitude: longitude,
                SpeedMetersPerSecond: ReadDouble(payload, fields.Speed) ?? 0d,
                OnPitRoad: ReadBool(payload, fields.OnPitRoad)));
        }

        return samples;
    }

    private static int BinCount(HistoricalTrackIdentity track, IReadOnlyList<CompleteLapCandidate> laps)
    {
        var targetMeters = TargetLengthMeters(track, laps) ?? 3_600d;
        return Math.Clamp((int)Math.Round(targetMeters / 3d), MinimumGeneratedBins, MaximumGeneratedBins);
    }

    private static double? TargetLengthMeters(HistoricalTrackIdentity track, IReadOnlyList<CompleteLapCandidate> laps)
    {
        var lapDistance = Median(laps
            .Select(lap => lap.LapDistanceMeters)
            .Where(value => value is not null)
            .Select(value => value!.Value)
            .ToArray());
        if (lapDistance is { } distance && distance > 0d)
        {
            return distance;
        }

        return track.TrackLengthKm is { } km && IsFinite(km) && km > 0d ? km * 1000d : null;
    }

    private static double? LapDistanceMeters(IEnumerable<IbtTrackMapSample> samples)
    {
        var values = samples
            .Select(sample => sample.LapDistMeters)
            .Where(value => value is not null && IsFinite(value.Value))
            .Select(value => value!.Value)
            .ToArray();
        return values.Length == 0 ? null : values.Max() - values.Min();
    }

    private static ProjectedPoint?[] BuildBinnedLine(IReadOnlyList<ProjectedSample> samples, int binCount)
    {
        var bins = new List<ProjectedSample>[binCount];
        foreach (var sample in samples)
        {
            var index = BinIndex(sample.LapDistPct, binCount);
            bins[index] ??= [];
            bins[index].Add(sample);
        }

        var points = new ProjectedPoint?[binCount];
        for (var index = 0; index < bins.Length; index++)
        {
            if (bins[index] is not { Count: > 0 } bin)
            {
                continue;
            }

            points[index] = new ProjectedPoint(
                MedianRequired(bin.Select(sample => sample.X).ToArray()),
                MedianRequired(bin.Select(sample => sample.Y).ToArray()));
        }

        return points;
    }

    private static IReadOnlyList<ProjectedPoint> InterpolateMissingBins(IReadOnlyList<ProjectedPoint?> points)
    {
        var knownIndexes = points
            .Select((point, index) => new { point, index })
            .Where(item => item.point is not null)
            .Select(item => item.index)
            .ToArray();
        if (knownIndexes.Length == 0)
        {
            return [];
        }

        var result = new ProjectedPoint[points.Count];
        foreach (var index in knownIndexes)
        {
            result[index] = points[index]!;
        }

        for (var index = 0; index < points.Count; index++)
        {
            if (points[index] is not null)
            {
                continue;
            }

            var previous = PreviousKnown(index, knownIndexes, points.Count);
            var next = NextKnown(index, knownIndexes, points.Count);
            var span = next > previous ? next - previous : next + points.Count - previous;
            var offset = index >= previous ? index - previous : index + points.Count - previous;
            var t = span <= 0 ? 0d : offset / (double)span;
            var start = points[previous]!;
            var end = points[next]!;
            result[index] = new ProjectedPoint(
                start.X + (end.X - start.X) * t,
                start.Y + (end.Y - start.Y) * t);
        }

        return result;
    }

    private static int PreviousKnown(int index, IReadOnlyList<int> knownIndexes, int count)
    {
        return knownIndexes.LastOrDefault(known => known < index, knownIndexes[^1]);
    }

    private static int NextKnown(int index, IReadOnlyList<int> knownIndexes, int count)
    {
        return knownIndexes.FirstOrDefault(known => known > index, knownIndexes[0]);
    }

    private static TrackMapGeometry? BuildPitLane(IReadOnlyList<IbtTrackMapSample> samples, ProjectionOrigin origin)
    {
        var passes = new List<List<IbtTrackMapSample>>();
        List<IbtTrackMapSample>? current = null;
        foreach (var sample in samples.OrderBy(sample => sample.Sequence))
        {
            var usable = sample.OnPitRoad
                && sample.SpeedMetersPerSecond >= 1d
                && IsFinite(sample.Latitude)
                && IsFinite(sample.Longitude);
            if (!usable)
            {
                if (current is { Count: >= 20 })
                {
                    passes.Add(current);
                }

                current = null;
                continue;
            }

            current ??= [];
            current.Add(sample);
        }

        if (current is { Count: >= 20 })
        {
            passes.Add(current);
        }

        var best = passes.OrderByDescending(pass => pass.Count).FirstOrDefault();
        if (best is null)
        {
            return null;
        }

        var stride = Math.Max(1, (int)Math.Ceiling(best.Count / 300d));
        var points = best
            .Where((_, index) => index % stride == 0)
            .Select((sample, index) =>
            {
                var point = Project(sample.Latitude, sample.Longitude, origin);
                return new TrackMapPoint(index / (double)Math.Max(1, best.Count - 1), Math.Round(point.X, 3), Math.Round(point.Y, 3));
            })
            .ToArray();
        return points.Length >= 2 ? new TrackMapGeometry(points, Closed: false) : null;
    }

    private static int PitLanePassCount(IReadOnlyList<IbtTrackMapSample> samples)
    {
        var count = 0;
        var current = 0;
        foreach (var sample in samples.OrderBy(sample => sample.Sequence))
        {
            if (sample.OnPitRoad && sample.SpeedMetersPerSecond >= 1d)
            {
                current++;
                continue;
            }

            if (current >= 20)
            {
                count++;
            }

            current = 0;
        }

        return count + (current >= 20 ? 1 : 0);
    }

    private static RepeatabilitySummary Repeatability(
        IReadOnlyList<ProjectedSample> samples,
        IReadOnlyList<ProjectedPoint> line,
        int binCount)
    {
        var distances = samples
            .Select(sample => Distance(sample, line[BinIndex(sample.LapDistPct, binCount)]))
            .Where(IsFinite)
            .OrderBy(value => value)
            .ToArray();
        return distances.Length == 0
            ? new RepeatabilitySummary(null, null)
            : new RepeatabilitySummary(MedianRequired(distances), Percentile(distances, 0.95d));
    }

    private static IReadOnlyList<string> QualityReasons(
        TrackMapConfidence confidence,
        int completeLapCount,
        int missingBinCount,
        double closureMeters,
        double? lengthDeltaPercent,
        double? repeatabilityP95Meters)
    {
        var reasons = new List<string> { $"confidence_{confidence.ToString().ToLowerInvariant()}" };
        if (completeLapCount < 5)
        {
            reasons.Add("limited_complete_laps");
        }

        if (missingBinCount > 0)
        {
            reasons.Add("missing_bins_interpolated");
        }

        if (closureMeters > 10d)
        {
            reasons.Add("large_closure_error");
        }

        if (lengthDeltaPercent is { } lengthDelta && Math.Abs(lengthDelta) > 0.015d)
        {
            reasons.Add("length_delta_above_high_confidence_threshold");
        }

        if (repeatabilityP95Meters is { } p95 && p95 > 3.5d)
        {
            reasons.Add("repeatability_p95_above_high_confidence_threshold");
        }

        return reasons;
    }

    private static ProjectedPoint Project(double latitude, double longitude, ProjectionOrigin origin)
    {
        var latRad = DegreesToRadians(latitude);
        var lonRad = DegreesToRadians(longitude);
        return new ProjectedPoint(
            (lonRad - origin.LongitudeRadians) * Math.Cos(origin.LatitudeRadians) * EarthRadiusMeters,
            (latRad - origin.LatitudeRadians) * EarthRadiusMeters);
    }

    private static double PolylineLength(IReadOnlyList<ProjectedPoint> points, bool closed)
    {
        var total = 0d;
        for (var index = 1; index < points.Count; index++)
        {
            total += Distance(points[index - 1], points[index]);
        }

        return closed && points.Count > 1 ? total + Distance(points[^1], points[0]) : total;
    }

    private static double Distance(ProjectedPoint left, ProjectedPoint right)
    {
        var dx = right.X - left.X;
        var dy = right.Y - left.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static int BinIndex(double lapDistPct, int binCount)
    {
        return Math.Clamp((int)Math.Floor(Math.Clamp(lapDistPct, 0d, 0.999999d) * binCount), 0, binCount - 1);
    }

    private static double? Median(IReadOnlyList<double> values)
    {
        return values.Count == 0 ? null : MedianRequired(values.OrderBy(value => value).ToArray());
    }

    private static double MedianRequired(IReadOnlyList<double> sortedOrUnsorted)
    {
        var values = sortedOrUnsorted.OrderBy(value => value).ToArray();
        var middle = values.Length / 2;
        return values.Length % 2 == 1 ? values[middle] : (values[middle - 1] + values[middle]) / 2d;
    }

    private static double Percentile(IReadOnlyList<double> sortedValues, double percentile)
    {
        if (sortedValues.Count == 0)
        {
            return 0d;
        }

        var index = Math.Clamp((int)Math.Ceiling(sortedValues.Count * percentile) - 1, 0, sortedValues.Count - 1);
        return sortedValues[index];
    }

    private static double? ReadDouble(byte[] payload, IbtTelemetryVariableSchema? field)
    {
        return field is null ? null : ReadValue(payload, field) switch
        {
            byte byteValue => byteValue,
            int intValue => intValue,
            uint uintValue => uintValue,
            float single => single,
            double number => number,
            _ => null
        };
    }

    private static int? ReadInt(byte[] payload, IbtTelemetryVariableSchema? field)
    {
        return field is null ? null : ReadValue(payload, field) switch
        {
            byte byteValue => byteValue,
            int intValue => intValue,
            uint uintValue when uintValue <= int.MaxValue => (int)uintValue,
            float single when IsFinite(single) => (int)Math.Round(single),
            double number when IsFinite(number) => (int)Math.Round(number),
            _ => null
        };
    }

    private static bool ReadBool(byte[] payload, IbtTelemetryVariableSchema? field)
    {
        return field is not null && ReadValue(payload, field) switch
        {
            bool boolean => boolean,
            byte byteValue => byteValue != 0,
            int intValue => intValue != 0,
            uint uintValue => uintValue != 0,
            float single => Math.Abs(single) > 0.000001d,
            double number => Math.Abs(number) > 0.000001d,
            _ => false
        };
    }

    private static object? ReadValue(byte[] payload, IbtTelemetryVariableSchema field)
    {
        if (field.Offset < 0 || field.ByteSize <= 0 || field.Offset + field.ByteSize > payload.Length)
        {
            return null;
        }

        return field.TypeCode switch
        {
            0 => payload[field.Offset],
            1 => payload[field.Offset] != 0,
            2 => BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(field.Offset, 4)),
            3 => BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(field.Offset, 4)),
            4 => BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(field.Offset, 4))),
            5 => BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(payload.AsSpan(field.Offset, 8))),
            _ => null
        };
    }

    private static void ReadExact(Stream stream, byte[] buffer)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = stream.Read(buffer, total, buffer.Length - total);
            if (read == 0)
            {
                throw new EndOfStreamException("Unexpected end of IBT file.");
            }

            total += read;
        }
    }

    private static bool IsValidLapDistPct(double value)
    {
        return IsFinite(value) && value >= 0d && value <= 1.000001d;
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    private static double DegreesToRadians(double degrees)
    {
        return degrees * Math.PI / 180d;
    }

    private sealed record RequiredFields(
        IbtTelemetryVariableSchema? Latitude,
        IbtTelemetryVariableSchema? Longitude,
        IbtTelemetryVariableSchema? LapDistPct,
        IbtTelemetryVariableSchema? LapDist,
        IbtTelemetryVariableSchema? LapCompleted,
        IbtTelemetryVariableSchema? Lap,
        IbtTelemetryVariableSchema? Speed,
        IbtTelemetryVariableSchema? OnPitRoad,
        IReadOnlyList<string> MissingReasons)
    {
        public static RequiredFields From(IReadOnlyList<IbtTelemetryVariableSchema> fields)
        {
            var byName = fields
                .GroupBy(field => field.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
            var missing = new List<string>();
            var lat = Get(byName, "Lat", missing);
            var lon = Get(byName, "Lon", missing);
            var lapDistPct = Get(byName, "LapDistPct", missing);
            return new RequiredFields(
                Latitude: lat,
                Longitude: lon,
                LapDistPct: lapDistPct,
                LapDist: byName.GetValueOrDefault("LapDist"),
                LapCompleted: byName.GetValueOrDefault("LapCompleted"),
                Lap: byName.GetValueOrDefault("Lap"),
                Speed: byName.GetValueOrDefault("Speed"),
                OnPitRoad: byName.GetValueOrDefault("OnPitRoad"),
                MissingReasons: missing);
        }

        private static IbtTelemetryVariableSchema? Get(
            IReadOnlyDictionary<string, IbtTelemetryVariableSchema> fields,
            string name,
            List<string> missing)
        {
            if (fields.TryGetValue(name, out var field))
            {
                return field;
            }

            missing.Add($"{name.ToLowerInvariant()}_missing");
            return null;
        }
    }

    private sealed record CompleteLapCandidate(
        int LapNumber,
        IReadOnlyList<IbtTrackMapSample> Samples,
        double MinPct,
        double MaxPct,
        double? LapDistanceMeters);

    private record ProjectedPoint(double X, double Y);

    private sealed record ProjectedSample(
        long Sequence,
        int LapNumber,
        double LapDistPct,
        double X,
        double Y) : ProjectedPoint(X, Y)
    {
        public static ProjectedSample From(IbtTrackMapSample sample, ProjectionOrigin origin)
        {
            var point = Project(sample.Latitude, sample.Longitude, origin);
            return new ProjectedSample(sample.Sequence, sample.LapNumber, sample.LapDistPct, point.X, point.Y);
        }
    }

    private sealed record ProjectionOrigin(double LatitudeRadians, double LongitudeRadians)
    {
        public static ProjectionOrigin From(IReadOnlyList<IbtTrackMapSample> samples)
        {
            return new ProjectionOrigin(
                DegreesToRadians(MedianRequired(samples.Select(sample => sample.Latitude).ToArray())),
                DegreesToRadians(MedianRequired(samples.Select(sample => sample.Longitude).ToArray())));
        }
    }

    private sealed record RepeatabilitySummary(double? MedianMeters, double? P95Meters);
}

internal sealed record IbtTrackMapSample(
    long Sequence,
    int LapNumber,
    double LapDistPct,
    double? LapDistMeters,
    double Latitude,
    double Longitude,
    double SpeedMetersPerSecond,
    bool OnPitRoad);

internal sealed record TrackMapBuildResult(
    TrackMapDocument? Document,
    IReadOnlyList<string> RejectionReasons)
{
    public static TrackMapBuildResult Rejected(IReadOnlyList<string> reasons)
    {
        return new TrackMapBuildResult(null, reasons);
    }
}
