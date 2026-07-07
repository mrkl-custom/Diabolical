using System.Text.Json;
using Diabolical.Models;

namespace Diabolical.Services;

/// <summary>
/// Shared markdown-fence-stripping and JSON-parsing logic for turning a vision provider's
/// raw text response into a ParsedItemExtraction. Used by both GeminiVisionService and
/// OllamaVisionService, whose response envelopes differ but whose extracted item JSON
/// (and failure modes) are identical.
/// </summary>
internal static class ExtractionJsonParser
{
    public static string StripMarkdownFences(string text)
    {
        var trimmed = text.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return trimmed;
        }

        var firstNewline = trimmed.IndexOf('\n');
        trimmed = firstNewline >= 0 ? trimmed[(firstNewline + 1)..] : trimmed[3..];

        var closingFenceIndex = trimmed.LastIndexOf("```", StringComparison.Ordinal);
        if (closingFenceIndex >= 0)
        {
            trimmed = trimmed[..closingFenceIndex];
        }

        return trimmed.Trim();
    }

    public static ItemExtractionResult ParseItemJson(string rawText, string providerName)
    {
        var itemJson = StripMarkdownFences(rawText);
        try
        {
            var item = JsonSerializer.Deserialize<ParsedItemExtraction>(itemJson);
            if (item is null)
            {
                return ItemExtractionResult.Fail($"{providerName}'s extracted item JSON deserialized to null.");
            }

            return ItemExtractionResult.Ok(item);
        }
        catch (JsonException ex)
        {
            return ItemExtractionResult.Fail($"{providerName}'s extracted item was not valid JSON: {ex.Message}\nRaw text: {rawText}");
        }
    }
}
