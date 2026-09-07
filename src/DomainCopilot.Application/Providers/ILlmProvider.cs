using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainCopilot.Application.Providers
{
    public interface ILlmProvider
    {
        Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default);
        IAsyncEnumerable<string> StreamCompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default);
        Task<float[]> EmbedAsync(string text, CancellationToken ct = default);
    }
}
