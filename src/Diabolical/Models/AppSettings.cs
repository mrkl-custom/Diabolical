namespace Diabolical.Models;

public class AppSettings
{
    public string VisionProvider { get; set; } = "Gemini";
    public GeminiSettings Gemini { get; set; } = new();
    public OllamaSettings Ollama { get; set; } = new();
    public HotkeySettings Hotkey { get; set; } = new();
    public HotkeySettings QuickCopyHotkey { get; set; } = new();

    /// <summary>
    /// When true, skips the review/edit confirmation on scanned items and the
    /// "are you sure" prompt on equipment removal — actions apply immediately.
    /// </summary>
    public bool YoloMode { get; set; }
}

public class GeminiSettings
{
    public string ApiKey { get; set; } = string.Empty;
}

public class OllamaSettings
{
    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = string.Empty;
}

public class HotkeySettings
{
    public string Modifiers { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
}
