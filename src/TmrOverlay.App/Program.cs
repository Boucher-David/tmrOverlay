using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TmrOverlay.Core.AppInfo;
using TmrOverlay.App.Analysis;
using TmrOverlay.App.Bridge;
using TmrOverlay.App.Diagnostics;
using TmrOverlay.App.Events;
using TmrOverlay.App.History;
using TmrOverlay.App.Logging;
using TmrOverlay.App.Overlays;
using TmrOverlay.App.Overlays.Styling;
using TmrOverlay.App.Performance;
using TmrOverlay.App.Replay;
using TmrOverlay.App.Retention;
using TmrOverlay.App.Runtime;
using TmrOverlay.App.Shell;
using TmrOverlay.App.Settings;
using TmrOverlay.App.Storage;
using TmrOverlay.App.Telemetry;
using TmrOverlay.Core.Telemetry.Live;

namespace TmrOverlay.App;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        using var singleInstanceMutex = new Mutex(
            initiallyOwned: true,
            name: @"Local\TmrOverlay.App",
            createdNew: out var isFirstInstance);
        if (!isFirstInstance)
        {
            MessageBox.Show(
                "TMR Overlay is already running.",
                "TMR Overlay",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        using var host = CreateHostBuilder().Build();
        var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("TmrOverlay.App");
        var storageOptions = host.Services.GetRequiredService<AppStorageOptions>();
        var captureState = host.Services.GetRequiredService<TelemetryCaptureState>();

        RegisterUnhandledExceptionLogging(logger);
        OverlayTheme.LoadOverrides(Path.Combine(storageOptions.SettingsRoot, "overlay-theme.json"), logger);

        host.Start();
        RecordBuildFreshness(captureState, logger);
        logger.LogInformation(
            "TmrOverlay {Version} started. Captures: {CaptureRoot}. User history: {UserHistoryRoot}. Logs: {LogsRoot}.",
            AppVersionInfo.Current.InformationalVersion,
            storageOptions.CaptureRoot,
            storageOptions.UserHistoryRoot,
            storageOptions.LogsRoot);

        var applicationContext = host.Services.GetRequiredService<NotifyIconApplicationContext>();
        Application.Run(applicationContext);

        host.StopAsync().GetAwaiter().GetResult();
        logger.LogInformation("TmrOverlay stopped.");
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
            .ConfigureLogging((context, logging) =>
            {
                var storageOptions = AppStorageOptions.FromConfiguration(context.Configuration);
                logging.ClearProviders();
                logging.AddDebug();
                logging.AddProvider(new LocalFileLoggerProvider(
                    LocalFileLoggerOptions.FromConfiguration(context.Configuration, storageOptions)));
            })
            .ConfigureServices((context, services) =>
            {
                var storageOptions = AppStorageOptions.FromConfiguration(context.Configuration);
                var replayOptions = ReplayOptions.FromConfiguration(context.Configuration);
                services.AddSingleton(storageOptions);
                services.AddSingleton(TelemetryCaptureOptions.FromConfiguration(context.Configuration, storageOptions));
                services.AddSingleton(TelemetryEdgeCaseOptions.FromConfiguration(context.Configuration));
                services.AddSingleton(IbtAnalysisOptions.FromConfiguration(context.Configuration));
                services.AddSingleton(SessionHistoryOptions.FromConfiguration(context.Configuration, storageOptions));
                services.AddSingleton(RetentionOptions.FromConfiguration(context.Configuration));
                services.AddSingleton(replayOptions);
                services.AddSingleton(OverlayBridgeOptions.FromConfiguration(context.Configuration));
                services.AddSingleton<AppEventRecorder>();
                services.AddSingleton<AppSettingsStore>();
                services.AddSingleton<SessionHistoryStore>();
                services.AddSingleton<SessionHistoryQueryService>();
                services.AddSingleton<PostRaceAnalysisStore>();
                services.AddSingleton<PostRaceAnalysisPipeline>();
                services.AddSingleton<IbtAnalysisService>();
                services.AddSingleton<DiagnosticsBundleService>();
                services.AddSingleton<TelemetryCaptureState>();
                services.AddSingleton<TelemetryEdgeCaseRecorder>();
                services.AddSingleton<AppPerformanceState>();
                services.AddSingleton<AppPerformanceSnapshotRecorder>();
                services.AddSingleton<LiveTelemetryStore>();
                services.AddSingleton<ILiveTelemetrySource>(services => services.GetRequiredService<LiveTelemetryStore>());
                services.AddSingleton<ILiveTelemetrySink>(services => services.GetRequiredService<LiveTelemetryStore>());
                services.AddSingleton<OverlayManager>();
                services.AddSingleton<NotifyIconApplicationContext>();
                services.AddHostedService<RuntimeStateService>();
                services.AddHostedService<HistoryMaintenanceService>();
                services.AddHostedService<AppPerformanceHostedService>();
                services.AddHostedService<RetentionHostedService>();
                services.AddHostedService<OverlayBridgeHostedService>();
                services.AddTelemetryProvider(replayOptions);
            });
    }

    private static void RegisterUnhandledExceptionLogging(ILogger logger)
    {
        Application.ThreadException += (_, exception) =>
        {
            logger.LogError(exception.Exception, "Unhandled WinForms thread exception.");
        };

        AppDomain.CurrentDomain.UnhandledException += (_, exception) =>
        {
            if (exception.ExceptionObject is Exception unhandledException)
            {
                logger.LogCritical(unhandledException, "Unhandled application domain exception.");
                return;
            }

            logger.LogCritical(
                "Unhandled application domain exception object: {ExceptionObject}.",
                exception.ExceptionObject);
        };

        TaskScheduler.UnobservedTaskException += (_, exception) =>
        {
            logger.LogError(exception.Exception, "Unobserved task exception.");
        };
    }

    private static void RecordBuildFreshness(TelemetryCaptureState captureState, ILogger logger)
    {
        try
        {
            var buildFreshness = BuildFreshnessChecker.Check();
            if (!buildFreshness.SourceNewerThanBuild || string.IsNullOrWhiteSpace(buildFreshness.Message))
            {
                return;
            }

            captureState.RecordAppWarning(buildFreshness.Message);
            logger.LogWarning("{BuildFreshnessWarning}", buildFreshness.Message);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to evaluate local build freshness.");
        }
    }
}
