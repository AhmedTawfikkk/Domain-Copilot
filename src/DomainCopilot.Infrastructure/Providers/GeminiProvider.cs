using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DomainCopilot.Application.Providers;

namespace DomainCopilot.Infrastructure.Providers;

public class GeminiProvider : ILlmProvider
{
    private readonly HttpClient _httpClient;
    // Stable, high-throughput model for short structured extraction and RAG answers.
    private const string CompletionModel = "gemini-3.5-flash-lite";
    private const string EmbeddingModel = "gemini-embedding-001";
    private const int EmbeddingDimensions = 768; // must match pgvector schema (vector(768))

    public GeminiProvider(HttpClient httpClient, string apiKey)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress ??= new Uri("https://generativelanguage.googleapis.com/v1beta/");
        _httpClient.DefaultRequestHeaders.Remove("x-goog-api-key");
        _httpClient.DefaultRequestHeaders.Add("x-goog-api-key", apiKey);
    }

    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        var request = new
        {
            systemInstruction = new { parts = new[] { new { text = systemPrompt } } },
            contents = new[]
            {
                new { role = "user", parts = new[] { new { text = userPrompt } } }
            },
            generationConfig = new
            {
                responseMimeType = "application/json"
            }
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"models/{CompletionModel}:generateContent", request, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);

        return json
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString() ?? string.Empty;
    }

    public async IAsyncEnumerable<string> StreamCompleteAsync(
        string systemPrompt,
        string userPrompt,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var request = new
        {
            systemInstruction = new { parts = new[] { new { text = systemPrompt } } },
            contents = new[]
            {
                new { role = "user", parts = new[] { new { text = userPrompt } } }
            }
        };

        // NOTE: ?alt=sse is required — without it Gemini returns one plain
        // JSON array response instead of a real incremental stream.
        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post, $"models/{CompletionModel}:streamGenerateContent?alt=sse")
        {
            Content = JsonContent.Create(request)
        };

        using var response = await _httpClient.SendAsync(
            httpRequest, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: ")) continue;

            var data = line["data: ".Length..];

            JsonElement json;
            try
            {
                json = JsonSerializer.Deserialize<JsonElement>(data);
            }
            catch (JsonException)
            {
                continue; // skip malformed/partial SSE frames rather than crash the stream
            }

            if (!json.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
                continue;

            var candidate = candidates[0];
            if (!candidate.TryGetProperty("content", out var content)) continue;
            if (!content.TryGetProperty("parts", out var parts)) continue;

            foreach (var part in parts.EnumerateArray())
            {
                if (part.TryGetProperty("text", out var textProp))
                {
                    var text = textProp.GetString();
                    if (!string.IsNullOrEmpty(text))
                        yield return text;
                }
            }
        }
    }

  
    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        var request = new
        {
            model = $"models/{EmbeddingModel}",
            content = new { parts = new[] { new { text } } },
            outputDimensionality = EmbeddingDimensions
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"models/{EmbeddingModel}:embedContent", request, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);

        var values = json
            .GetProperty("embedding")
            .GetProperty("values")
            .EnumerateArray()
            .Select(v => v.GetSingle())
            .ToArray();

        if (values.Length != EmbeddingDimensions)
        {
            throw new InvalidOperationException(
                $"Expected {EmbeddingDimensions} embedding dimensions from Gemini, got {values.Length}.");
        }

        return NormalizeL2(values);
    }

 
    private static float[] NormalizeL2(float[] vector)
    {
        var magnitude = MathF.Sqrt(vector.Sum(v => v * v));
        if (magnitude == 0f) return vector;

        return vector.Select(v => v / magnitude).ToArray();
    }
}
