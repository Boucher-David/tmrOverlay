using System.Reflection;
using System.Text;
using System.Text.Json;
using TmrOverlay.Core.Settings;
using Xunit;

namespace TmrOverlay.App.Tests.Settings;

public sealed class AppSettingsSchemaCompatibilityTests
{
    private const string ExpectedSettingsSchema = """
ApplicationSettings
  General: ApplicationGeneralSettings
  Overlays: List<OverlaySettings>
  SettingsVersion: int
ApplicationGeneralSettings
  FontFamily: string
  UnitSystem: string
OverlaySettings
  AlwaysOnTop: bool
  Enabled: bool
  Height: int
  Id: string
  LegacyProperties: Dictionary<string, JsonElement>
  Opacity: double
  Options: Dictionary<string, string>
  Scale: double
  ScreenId: string
  ShowInPractice: bool
  ShowInQualifying: bool
  ShowInRace: bool
  ShowInTest: bool
  Width: int
  X: int
  Y: int
""";

    private static readonly IReadOnlyDictionary<Type, string> TypeAliases = new Dictionary<Type, string>
    {
        [typeof(bool)] = "bool",
        [typeof(int)] = "int",
        [typeof(double)] = "double",
        [typeof(string)] = "string",
        [typeof(JsonElement)] = "JsonElement"
    };

    [Fact]
    public void DurableSettingsSchema_HasExplicitCompatibilityReview()
    {
        var currentSchema = BuildSchemaSnapshot(
            typeof(ApplicationSettings),
            typeof(ApplicationGeneralSettings),
            typeof(OverlaySettings));

        Assert.True(
            Normalize(ExpectedSettingsSchema) == Normalize(currentSchema),
            "The durable app settings schema changed. Before updating this snapshot, decide whether SettingsVersion/migration/defaults/docs need to change; update AppSettingsMigrator and compatibility tests in the same pass.");
    }

    private static string BuildSchemaSnapshot(params Type[] types)
    {
        var builder = new StringBuilder();
        foreach (var type in types)
        {
            builder.AppendLine(type.Name);
            foreach (var property in type
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(property => property.GetIndexParameters().Length == 0)
                .OrderBy(property => property.Name, StringComparer.Ordinal))
            {
                builder.Append("  ");
                builder.Append(property.Name);
                builder.Append(": ");
                builder.AppendLine(FormatType(property.PropertyType));
            }
        }

        return builder.ToString();
    }

    private static string FormatType(Type type)
    {
        var nullableInnerType = Nullable.GetUnderlyingType(type);
        if (nullableInnerType is not null)
        {
            return $"{FormatType(nullableInnerType)}?";
        }

        if (TypeAliases.TryGetValue(type, out var alias))
        {
            return alias;
        }

        if (type.IsGenericType)
        {
            var definition = type.GetGenericTypeDefinition();
            var name = definition.Name.Split('`')[0];
            var arguments = string.Join(", ", type.GetGenericArguments().Select(FormatType));
            return $"{name}<{arguments}>";
        }

        return type.Name;
    }

    private static string Normalize(string value)
    {
        return value.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
    }
}
