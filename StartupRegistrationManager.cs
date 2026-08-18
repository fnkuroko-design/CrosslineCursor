using System.Diagnostics;
using Microsoft.Win32;

namespace CrosshairOverlayTool;

internal sealed class StartupRegistrationManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private readonly string _applicationName;

    public StartupRegistrationManager(string applicationName)
    {
        _applicationName = applicationName;
    }

    public bool Sync(bool runOnStartup)
    {
        try
        {
            using var runKey = Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (runKey is null)
            {
                return false;
            }

            if (runOnStartup)
            {
                var executablePath = Environment.ProcessPath ?? Application.ExecutablePath;
                runKey.SetValue(_applicationName, $"\"{executablePath}\"");
            }
            else
            {
                runKey.DeleteValue(_applicationName, throwOnMissingValue: false);
            }

            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException)
        {
            Debug.WriteLine($"Failed to update startup registration: {ex}");
            return false;
        }
    }
}
