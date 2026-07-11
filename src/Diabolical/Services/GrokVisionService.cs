using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diabolical.Models;

namespace Diabolical.Services;

/// <summary>
/// Sends a cropped item-tooltip screenshot to xAI's Grok vision models via the OpenAI-compatible
/// /v1/chat/completions endpoint and parses the strict-JSON response into a
/// ParsedItemExtraction. No SDK — direct REST call, mirroring GeminiVisionService's approach
/// per CLAUDE.md.
/// </summary>
public class GrokVisionService : IVisionService
{
    private const string ApiBaseUrl = "https://api.x.ai/v1";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly string _prompt;

    public GrokVisionService(HttpClient httpClient, GrokSettings settings, string prompt)
        : this(httpClient, settings.ApiKey, settings.Model, prompt)
    {
    }

    public GrokVisionService(HttpClient httpClient, string apiKey, string model, string prompt)
    {
        _httpClient = httpClient;
        _apiKey = apiKey;
        _model = model;
        _prompt = prompt;
    }

    public async Task<ItemExtractionResult> ExtractItemAsync(
        byte[] imageBytes,
        string mimeType = "image/png",
        CancellationToken cancellationToken = default)
    {
        var request = new GrokChatCompletionRequest
        {
            Model = _model,
            Messages = new List<GrokMessage>
            {
                new()
                {
                    Role = "user",
                    Content = new List<GrokContentPart>
                    {
                        new() { Type = "text", Text = _prompt },
                        new()
                        {
                            Type = "image_url",
                            ImageUrl = new GrokImageUrl { Url = $"data:{mimeType};base64,{Convert.ToBase64String(imageBytes)}" }
                        }
                    }
                }
            }
        };

        HttpResponseMessage response;
        try
        {
            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{ApiBaseUrl}/chat/completions")
            {
                Content = JsonContent.Create(request, options: JsonOptions)
            };
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            response = await _httpClient.SendAsync(requestMessage, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return ItemExtractionResult.Fail($"Request to Grok failed: {ex.Message}");
        }

        using (response)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return ItemExtractionResult.Fail($"Grok returned {(int)response.StatusCode} {response.ReasonPhrase}: {responseBody}");
            }

            string? rawText;
            try
            {
                var envelope = JsonSerializer.Deserialize<GrokChatCompletionResponse>(responseBody, JsonOptions);
                rawText = envelope?.Choices?.FirstOrDefault()?.Message?.Content;
            }
            catch (JsonException ex)
            {
                return ItemExtractionResult.Fail($"Grok response envelope was not valid JSON: {ex.Message}");
            }

            if (string.IsNullOrWhiteSpace(rawText))
            {
                return ItemExtractionResult.Fail("Grok response contained no text content.");
            }

            return ExtractionJsonParser.ParseItemJson(rawText, "Grok");
        }
    }

    public async Task<VisionAvailabilityResult> CheckAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            return new VisionAvailabilityResult(false, "No Grok API key configured.");
        }

        try
        {
            using var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"{ApiBaseUrl}/models");
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            using var response = await _httpClient.SendAsync(requestMessage, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return new VisionAvailabilityResult(true);
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return new VisionAvailabilityResult(false, $"Grok returned {(int)response.StatusCode} {response.ReasonPhrase}: {body}");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new VisionAvailabilityResult(false, $"Request to Grok failed: {ex.Message}");
        }
    }
}
