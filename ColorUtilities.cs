namespace CrosshairOverlayTool;

internal static class ColorUtilities
{
    public const string DefaultLineColorHex = "#FF3B30";

    public static bool TryParseHtmlColor(string? text, out Color color)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            color = default;
            return false;
        }

        try
        {
            color = ColorTranslator.FromHtml(text.Trim());
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            color = default;
            return false;
        }
    }

    public static string ToHtml(Color color)
    {
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }
}
