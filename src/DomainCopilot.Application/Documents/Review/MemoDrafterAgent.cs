using System.Text.Json;
using DomainCopilot.Application.Providers;
using Microsoft.Extensions.Logging;

namespace DomainCopilot.Application.Documents.Review;

public sealed class MemoDrafterAgent : IMemoDrafterAgent
{
    private const int MaximumMemoCharacters = 20_000;

    private readonly ILlmProvider _llmProvider;
    private readonly IMemoDraftPromptTemplate _promptTemplate;
    private readonly ILogger<MemoDrafterAgent> _logger;

    public MemoDrafterAgent(
        ILlmProvider llmProvider,
        IMemoDraftPromptTemplate promptTemplate,
        ILogger<MemoDrafterAgent> logger)
    {
        _llmProvider = llmProvider;
        _promptTemplate = promptTemplate;
        _logger = logger;
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

        _logger.LogInformation(
            "Memo drafting started. DocumentId: {DocumentId}; ExtractedClauseCount: {ExtractedClauseCount}; RiskFindingCount: {RiskFindingCount}.",
            request.DocumentId,
            request.ExtractedClauses.Count,
            request.RiskFindings.Count);

        var modelResponse = await _llmProvider.CompleteAsync(
            prompts.SystemPrompt,
            prompts.UserPrompt,
            cancellationToken);

        var result = ParseAndValidateResponse(
            modelResponse,
            request);

        if (result.Succeeded)
        {
            _logger.LogInformation(
                "Memo drafting completed. DocumentId: {DocumentId}; CitationCount: {CitationCount}.",
                request.DocumentId,
                result.CitationChunkIds.Count);
        }
        else
        {
            _logger.LogWarning(
                "Memo drafting failed validation. DocumentId: {DocumentId}; FailureReason: {FailureReason}.",
                request.DocumentId,
                result.FailureReason);
        }

        return result;
    }

    private static MemoDraftResult ParseAndValidateResponse(
        string modelResponse,
        MemoDraftRequest request)
    {
        if (string.IsNullOrWhiteSpace(modelResponse))
        {
            return Failed("The Memo Drafter returned an empty response.");
        }

        try
        {
            modelResponse = StripJsonCodeFence(modelResponse);

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

            var citationChunkIds = BuildDeterministicCitationIds(request);

            if (citationChunkIds.Count == 0)
            {
                return Failed(
                    "The memo cannot be grounded because no source chunks were supplied.");
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

    private static IReadOnlyList<Guid> BuildDeterministicCitationIds(
        MemoDraftRequest request)
    {
        var riskSourceChunkIds = request.RiskFindings
            .Where(finding => finding.DocumentChunkId.HasValue)
            .Select(finding => finding.DocumentChunkId!.Value)
            .Distinct()
            .ToList();

        return riskSourceChunkIds.Count > 0
            ? riskSourceChunkIds
            : request.ExtractedClauses
                .Select(clause => clause.DocumentChunkId)
                .Distinct()
                .ToList();
    }

    private static string StripJsonCodeFence(string response)
    {
        var trimmed = response.Trim();

        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return trimmed;
        }

        var firstLineEnd = trimmed.IndexOf('\n');

        if (firstLineEnd < 0)
        {
            return trimmed;
        }

        var openingFence = trimmed[..firstLineEnd].Trim();

        if (!string.Equals(openingFence, "```json", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(openingFence, "```", StringComparison.Ordinal))
        {
            return trimmed;
        }

        var closingFenceIndex = trimmed.LastIndexOf(
            "```",
            StringComparison.Ordinal);

        if (closingFenceIndex <= firstLineEnd)
        {
            return trimmed;
        }

        return trimmed[(firstLineEnd + 1)..closingFenceIndex].Trim();
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
