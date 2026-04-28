using Microsoft.Extensions.Configuration;

namespace TmrOverlay.App.Bridge;

internal sealed class OverlayBridgeOptions
{
    private const int DefaultPort = 8765;

    public bool Enabled { get; init; }

    public int Port { get; init; } = DefaultPort;

    public string Prefix => $"http://localhost:{Port}/";

    public static OverlayBridgeOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection("OverlayBridge");
        return new OverlayBridgeOptions
        {
            Enabled = bool.TryParse(section["Enabled"], out var enabled) && enabled,
            Port = ParsePort(section["Port"])
        };
    }

    private static int ParsePort(string? configuredValue)
    {
        return int.TryParse(configuredValue, out var parsed) && parsed is >= 1024 and <= 65535
            ? parsed
            : DefaultPort;
    }
}
