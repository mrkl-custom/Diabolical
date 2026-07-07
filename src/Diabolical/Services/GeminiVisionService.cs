using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Diabolical.Models;

namespace Diabolical.Services;

/// <summary>
/// Sends a cropped item-tooltip screenshot to Gemini 2.5 Flash and parses the strict-JSON
/// response into a ParsedItemExtraction. No SDK — direct REST call per CLAUDE.md.
/// </summary>
public class GeminiVisionService : IVisionService
{
    private const string Model = "gemini-2.5-flash";
    private const string ApiBaseUrl = "https://generativelanguage.googleapis.com/v1beta/models";
    private const string DefaultPromptRelativePath = "Prompts/item_extraction_prompt.txt";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _prompt;

    public GeminiVisionService() : this(new HttpClient(), AppSettingsLoader.Load().Gemini.ApiKey, LoadDefaultPrompt())
    {
    }

    public GeminiVisionService(HttpClient httpClient, string apiKey, string prompt)
    {
        _httpClient = httpClient;
        _apiKey = apiKey;
        _prompt = prompt;
    }

    public async Task<ItemExtractionResult> ExtractItemAsync(
        byte[] imageBytes,
        string mimeType = "image/png",
        CancellationToken cancellationToken = default)
    {
        var request = new GeminiGenerateContentRequest
        {
            Contents = new List<GeminiContent>
            {
                new()
                {
                    Parts = new List<GeminiPart>
                    {
                        new() { Text = _prompt },
                        new() { InlineData = new GeminiInlineData { MimeType = mimeType, Data = Convert.ToBase64String(imageBytes) } }
                    }
                }
            }
        };

        HttpResponseMessage response;
        try
        {
            var requestUrl = $"{ApiBaseUrl}/{Model}:generateContent?key={_apiKey}";
            response = await _httpClient.PostAsJsonAsync(requestUrl, request, JsonOptions, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            return ItemExtractionResult.Fail($"Request to Gemini failed: {ex.Message}");
        }

        using (response)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return ItemExtractionResult.Fail($"Gemini returned {(int)response.StatusCode} {response.ReasonPhrase}: {responseBody}");
            }

            string? rawText;
            try
            {
                var envelope = JsonSerializer.Deserialize<GeminiGenerateContentResponse>(responseBody, JsonOptions);
                rawText = envelope?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault(p => p.Text is not null)?.Text;
            }
            catch (JsonException ex)
            {
                return ItemExtractionResult.Fail($"Gemini response envelope was not valid JSON: {ex.Message}");
            }

            if (string.IsNullOrWhiteSpace(rawText))
            {
                return ItemExtractionResult.Fail("Gemini response contained no text content.");
            }

            return ExtractionJsonParser.ParseItemJson(rawText, "Gemini");
        }
    }

    public async Task<VisionAvailabilityResult> CheckAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            return new VisionAvailabilityResult(false, "No Gemini API key configured.");
        }

        try
        {
            var requestUrl = $"{ApiBaseUrl}?key={_apiKey}";
            using var response = await _httpClient.GetAsync(requestUrl, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return new VisionAvailabilityResult(true);
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return new VisionAvailabilityResult(false, $"Gemini returned {(int)response.StatusCode} {response.ReasonPhrase}: {body}");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new VisionAvailabilityResult(false, $"Request to Gemini failed: {ex.Message}");
        }
    }

    private static string LoadDefaultPrompt() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, DefaultPromptRelativePath));
}
