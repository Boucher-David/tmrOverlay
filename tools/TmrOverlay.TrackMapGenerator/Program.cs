using TmrOverlay.App.Storage;
using TmrOverlay.App.TrackMaps;
using TmrOverlay.Core.TrackMaps;

namespace TmrOverlay.TrackMapGenerator;

internal static class Program
{
    private static int Main(string[] args)
    {
        var options = GeneratorOptions.Parse(args);
        if (!Directory.Exists(options.SourceRoot))
        {
            Console.Error.WriteLine($"IBT source root does not exist: {options.SourceRoot}");
            return 2;
        }

        Directory.CreateDirectory(options.OutputRoot);
        var storage = StorageOptionsFor(options.OutputRoot);
        var store = new TrackMapStore(storage);
        var builder = new IbtTrackMapBuilder();
        var paths = Directory
            .EnumerateFiles(options.SourceRoot, "*.ibt", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var saved = 0;
        var skippedComplete = 0;
        var rejected = 0;
        var skippedQuality = 0;
        var failed = 0;
        foreach (var path in paths)
        {
            try
            {
                var track = builder.ReadTrackIdentity(path, CancellationToken.None);
                if (!options.Force && store.HasCompleteMap(track))
                {
                    skippedComplete++;
                    Console.WriteLine($"skip complete | {Path.GetFileName(path)}");
                    continue;
                }

                var build = builder.BuildFromIbt(path, captureId: null, CancellationToken.None);
                if (build.Document is null)
                {
                    rejected++;
                    Console.WriteLine($"reject {string.Join(",", build.RejectionReasons)} | {Path.GetFileName(path)}");
                    continue;
                }

                var document = ForBundledAsset(build.Document, path);
                if (!document.IsCompleteForRuntime || document.Quality.Confidence < options.MinimumConfidence)
                {
                    skippedQuality++;
                    Console.WriteLine(
                        $"skip quality {document.Quality.Confidence} missing={document.Quality.MissingBinCount} laps={document.Quality.CompleteLapCount} | {Path.GetFileName(path)}");
                    continue;
                }

                if (options.DryRun)
                {
                    saved++;
                    Console.WriteLine($"would save {document.Identity.Key} {document.Quality.Confidence} | {Path.GetFileName(path)}");
                    continue;
                }

                var result = store.SaveIfImproved(document, force: options.Force);
                if (result.Saved)
                {
                    saved++;
                    Console.WriteLine($"saved {Path.GetFileName(result.Path)} {document.Quality.Confidence} | {Path.GetFileName(path)}");
                }
                else
                {
                    skippedComplete++;
                    Console.WriteLine($"skip {result.Reason} | {Path.GetFileName(path)}");
                }
            }
            catch (Exception exception)
            {
                failed++;
                Console.WriteLine($"fail {exception.GetType().Name} | {Path.GetFileName(path)}");
            }

            if (options.Limit is { } limit && saved >= limit)
            {
                break;
            }
        }

        Console.WriteLine(
            $"track-map generation complete: saved={saved} skippedComplete={skippedComplete} rejected={rejected} skippedQuality={skippedQuality} failed={failed} sourceFiles={paths.Length} output={options.OutputRoot}");
        return failed == 0 ? 0 : 1;
    }

    private static TrackMapDocument ForBundledAsset(TrackMapDocument document, string sourcePath)
    {
        return document with
        {
            Provenance = document.Provenance with
            {
                SourceKind = "bundled-ibt",
                SourcePath = Path.GetFileName(sourcePath),
                CaptureId = null
            }
        };
    }

    private static AppStorageOptions StorageOptionsFor(string outputRoot)
    {
        return new AppStorageOptions
        {
            AppDataRoot = outputRoot,
            CaptureRoot = Path.Combine(outputRoot, "_captures"),
            UserHistoryRoot = Path.Combine(outputRoot, "_history", "user"),
            BaselineHistoryRoot = Path.Combine(outputRoot, "_history", "baseline"),
            LogsRoot = Path.Combine(outputRoot, "_logs"),
            SettingsRoot = Path.Combine(outputRoot, "_settings"),
            DiagnosticsRoot = Path.Combine(outputRoot, "_diagnostics"),
            TrackMapRoot = outputRoot,
            EventsRoot = Path.Combine(outputRoot, "_events"),
            RuntimeStatePath = Path.Combine(outputRoot, "_runtime-state.json")
        };
    }

    private sealed record GeneratorOptions(
        string SourceRoot,
        string OutputRoot,
        TrackMapConfidence MinimumConfidence,
        int? Limit,
        bool Force,
        bool DryRun)
    {
        public static GeneratorOptions Parse(IReadOnlyList<string> args)
        {
            var sourceRoot = Path.GetFullPath(Path.Combine("captures", "IBT"));
            var outputRoot = Path.GetFullPath(Path.Combine("src", "TmrOverlay.App", "Assets", "TrackMaps"));
            var minimumConfidence = TrackMapConfidence.Medium;
            int? limit = null;
            var force = false;
            var dryRun = false;

            for (var index = 0; index < args.Count; index++)
            {
                var arg = args[index];
                switch (arg)
                {
                    case "--source":
                        sourceRoot = Path.GetFullPath(RequireValue(args, ref index, arg));
                        break;
                    case "--output":
                        outputRoot = Path.GetFullPath(RequireValue(args, ref index, arg));
                        break;
                    case "--min-confidence":
                        minimumConfidence = Enum.TryParse<TrackMapConfidence>(
                            RequireValue(args, ref index, arg),
                            ignoreCase: true,
                            out var parsed)
                            ? parsed
                            : TrackMapConfidence.Medium;
                        break;
                    case "--limit":
                        limit = int.TryParse(RequireValue(args, ref index, arg), out var parsedLimit)
                            ? Math.Max(1, parsedLimit)
                            : null;
                        break;
                    case "--force":
                        force = true;
                        break;
                    case "--dry-run":
                        dryRun = true;
                        break;
                    case "--help":
                    case "-h":
                        PrintUsage();
                        Environment.Exit(0);
                        break;
                }
            }

            return new GeneratorOptions(sourceRoot, outputRoot, minimumConfidence, limit, force, dryRun);
        }

        private static string RequireValue(IReadOnlyList<string> args, ref int index, string name)
        {
            if (index + 1 >= args.Count)
            {
                throw new ArgumentException($"{name} requires a value.");
            }

            index++;
            return args[index];
        }

        private static void PrintUsage()
        {
            Console.WriteLine(
                "Usage: TmrOverlay.TrackMapGenerator [--source captures/IBT] [--output src/TmrOverlay.App/Assets/TrackMaps] [--min-confidence Medium|High] [--limit N] [--force] [--dry-run]");
        }
    }
}
