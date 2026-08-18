namespace CrosshairOverlayTool;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        DeleteLegacyDiagnosticLog();

        using var appController = new AppController();
        Application.Run(appController);
    }

    private static void DeleteLegacyDiagnosticLog()
    {
        try
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CrosshairOverlayTool",
                "input-diagnostics.log");

            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // 旧診断ログの削除失敗は起動を妨げない。
        }
    }
}
