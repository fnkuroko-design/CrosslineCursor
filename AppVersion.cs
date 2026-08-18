using System.Reflection;

namespace CrosshairOverlayTool;

internal static class AppVersion
{
    public static string DisplayText { get; } = $"v{GetInformationalVersion()}";

    private static string GetInformationalVersion()
    {
        var assembly = typeof(AppVersion).Assembly;
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            return informationalVersion.Split('+', 2)[0];
        }

        return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }
}
