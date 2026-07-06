namespace Diabolical.Models;

public class AppSettings
{
    public GeminiSettings Gemini { get; set; } = new();
    public HotkeySettings Hotkey { get; set; } = new();
}

public class GeminiSettings
{
    public string ApiKey { get; set; } = string.Empty;
}

public class HotkeySettings
{
    public string Modifiers { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
}
