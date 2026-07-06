namespace Diabolical.Models;

/// <summary>
/// Outcome of a GeminiVisionService extraction call. A failure here (bad HTTP status,
/// unparseable response) is surfaced through this result rather than an exception, so
/// the capture flow can show the user a message instead of crashing.
/// </summary>
public class ItemExtractionResult
{
    public bool Success { get; init; }
    public ParsedItemExtraction? Item { get; init; }
    public string? ErrorMessage { get; init; }

    public static ItemExtractionResult Ok(ParsedItemExtraction item) => new() { Success = true, Item = item };

    public static ItemExtractionResult Fail(string errorMessage) => new() { Success = false, ErrorMessage = errorMessage };
}
