namespace TmrOverlay.Core.Overlays;

internal enum OverlaySettingsOptionKind
{
    Boolean,
    Integer
}

internal sealed class OverlaySettingsOptionDescriptor
{
    private OverlaySettingsOptionDescriptor(
        string key,
        string label,
        OverlaySettingsOptionKind kind,
        bool booleanDefault,
        int integerDefault,
        int minimum,
        int maximum)
    {
        Key = key;
        Label = label;
        Kind = kind;
        BooleanDefault = booleanDefault;
        IntegerDefault = integerDefault;
        Minimum = minimum;
        Maximum = maximum;
    }

    public string Key { get; }

    public string Label { get; }

    public OverlaySettingsOptionKind Kind { get; }

    public bool BooleanDefault { get; }

    public int IntegerDefault { get; }

    public int Minimum { get; }

    public int Maximum { get; }

    public static OverlaySettingsOptionDescriptor Boolean(
        string key,
        string label,
        bool defaultValue)
    {
        return new OverlaySettingsOptionDescriptor(
            key,
            label,
            OverlaySettingsOptionKind.Boolean,
            booleanDefault: defaultValue,
            integerDefault: 0,
            minimum: 0,
            maximum: 0);
    }

    public static OverlaySettingsOptionDescriptor Integer(
        string key,
        string label,
        int minimum,
        int maximum,
        int defaultValue)
    {
        return new OverlaySettingsOptionDescriptor(
            key,
            label,
            OverlaySettingsOptionKind.Integer,
            booleanDefault: false,
            integerDefault: Math.Clamp(defaultValue, minimum, maximum),
            minimum,
            maximum);
    }
}
