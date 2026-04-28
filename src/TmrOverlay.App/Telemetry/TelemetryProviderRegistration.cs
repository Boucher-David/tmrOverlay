using Microsoft.Extensions.DependencyInjection;
using TmrOverlay.App.Replay;

namespace TmrOverlay.App.Telemetry;

internal static class TelemetryProviderRegistration
{
    public static IServiceCollection AddTelemetryProvider(
        this IServiceCollection services,
        ReplayOptions replayOptions)
    {
        if (replayOptions.Enabled)
        {
            services.AddHostedService<ReplayTelemetryHostedService>();
            return services;
        }

        services.AddHostedService<TelemetryCaptureHostedService>();
        return services;
    }
}
