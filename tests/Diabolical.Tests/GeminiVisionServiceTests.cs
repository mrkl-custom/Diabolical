using System.Net;
using System.Net.Http;
using Diabolical.Models;
using Diabolical.Services;

namespace Diabolical.Tests;

public class GeminiVisionServiceTests
{
    private const string Prompt = "extract the item";

    private static GeminiVisionService CreateService(HttpStatusCode statusCode, string responseBody)
    {
        var handler = new FakeHttpMessageHandler(statusCode, responseBody);
        var httpClient = new HttpClient(handler);
        return new GeminiVisionService(httpClient, apiKey: "test-key", prompt: Prompt);
    }

    private static string WrapAsGeminiEnvelope(string innerText)
    {
        var escaped = innerText.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
        return $$"""
        {
          "candidates": [
            { "content": { "parts": [ { "text": "{{escaped}}" } ] } }
          ]
        }
        """;
    }

    [Fact]
    public async Task ExtractItemAsync_ValidJson_ReturnsParsedItem()
    {
        const string itemJson = """
            {"slot":"helm","name":"Rage of Harrogath","rarity":"Unique","quality":"Ancestral","itemPower":800,
             "affixes":[{"text":"+40% Fury Generation"}],"specialEffects":[],
             "transfigured":false,"modifiable":true}
            """;
        var service = CreateService(HttpStatusCode.OK, WrapAsGeminiEnvelope(itemJson));

        var result = await service.ExtractItemAsync(new byte[] { 1, 2, 3 });

        Assert.True(result.Success);
        Assert.NotNull(result.Item);
        Assert.Equal("helm", result.Item!.Slot);
        Assert.Equal("Rage of Harrogath", result.Item.Name);
        Assert.Equal(ItemRarity.Unique, result.Item.Rarity);
        Assert.Equal(ItemQuality.Ancestral, result.Item.Quality);
        Assert.Equal(800, result.Item.ItemPower);
        Assert.Single(result.Item.Affixes);
        Assert.Empty(result.Item.SpecialEffects);
        Assert.True(result.Item.Modifiable);
    }

    [Fact]
    public async Task ExtractItemAsync_MarkdownFencedJson_StripsFencesAndParses()
    {
        const string itemJson = """
            {"slot":"weapon","name":"Windforce","rarity":"Legendary","quality":"Normal","itemPower":750,
             "affixes":[],"specialEffects":["Aspect of Disobedience"],"transfigured":false,"modifiable":true}
            """;
        var fenced = $"```json\n{itemJson}\n```";
        var service = CreateService(HttpStatusCode.OK, WrapAsGeminiEnvelope(fenced));

        var result = await service.ExtractItemAsync(new byte[] { 1, 2, 3 });

        Assert.True(result.Success);
        Assert.Equal("Windforce", result.Item!.Name);
        Assert.Equal(ItemRarity.Legendary, result.Item.Rarity);
        Assert.Equal("Aspect of Disobedience", Assert.Single(result.Item.SpecialEffects));
    }

    [Fact]
    public async Task ExtractItemAsync_HttpErrorStatus_ReturnsFailureInsteadOfThrowing()
    {
        var service = CreateService(HttpStatusCode.TooManyRequests, "rate limited");

        var result = await service.ExtractItemAsync(new byte[] { 1, 2, 3 });

        Assert.False(result.Success);
        Assert.Null(result.Item);
        Assert.Contains("429", result.ErrorMessage);
    }

    [Fact]
    public async Task ExtractItemAsync_NonJsonBody_ReturnsFailureInsteadOfThrowing()
    {
        var service = CreateService(HttpStatusCode.OK, "this is not json at all");

        var result = await service.ExtractItemAsync(new byte[] { 1, 2, 3 });

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task ExtractItemAsync_ExtractedTextIsNotJson_ReturnsFailureInsteadOfThrowing()
    {
        var service = CreateService(HttpStatusCode.OK, WrapAsGeminiEnvelope("Sorry, I can't read this image."));

        var result = await service.ExtractItemAsync(new byte[] { 1, 2, 3 });

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task ExtractItemAsync_NoCandidates_ReturnsFailureInsteadOfThrowing()
    {
        var service = CreateService(HttpStatusCode.OK, """{ "candidates": [] }""");

        var result = await service.ExtractItemAsync(new byte[] { 1, 2, 3 });

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }
}
