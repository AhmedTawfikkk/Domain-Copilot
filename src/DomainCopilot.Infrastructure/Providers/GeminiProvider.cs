using System.Net.Http.Json;
using System.Text.Json;
using DomainCopilot.Application.Providers;
using DomainCopilot.Infrastructure.Observability;

namespace DomainCopilot.Infrastructure.Providers;

public sealed class GeminiProvider : ILlmProvider
{
    private const string CompletionModel = "gemini-3.5-flash-lite";
    private const string EmbeddingModel = "gemini-embedding-001";
    private const int EmbeddingDimensions = 768;

    private readonly HttpClient _httpClient;
    private readonly ILlmRequestTelemetryRecorder _telemetryRecorder;

    public GeminiProvider(
        HttpClient httpClient,
        string apiKey,
        ILlmRequestTelemetryRecorder telemetryRecorder)
    {
        _httpClient = httpClient;
        _telemetryRecorder = telemetryRecorder;
        _httpClient.BaseAddress ??= new Uri(
            "https://generativelanguage.googleapis.com/v1beta/");
        _httpClient.DefaultRequestHeaders.Remove("x-goog-api-key");
        _httpClient.DefaultRequestHeaders.Add("x-goog-api-key", apiKey);
    }

    public async Task<string> CompleteAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken ct = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var request = new
        {
            systemInstruction = new
            {
                parts = new[] { new { text = systemPrompt } }
            },
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[] { new { text = userPrompt } }
                }
            },
            generationConfig = new { responseMimeType = "application/json" }
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"models/{CompletionModel}:generateContent",
                request,
                ct);
            await EnsureSuccessAsync(response, ct);

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(
                cancellationToken: ct);
            var usage = ReadUsage(json);
            var content = json
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString() ?? string.Empty;

            await RecordAsync(
                "Completion",
                usage,
                succeeded: true,
                wasCancelled: false,
                failureReason: null,
                stopwatch.ElapsedMilliseconds);

            return content;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await RecordAsync(
                "Completion",
                usage: null,
                succeeded: false,
                wasCancelled: true,
                failureReason: "Cancelled",
                stopwatch.ElapsedMilliseconds);
            throw;
        }
        catch (Exception exception)
        {
            await RecordAsync(
                "Completion",
                usage: null,
                succeeded: false,
                wasCancelled: false,
                failureReason: exception.GetType().Name,
                stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    public async IAsyncEnumerable<string> StreamCompleteAsync(
        string systemPrompt,
        string userPrompt,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken ct = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        LlmTokenUsage? usage = null;
        var completed = false;
        var request = new
        {
            systemInstruction = new
            {
                parts = new[] { new { text = systemPrompt } }
            },
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[] { new { text = userPrompt } }
                }
            }
        };

        try
        {
            using var httpRequest = new HttpRequestMessage(
                HttpMethod.Post,
                $"models/{CompletionModel}:streamGenerateContent?alt=sse")
            {
                Content = JsonContent.Create(request)
            };

            using var response = await _httpClient.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseHeadersRead,
                ct);
            await EnsureSuccessAsync(response, ct);

            using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var reader = new StreamReader(stream);

            while (!reader.EndOfStream && !ct.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(ct);
                if (string.IsNullOrWhiteSpace(line) ||
                    !line.StartsWith("data: ", StringComparison.Ordinal))
                {
                    continue;
                }

                var data = line["data: ".Length..];

                JsonElement json;
                try
                {
                    json = JsonSerializer.Deserialize<JsonElement>(data);
                }
                catch (JsonException)
                {
                    continue;
                }

                usage = ReadUsage(json) ?? usage;

                if (!json.TryGetProperty("candidates", out var candidates) ||
                    candidates.GetArrayLength() == 0)
                {
                    continue;
                }

                var candidate = candidates[0];
                if (!candidate.TryGetProperty("content", out var content) ||
                    !content.TryGetProperty("parts", out var parts))
                {
                    continue;
                }

                foreach (var part in parts.EnumerateArray())
                {
                    if (part.TryGetProperty("text", out var textProperty))
                    {
                        var text = textProperty.GetString();
                        if (!string.IsNullOrEmpty(text))
                        {
                            yield return text;
                        }
                    }
                }
            }

            ct.ThrowIfCancellationRequested();

            completed = true;
        }
        finally
        {
            await RecordAsync(
                "StreamingCompletion",
                usage,
                succeeded: completed,
                wasCancelled: !completed && ct.IsCancellationRequested,
                failureReason: completed
                    ? null
                    : ct.IsCancellationRequested
                        ? "Cancelled"
                        : "StreamInterrupted",
                stopwatch.ElapsedMilliseconds);
        }
    }

    public async Task<float[]> EmbedAsync(
        string text,
        CancellationToken ct = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var request = new
        {
            model = $"models/{EmbeddingModel}",
            content = new { parts = new[] { new { text } } },
            outputDimensionality = EmbeddingDimensions
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"models/{EmbeddingModel}:embedContent",
                request,
                ct);
            await EnsureSuccessAsync(response, ct);

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(
                cancellationToken: ct);
            var values = json
                .GetProperty("embedding")
                .GetProperty("values")
                .EnumerateArray()
                .Select(value => value.GetSingle())
                .ToArray();

            if (values.Length != EmbeddingDimensions)
            {
                throw new InvalidOperationException(
                    $"Expected {EmbeddingDimensions} embedding dimensions from Gemini, got {values.Length}.");
            }

            await RecordAsync(
                "Embedding",
                ReadUsage(json),
                succeeded: true,
                wasCancelled: false,
                failureReason: null,
                stopwatch.ElapsedMilliseconds);

            return NormalizeL2(values);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await RecordAsync(
                "Embedding",
                usage: null,
                succeeded: false,
                wasCancelled: true,
                failureReason: "Cancelled",
                stopwatch.ElapsedMilliseconds);
            throw;
        }
        catch (Exception exception)
        {
            await RecordAsync(
                "Embedding",
                usage: null,
                succeeded: false,
                wasCancelled: false,
                failureReason: exception.GetType().Name,
                stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    private Task RecordAsync(
        string operation,
        LlmTokenUsage? usage,
        bool succeeded,
        bool wasCancelled,
        string? failureReason,
        long durationMilliseconds)
    {
        return _telemetryRecorder.RecordAsync(
            new LlmRequestTelemetryEntry(
                "Gemini",
                operation == "Embedding" ? EmbeddingModel : CompletionModel,
                operation,
                usage?.InputTokens,
                usage?.OutputTokens,
                usage?.TotalTokens,
                succeeded,
                wasCancelled,
                failureReason,
                durationMilliseconds),
            CancellationToken.None);
    }

    private static LlmTokenUsage? ReadUsage(JsonElement json)
    {
        if (!json.TryGetProperty("usageMetadata", out var usageMetadata))
        {
            return null;
        }

        return new LlmTokenUsage(
            TryReadInt32(usageMetadata, "promptTokenCount"),
            TryReadInt32(usageMetadata, "candidatesTokenCount"),
            TryReadInt32(usageMetadata, "totalTokenCount"));
    }

    private static int? TryReadInt32(
        JsonElement element,
        string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) &&
               property.TryGetInt32(out var value)
            ? value
            : null;
    }

    private static float[] NormalizeL2(float[] vector)
    {
        var magnitude = MathF.Sqrt(vector.Sum(value => value * value));
        return magnitude == 0f
            ? vector
            : vector.Select(value => value / magnitude).ToArray();
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var errorBody = await response.Content.ReadAsStringAsync(
            cancellationToken);

        if (errorBody.Length > 1_000)
        {
            errorBody = errorBody[..1_000];
        }

        throw new HttpRequestException(
            $"Gemini request failed with {(int)response.StatusCode} " +
            $"({response.ReasonPhrase}). Response: {errorBody}");
    }

    private sealed record LlmTokenUsage(
        int? InputTokens,
        int? OutputTokens,
        int? TotalTokens);
}
