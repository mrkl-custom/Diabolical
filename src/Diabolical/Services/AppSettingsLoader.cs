using System.IO;
using System.Text.Json;
using Diabolical.Models;

namespace Diabolical.Services;

/// <summary>
/// Loads appsettings.local.json from the repo root (gitignored — see CLAUDE.md's
/// Config & Secrets section). appsettings.example.json documents the expected shape.
/// </summary>
public static class AppSettingsLoader
{
    private const string FileName = "appsettings.local.json";

    public static AppSettings Load()
    {
        var path = Path.Combine(RepoPaths.FindRepoRoot(), FileName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"{FileName} not found at repo root. Copy appsettings.example.json to {FileName} and fill in your Gemini API key.",
                path);
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<AppSettings>(json)
            ?? throw new InvalidOperationException($"{FileName} is empty or invalid.");
    }
}
