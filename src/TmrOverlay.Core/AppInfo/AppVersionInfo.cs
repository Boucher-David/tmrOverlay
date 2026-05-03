using System.Reflection;
using System.Runtime.InteropServices;

namespace TmrOverlay.Core.AppInfo;

internal sealed class AppVersionInfo
{
    public required string ProductName { get; init; }

    public required string Version { get; init; }

    public required string InformationalVersion { get; init; }

    public required string RuntimeVersion { get; init; }

    public required string OperatingSystem { get; init; }

    public required string ProcessArchitecture { get; init; }

    public static AppVersionInfo Current { get; } = Create();

    private static AppVersionInfo Create()
    {
        var assembly = Assembly.GetEntryAssembly() ?? typeof(AppVersionInfo).Assembly;
        var name = assembly.GetName();

        return new AppVersionInfo
        {
            ProductName = assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? "Tech Mates Racing Overlay",
            Version = name.Version?.ToString() ?? "0.0.0",
            InformationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? name.Version?.ToString()
                ?? "0.0.0",
            RuntimeVersion = RuntimeInformation.FrameworkDescription,
            OperatingSystem = RuntimeInformation.OSDescription,
            ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString()
        };
    }
}
