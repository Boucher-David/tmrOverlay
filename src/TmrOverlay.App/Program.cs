using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TmrOverlay.App.Telemetry;

namespace TmrOverlay.App;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        using var host = CreateHostBuilder().Build();
        host.Start();

        var applicationContext = host.Services.GetRequiredService<NotifyIconApplicationContext>();
        Application.Run(applicationContext);

        host.StopAsync().GetAwaiter().GetResult();
    }

    private static IHostBuilder CreateHostBuilder()
    {
        return Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((_, configurationBuilder) =>
            {
                configurationBuilder.Sources.Clear();
                configurationBuilder.SetBasePath(AppContext.BaseDirectory);
                configurationBuilder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                configurationBuilder.AddEnvironmentVariables(prefix: "TMR_");
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddDebug();
            })
            .ConfigureServices((context, services) =>
            {
                services.AddSingleton(TelemetryCaptureOptions.FromConfiguration(context.Configuration));
                services.AddSingleton<TelemetryCaptureState>();
                services.AddSingleton<NotifyIconApplicationContext>();
                services.AddHostedService<TelemetryCaptureHostedService>();
            });
    }
}

