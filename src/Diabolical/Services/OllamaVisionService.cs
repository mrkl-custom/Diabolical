using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Diabolical.Models;

namespace Diabolical.Services;

/// <summary>
/// Sends a cropped item-tooltip screenshot to a locally-running Ollama instance serving a
/// vision-capable model, via Ollama's /api/generate endpoint, and parses the strict-JSON
/// response into a ParsedItemExtraction. No SDK — direct REST call, mirroring
/// GeminiVisionService's approach per CLAUDE.md.
/// </summary>
public class OllamaVisionService : IVisionService
{
    private const string DefaultPromptRelativePath = "Prompts/item_extraction_prompt.txt";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _model;
    private readonly string _prompt;

    public OllamaVisionService() : this(new HttpClient(), AppSettingsLoader.Load().Ollama, LoadDefaultPrompt())
    {
    }

    public OllamaVisionService(HttpClient httpClient, OllamaSettings settings, string prompt)
        : this(httpClient, settings.BaseUrl, settings.Model, prompt)
    {
    }

    public OllamaVisionService(HttpClient httpClient, string baseUrl, string model, string prompt)
    {
        _httpClient = httpClient;
        _baseUrl = baseUrl.TrimEnd('/');
        _model = model;
        _prompt = prompt;
    }

    public async Task<ItemExtractionResult> ExtractItemAsync(
        byte[] imageBytes,
        string mimeType = "image/png",
        CancellationToken cancellationToken = default)
    {
        var request = new OllamaGenerateRequest
        {
            Model = _model,
            Prompt = _prompt,
            Images = new List<string> { Convert.ToBase64String(imageBytes) },
            Stream = false
        };

        HttpResponseMessage response;
        try
        {
            var requestUrl = $"{_baseUrl}/api/generate";
            response = await _httpClient.PostAsJsonAsync(requestUrl, request, JsonOptions, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            return ItemExtractionResult.Fail($"Request to Ollama failed: {ex.Message}");
        }

        using (response)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return ItemExtractionResult.Fail($"Ollama returned {(int)response.StatusCode} {response.ReasonPhrase}: {responseBody}");
            }

            string? rawText;
            try
            {
                var envelope = JsonSerializer.Deserialize<OllamaGenerateResponse>(responseBody, JsonOptions);
                rawText = envelope?.Response;
            }
            catch (JsonException ex)
            {
                return ItemExtractionResult.Fail($"Ollama response was not valid JSON: {ex.Message}");
            }

            if (string.IsNullOrWhiteSpace(rawText))
            {
                return ItemExtractionResult.Fail("Ollama response contained no text content.");
            }

            return ExtractionJsonParser.ParseItemJson(rawText, "Ollama");
        }
    }

    public async Task<VisionAvailabilityResult> CheckAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync($"{_baseUrl}/api/tags", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new VisionAvailabilityResult(false, $"Ollama returned {(int)response.StatusCode} {response.ReasonPhrase}.");
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var tags = JsonSerializer.Deserialize<OllamaTagsResponse>(body, JsonOptions);
            var modelNames = tags?.Models?.Select(m => m.Name) ?? Enumerable.Empty<string?>();
            var modelPresent = modelNames.Any(name =>
                string.Equals(name, _model, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name?.Split(':')[0], _model.Split(':')[0], StringComparison.OrdinalIgnoreCase));

            return modelPresent
                ? new VisionAvailabilityResult(true)
                : new VisionAvailabilityResult(false, $"Ollama is reachable, but model '{_model}' isn't pulled locally.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return new VisionAvailabilityResult(false, $"Request to Ollama failed: {ex.Message}");
        }
    }

    private static string LoadDefaultPrompt() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, DefaultPromptRelativePath));
}
