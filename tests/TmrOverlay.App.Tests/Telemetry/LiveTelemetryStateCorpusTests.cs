using System.Text.Json.Nodes;
using Xunit;

namespace TmrOverlay.App.Tests.Telemetry;

public sealed class LiveTelemetryStateCorpusTests
{
    private const string CorpusRelativePath = "fixtures/telemetry-analysis/live-telemetry-state-corpus.json";

    [Fact]
    public void Corpus_CoversCurrentSourceSelectionTargets()
    {
        var corpus = ReadCorpus();
        var states = RequiredArray(corpus["states"]);
        var ids = states.Select(state => RequiredString(RequiredObject(state), "id")).ToArray();

        string[] expectedIds =
        [
            "ai-practice-no-valid-lap",
            "ai-qualifying-valid-lap-gated",
            "ai-race-pre-green",
            "endurance-4h-race-pre-countdown",
            "endurance-4h-race-pre-grid-no-countdown",
            "ai-race-green-non-player-focus",
            "open-practice-non-player-focus",
            "open-practice-player-focus",
            "endurance-4h-race-running",
            "endurance-4h-pit-or-garage",
            "endurance-24h-race-running",
            "endurance-24h-pit-or-garage"
        ];
        Assert.True(
            expectedIds.SequenceEqual(ids),
            $"Expected state ids [{string.Join(", ", expectedIds)}], got [{string.Join(", ", ids)}].");

        var missing = RequiredArray(corpus["missingTargets"])
            .Select(target => RequiredString(RequiredObject(target), "id"))
            .ToArray();
        string[] expectedMissingTargets = ["ai-race-green-player-focus", "degraded-focus-unavailable"];
        Assert.True(
            expectedMissingTargets.SequenceEqual(missing),
            $"Expected missing targets [{string.Join(", ", expectedMissingTargets)}], got [{string.Join(", ", missing)}].");

        var sources = RequiredArray(corpus["sources"]);
        Assert.Contains(sources, source => RequiredString(RequiredObject(source), "sourceCategory") == "ai-multisession-spectated");
        Assert.Contains(sources, source => RequiredString(RequiredObject(source), "sourceCategory") == "open-player-practice");
        Assert.Contains(sources, source => RequiredString(RequiredObject(source), "sourceCategory") == "endurance-4h-team-race");
        Assert.Contains(sources, source => RequiredString(RequiredObject(source), "sourceCategory") == "endurance-24h-fragment");
    }

    [Fact]
    public void Corpus_EncodesExpectedOverlaySourceDecisions()
    {
        var corpus = ReadCorpus();

        var aiPractice = StateById(corpus, "ai-practice-no-valid-lap");
        AssertStandings(aiPractice, "none", rows: 0, validRows: 0, renders: false);
        AssertRelative(aiPractice, "waiting");
        AssertGapData(aiPractice, expected: false);

        var aiRacePreGreen = StateById(corpus, "ai-race-pre-green");
        AssertStandings(aiRacePreGreen, "starting-grid", rows: 41, validRows: 38, renders: true);
        AssertRelative(aiRacePreGreen, "model-v2-timing-fallback");
        AssertGapData(aiRacePreGreen, expected: false);

        var endurance4hRacePreCountdown = StateById(corpus, "endurance-4h-race-pre-countdown");
        AssertStandings(endurance4hRacePreCountdown, "starting-grid", rows: 60, validRows: 41, renders: true);
        AssertRelative(endurance4hRacePreCountdown, "live-proximity");
        AssertGapData(endurance4hRacePreCountdown, expected: false);

        var endurance4hRacePreGridNoCountdown = StateById(corpus, "endurance-4h-race-pre-grid-no-countdown");
        AssertStandings(endurance4hRacePreGridNoCountdown, "starting-grid", rows: 60, validRows: 41, renders: true);
        AssertRelative(endurance4hRacePreGridNoCountdown, "live-proximity");
        AssertGapData(endurance4hRacePreGridNoCountdown, expected: false);

        var aiRaceGreen = StateById(corpus, "ai-race-green-non-player-focus");
        Assert.Equal("non-player", RequiredString(RequiredObject(aiRaceGreen["labelBasis"]), "focusRelation"));
        AssertStandings(aiRaceGreen, "session-results", rows: 40, validRows: 40, renders: true);
        AssertRelative(aiRaceGreen, "model-v2-timing-fallback");
        AssertGapData(aiRaceGreen, expected: true);

        var openPractice = StateById(corpus, "open-practice-non-player-focus");
        AssertStandings(openPractice, "session-results", rows: 14, validRows: 14, renders: true);
        AssertRelative(openPractice, "model-v2-timing-fallback");
        AssertGapData(openPractice, expected: true);

        var openPracticePlayer = StateById(corpus, "open-practice-player-focus");
        AssertStandings(openPracticePlayer, "none", rows: 0, validRows: 0, renders: false);
        AssertRelative(openPracticePlayer, "waiting");
        AssertGapData(openPracticePlayer, expected: false);

        var endurance4hRunning = StateById(corpus, "endurance-4h-race-running");
        AssertStandings(endurance4hRunning, "session-results", rows: 58, validRows: 55, renders: true);
        AssertRelative(endurance4hRunning, "live-proximity");
        AssertLocalRadar(endurance4hRunning, available: true);
        AssertGapData(endurance4hRunning, expected: true);

        var endurance4hPit = StateById(corpus, "endurance-4h-pit-or-garage");
        AssertPitOrGarage(endurance4hPit, expected: true);
        AssertLocalRadar(endurance4hPit, available: false);
        AssertRelative(endurance4hPit, "model-v2-timing-fallback");
        AssertGapData(endurance4hPit, expected: true);

        var endurance24hRunning = StateById(corpus, "endurance-24h-race-running");
        AssertStandings(endurance24hRunning, "session-results", rows: 57, validRows: 50, renders: true);
        AssertRelative(endurance24hRunning, "model-v2-timing-fallback");
        AssertGapData(endurance24hRunning, expected: true);

        var endurance24hPit = StateById(corpus, "endurance-24h-pit-or-garage");
        AssertPitOrGarage(endurance24hPit, expected: true);
        AssertLocalRadar(endurance24hPit, available: false);
        AssertRelative(endurance24hPit, "model-v2-timing-fallback");
        AssertGapData(endurance24hPit, expected: true);
    }

