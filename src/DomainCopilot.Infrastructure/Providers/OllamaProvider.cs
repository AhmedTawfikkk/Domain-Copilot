using System.Net.Http.Json;
using System.Text.Json;
using DomainCopilot.Application.Providers;
using DomainCopilot.Infrastructure.Observability;

namespace DomainCopilot.Infrastructure.Providers;

public sealed class OllamaProvider : ILlmProvider
{
    private const string ModelName = "llama3.1:8b";
    private const string EmbedModelName = "nomic-embed-text";

    private readonly HttpClient _httpClient;
    private readonly ILlmRequestTelemetryRecorder _telemetryRecorder;

    public OllamaProvider(
        HttpClient httpClient,
        ILlmRequestTelemetryRecorder telemetryRecorder)
    {
        _httpClient = httpClient;
        _telemetryRecorder = telemetryRecorder;
        _httpClient.BaseAddress ??= new Uri(
            Environment.GetEnvironmentVariable("OLLAMA_BASE_URL")
            ?? "http://localhost:11434");
    }

    public async Task<string> CompleteAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken ct = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var request = new
        {
            model = ModelName,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            stream = false,
            format = "json",
            options = new { temperature = 0, num_predict = 512 }
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "/api/chat",
                request,
                ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(
                cancellationToken: ct);
            var content = json
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? string.Empty;

            await RecordAsync(
                "Completion",
                ReadUsage(json),
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
            model = ModelName,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            stream = true,
            format = "json"
        };

        try
        {
            using var httpRequest = new HttpRequestMessage(
                HttpMethod.Post,
                "/api/chat")
            {
                Content = JsonContent.Create(request)
            };

            using var response = await _httpClient.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseHeadersRead,
                ct);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var reader = new StreamReader(stream);

            while (!reader.EndOfStream && !ct.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(ct);
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var json = JsonSerializer.Deserialize<JsonElement>(line);
                usage = ReadUsage(json) ?? usage;

                if (json.TryGetProperty("message", out var message) &&
                    message.TryGetProperty("content", out var content))
                {
                    var text = content.GetString();
                    if (!string.IsNullOrEmpty(text))
                    {
                        yield return text;
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
        var request = new { model = EmbedModelName, prompt = text };

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "/api/embeddings",
                request,
                ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(
                cancellationToken: ct);
            var values = json
                .GetProperty("embedding")
                .EnumerateArray()
                .Select(value => value.GetSingle())
                .ToArray();

            await RecordAsync(
                "Embedding",
                usage: null,
                succeeded: true,
                wasCancelled: false,
                failureReason: null,
                stopwatch.ElapsedMilliseconds);

            return values;
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
                "Ollama",
                operation == "Embedding" ? EmbedModelName : ModelName,
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
        var inputTokens = TryReadInt32(json, "prompt_eval_count");
        var outputTokens = TryReadInt32(json, "eval_count");

        if (!inputTokens.HasValue && !outputTokens.HasValue)
        {
            return null;
        }

        return new LlmTokenUsage(
            inputTokens,
            outputTokens,
            inputTokens.GetValueOrDefault() + outputTokens.GetValueOrDefault());
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

    private sealed record LlmTokenUsage(
        int? InputTokens,
        int? OutputTokens,
        int? TotalTokens);
}
