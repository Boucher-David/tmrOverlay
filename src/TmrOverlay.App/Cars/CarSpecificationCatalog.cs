using System.Text.Json;
using System.Text.Json.Serialization;
using TmrOverlay.Core.History;

namespace TmrOverlay.App.Cars;

internal sealed class CarSpecificationCatalog
{
    private static readonly Lazy<CarSpecificationCatalog> LazyBundled = new(() => new CarSpecificationCatalog());
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _path;
    private readonly Lazy<CarSpecificationDocument?> _document;

    public static CarSpecificationCatalog Bundled => LazyBundled.Value;

    public CarSpecificationCatalog(string? bundledRoot = null)
    {
        _path = Path.Combine(
            bundledRoot ?? Path.Combine(AppContext.BaseDirectory, "Assets", "CarSpecs"),
            "car-specs.json");
        _document = new Lazy<CarSpecificationDocument?>(ReadDocument);
    }

    public CarSpecification? TryFind(HistoricalComboIdentity combo)
    {
        var document = _document.Value;
        if (document?.SchemaVersion != CarSpecificationDocument.CurrentSchemaVersion)
        {
            return null;
        }

        return document.Cars
            .Where(car => car.IsValid)
            .FirstOrDefault(car => car.Matches(combo.CarKey));
    }

    public bool HasExactSpec(HistoricalComboIdentity combo)
    {
        return TryFind(combo)?.IsExact == true;
    }

    private CarSpecificationDocument? ReadDocument()
    {
        if (!File.Exists(_path))
        {
            return null;
        }

        try
        {
            using var stream = File.OpenRead(_path);
            return JsonSerializer.Deserialize<CarSpecificationDocument>(stream, JsonOptions);
        }
        catch
        {
            return null;
        }
    }
}

internal sealed class CarSpecificationDocument
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; }

    public DateTimeOffset GeneratedAtUtc { get; init; }

    public IReadOnlyList<CarSpecification> Cars { get; init; } = [];
}

internal sealed class CarSpecification
{
    public int? CarId { get; init; }

    public string? CarPath { get; init; }

    public string? DisplayName { get; init; }

    public IReadOnlyList<string> CarKeys { get; init; } = [];

    public double? BodyLengthMeters { get; init; }

    public string Confidence { get; init; } = "exact";

    public string? Source { get; init; }

    public string? SourceNote { get; init; }

    public bool IsValid => BodyLengthMeters is { } meters
        && meters >= 3.5d
        && meters <= 6.5d
        && CarKeys.Count > 0;

    public bool IsExact => string.Equals(Confidence, "exact", StringComparison.OrdinalIgnoreCase);

    public bool Matches(string carKey)
    {
        return CarKeys.Any(key => string.Equals(key, carKey, StringComparison.OrdinalIgnoreCase));
    }
}
