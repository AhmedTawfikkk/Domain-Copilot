using System.Net;
using System.Text;
using DomainCopilot.Infrastructure.Observability;
using DomainCopilot.Infrastructure.Providers;

namespace DomainCopilot.Application.Tests.Providers;

public sealed class GeminiProviderTelemetryTests
{
    [Fact]
    public async Task CompleteAsync_WhenGeminiReturnsUsage_RecordsSuccessfulTelemetry()
    {
        var recorder = new RecordingTelemetryRecorder();
        using var client = new HttpClient(new StubHttpMessageHandler(
            HttpStatusCode.OK,
            """
            {
              "candidates": [
                {
                  "content": {
                    "parts": [ { "text": "{\"decision\":\"answer\"}" } ]
                  }
                }
              ],
              "usageMetadata": {
                "promptTokenCount": 12,
                "candidatesTokenCount": 7,
                "totalTokenCount": 19
              }
            }
            """));

        var provider = new GeminiProvider(client, "test-key", recorder);

        var response = await provider.CompleteAsync("system", "user");

        Assert.Equal("{\"decision\":\"answer\"}", response);

        var entry = Assert.Single(recorder.Entries);
        Assert.Equal("Gemini", entry.Provider);
        Assert.Equal("gemini-3.5-flash-lite", entry.Model);
        Assert.Equal("Completion", entry.Operation);
        Assert.Equal(12, entry.InputTokens);
        Assert.Equal(7, entry.OutputTokens);
        Assert.Equal(19, entry.TotalTokens);
        Assert.True(entry.Succeeded);
        Assert.False(entry.WasCancelled);
        Assert.Null(entry.FailureReason);
    }

    [Fact]
    public async Task CompleteAsync_WhenGeminiFails_RecordsFailureTelemetry()
    {
        var recorder = new RecordingTelemetryRecorder();
        using var client = new HttpClient(new StubHttpMessageHandler(
            HttpStatusCode.TooManyRequests,
            """{ "error": { "message": "Rate limited" } }"""));

        var provider = new GeminiProvider(client, "test-key", recorder);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            provider.CompleteAsync("system", "user"));

        var entry = Assert.Single(recorder.Entries);
        Assert.Equal("Gemini", entry.Provider);
        Assert.Equal("Completion", entry.Operation);
        Assert.False(entry.Succeeded);
        Assert.False(entry.WasCancelled);
        Assert.Equal("HttpRequestException", entry.FailureReason);
        Assert.Null(entry.InputTokens);
        Assert.Null(entry.OutputTokens);
    }

    private sealed class RecordingTelemetryRecorder
        : ILlmRequestTelemetryRecorder
    {
        public List<LlmRequestTelemetryEntry> Entries { get; } = new();

        public Task RecordAsync(
            LlmRequestTelemetryEntry entry,
            CancellationToken cancellationToken = default)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _body;

        public StubHttpMessageHandler(HttpStatusCode statusCode, string body)
        {
            _statusCode = statusCode;
            _body = body;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(
                    _body,
                    Encoding.UTF8,
                    "application/json")
            });
        }
    }
}