    [Fact]
    public void Corpus_DoesNotCommitRawPayloadsOrPrivateIdentityFields()
    {
        var corpus = ReadCorpus();
        foreach (var state in RequiredArray(corpus["states"]).Select(RequiredObject))
        {
            var text = state.ToJsonString().ToLowerInvariant();
            Assert.DoesNotContain("telemetry.bin", text);
            Assert.DoesNotContain(".ibt", text);
            Assert.DoesNotContain("drivername", text);
            Assert.DoesNotContain("username", text);
            Assert.DoesNotContain("userid", text);
            Assert.DoesNotContain("teamname", text);
        }
    }

    private static void AssertStandings(JsonObject state, string source, int rows, int validRows, bool renders)
    {
        var standings = RequiredObject(RequiredObject(state["modelInputs"])["standings"]);
        Assert.Equal(source, RequiredString(standings, "selectedSource"));
        Assert.Equal(rows, RequiredInt(standings, "selectedRowCount"));
        Assert.Equal(validRows, RequiredInt(standings, "selectedValidLapRowCount"));
        Assert.Equal(renders, RequiredBool(standings, "standingsWouldRender"));
    }

    private static void AssertRelative(JsonObject state, string source)
    {
        var relative = RequiredObject(RequiredObject(state["modelInputs"])["relative"]);
        Assert.Equal(source, RequiredString(relative, "relativeModelLikelySource"));
    }

    private static void AssertGapData(JsonObject state, bool expected)
    {
        var gap = RequiredObject(RequiredObject(state["modelInputs"])["gapToLeader"]);
        Assert.Equal(expected, RequiredBool(gap, "gapOverlayWouldHaveData"));
    }

    private static void AssertLocalRadar(JsonObject state, bool available)
    {
        var focusContext = RequiredObject(state["focusContext"]);
        Assert.Equal(available, RequiredBool(focusContext, "localRadarAvailable"));
    }

    private static void AssertPitOrGarage(JsonObject state, bool expected)
    {
        var labelBasis = RequiredObject(state["labelBasis"]);
        Assert.Equal(expected, RequiredBool(labelBasis, "pitOrGarageContext"));
    }

    private static JsonObject StateById(JsonObject corpus, string id)
    {
        return RequiredArray(corpus["states"])
            .Select(RequiredObject)
            .Single(state => RequiredString(state, "id") == id);
    }

    private static JsonObject ReadCorpus()
    {
        var path = FindRepoRootFile(CorpusRelativePath);
        return RequiredObject(JsonNode.Parse(File.ReadAllText(path)));
    }

    private static string FindRepoRootFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find {relativePath} from {AppContext.BaseDirectory}.");
    }

    private static JsonObject RequiredObject(JsonNode? node)
    {
        return Assert.IsType<JsonObject>(node);
    }

    private static JsonArray RequiredArray(JsonNode? node)
    {
        return Assert.IsType<JsonArray>(node);
    }

    private static string RequiredString(JsonObject value, string propertyName)
    {
        return Assert.IsAssignableFrom<JsonValue>(value[propertyName]).GetValue<string>();
    }

    private static int RequiredInt(JsonObject value, string propertyName)
    {
        return Assert.IsAssignableFrom<JsonValue>(value[propertyName]).GetValue<int>();
    }

    private static bool RequiredBool(JsonObject value, string propertyName)
    {
        return Assert.IsAssignableFrom<JsonValue>(value[propertyName]).GetValue<bool>();
    }
}
