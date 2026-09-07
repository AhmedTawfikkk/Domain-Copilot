using DomainCopilot.Application.Providers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DomainCopilot.Infrastructure.Providers
{
    public class GroqProvider : ILlmProvider
    {
        private readonly HttpClient _httpClient;
        private const string ModelName = "openai/gpt-oss-20b";

        public GroqProvider(HttpClient httpClient, string apiKey)
        {
            _httpClient = httpClient;
            _httpClient.BaseAddress ??= new Uri("https://api.groq.com/openai/v1/");
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);
        }

        public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
        {
            var request = new
            {
                model = ModelName,
                messages = new[]
                {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
                stream = false
            };

            var response = await _httpClient.PostAsJsonAsync("chat/completions", request, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            return json.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()
                   ?? string.Empty;
        }

        public async IAsyncEnumerable<string> StreamCompleteAsync(
            string systemPrompt,
            string userPrompt,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            var request = new
            {
                model = ModelName,
                messages = new[]
                {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
                stream = true
            };

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
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
                if (data == "[DONE]") break;

                var json = JsonSerializer.Deserialize<JsonElement>(data);
                if (json.GetProperty("choices")[0].TryGetProperty("delta", out var delta) &&
                    delta.TryGetProperty("content", out var content))
                {
                    var text = content.GetString();
                    if (!string.IsNullOrEmpty(text))
                        yield return text;
                }
            }
        }

        public Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
        {
            throw new NotSupportedException(
                "GroqProvider does not support embeddings. Use OllamaProvider for EmbedAsync calls.");
        }

    }
}
