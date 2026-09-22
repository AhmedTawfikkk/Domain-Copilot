using DomainCopilot.Application.Providers;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainCopilot.Infrastructure.Providers
{
    public class FallbackLlmProvider : ILlmProvider
    {
        private readonly ILlmProvider _primary;
        private readonly ILlmProvider _fallback;
        private readonly ILogger<FallbackLlmProvider> _logger;

        public FallbackLlmProvider(ILlmProvider primary, ILlmProvider fallback, ILogger<FallbackLlmProvider> logger)
        {
            _primary = primary;
            _fallback = fallback;
            _logger = logger;
        }
        public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
        {
            try
            {
                return await _primary.CompleteAsync(systemPrompt, userPrompt, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Primary LLM provider failed, falling back to secondary provider.");

                try
                {
                    return await _fallback.CompleteAsync(systemPrompt, userPrompt, ct);
                }
                catch (Exception fallbackException)
                {
                    _logger.LogError(
                        fallbackException,
                        "Fallback LLM provider also failed after the primary provider failed.");
                    throw;
                }
            }
        }

        public async IAsyncEnumerable<string> StreamCompleteAsync(
            string systemPrompt,
            string userPrompt,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            Exception? primaryFailure = null;

            await using var enumerator = _primary
                .StreamCompleteAsync(systemPrompt, userPrompt, ct)
                .GetAsyncEnumerator(ct);

            while (true)
            {
                bool hasMore;
                try
                {
                    hasMore = await enumerator.MoveNextAsync();
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    primaryFailure = ex;
                    break;
                }

                if (!hasMore)
                {
                    yield break;
                }

                yield return enumerator.Current;
            }

            _logger.LogWarning(
                primaryFailure,
                "Primary LLM provider stream failed, switching to secondary provider.");

            await foreach (var chunk in _fallback
                               .StreamCompleteAsync(systemPrompt, userPrompt, ct)
                               .WithCancellation(ct))
            {
                yield return chunk;
            }
        }

        public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
        {
            try
            {
                return await _primary.EmbedAsync(text, ct);
            }
            catch (Exception ex)
            {
               
                _logger.LogWarning(ex, "Primary LLM provider failed for embeddings, falling back to secondary provider.");
                return await _fallback.EmbedAsync(text, ct);
            }
        }
    }

    }
