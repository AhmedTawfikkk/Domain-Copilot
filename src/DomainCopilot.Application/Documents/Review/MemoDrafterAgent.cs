using System.Text.Json;
using DomainCopilot.Application.Providers;

namespace DomainCopilot.Application.Documents.Review;

public sealed class MemoDrafterAgent : IMemoDrafterAgent
{
    private const int MaximumMemoCharacters = 20_000;

    private readonly ILlmProvider _llmProvider;
    private readonly IMemoDraftPromptTemplate _promptTemplate;

    public MemoDrafterAgent(
        ILlmProvider llmProvider,
        IMemoDraftPromptTemplate promptTemplate)
    {
        _llmProvider = llmProvider;
        _promptTemplate = promptTemplate;
    }

    public async Task<MemoDraftResult> DraftAsync(
        MemoDraftRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.DocumentId == Guid.Empty)
        {
            return Failed("A document ID is required.");
        }

        var prompts = _promptTemplate.Render(request);

        var modelResponse = await _llmProvider.CompleteAsync(
            prompts.SystemPrompt,
            prompts.UserPrompt,
            cancellationToken);

        return ParseAndValidateResponse(
            modelResponse,
            request.ExtractedClauses);
    }

    private static MemoDraftResult ParseAndValidateResponse(
        string modelResponse,
        IReadOnlyList<ExtractedClause> extractedClauses)
    {
        if (string.IsNullOrWhiteSpace(modelResponse))
        {
            return Failed("The Memo Drafter returned an empty response.");
        }

        try
        {
            using var json = JsonDocument.Parse(modelResponse);

            var root = json.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return Failed(
                    "The Memo Drafter returned an invalid JSON contract.");
            }

            if (!TryGetRequiredString(
                    root,
                    "memoMarkdown",
                    out var memoContent))
            {
                return Failed(
                    "The Memo Drafter omitted the required 'memoMarkdown' field.");
            }

            if (memoContent.Length > MaximumMemoCharacters)
            {
                return Failed(
                    $"The Memo Drafter exceeded the {MaximumMemoCharacters} character limit.");
            }

            if (!root.TryGetProperty(
                    "citationChunkIds",
                    out var citationsElement) ||
                citationsElement.ValueKind != JsonValueKind.Array)
            {
                return Failed(
                    "The Memo Drafter omitted the required 'citationChunkIds' array.");
            }

            var allowedChunkIds = extractedClauses
                .Select(clause => clause.DocumentChunkId)
                .ToHashSet();

            var citationChunkIds = new List<Guid>();

            foreach (var citationElement in citationsElement.EnumerateArray())
            {
                if (citationElement.ValueKind != JsonValueKind.String ||
                    !Guid.TryParse(citationElement.GetString(), out var chunkId))
                {
                    return Failed(
                        "The Memo Drafter returned an invalid citation chunk ID.");
                }

                if (!allowedChunkIds.Contains(chunkId))
                {
                    return Failed(
                        "The Memo Drafter referenced a source chunk that was not supplied.");
                }

                if (!citationChunkIds.Contains(chunkId))
                {
                    citationChunkIds.Add(chunkId);
                }
            }

            if (citationChunkIds.Count == 0)
            {
                return Failed(
                    "The Memo Drafter returned a memo without source citations.");
            }

            return new MemoDraftResult(
                true,
                memoContent.Trim(),
                citationChunkIds,
                null);
        }
        catch (JsonException)
        {
            return Failed(
                "The Memo Drafter returned malformed JSON.");
        }
    }

    private static bool TryGetRequiredString(
        JsonElement element,
        string propertyName,
        out string value)
    {
        value = string.Empty;

        if (!element.TryGetProperty(
                propertyName,
                out var property) ||
            property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString()?.Trim() ?? string.Empty;

        return !string.IsNullOrWhiteSpace(value);
    }

    private static MemoDraftResult Failed(string failureReason)
    {
        return new MemoDraftResult(
            false,
            null,
            Array.Empty<Guid>(),
            failureReason);
    }
}