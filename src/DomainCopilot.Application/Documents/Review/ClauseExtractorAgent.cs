using System.Text.Json;
using DomainCopilot.Application.Providers;

namespace DomainCopilot.Application.Documents.Review;

public sealed class ClauseExtractorAgent : IClauseExtractorAgent
{
    private readonly ILlmProvider _llmProvider;
    private readonly IClauseExtractionPromptTemplate _promptTemplate;
    private readonly ReviewExecutionPolicy _policy;

    public ClauseExtractorAgent(
        ILlmProvider llmProvider,
        IClauseExtractionPromptTemplate promptTemplate,
        ReviewExecutionPolicy policy)
    {
        _llmProvider = llmProvider;
        _promptTemplate = promptTemplate;
        _policy = policy;
    }

    public async Task<ClauseExtractionResult> ExtractAsync(
        ClauseExtractionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.DocumentId == Guid.Empty)
        {
            return Failed("A document ID is required.");
        }

        if (request.Chunks.Count == 0)
        {
            return Failed("No source chunks were supplied to the Clause Extractor.");
        }

        var prompts = _promptTemplate.Render(
            request.Chunks,
            _policy.MaxCharactersPerChunk);

        var modelResponse = await _llmProvider.CompleteAsync(
            prompts.SystemPrompt,
            prompts.UserPrompt,
            cancellationToken);

        return ParseAndValidateResponse(
            modelResponse,
            request.Chunks);
    }

    private static ClauseExtractionResult ParseAndValidateResponse(
        string modelResponse,
        IReadOnlyList<ReviewSourceChunk> sourceChunks)
    {
        if (string.IsNullOrWhiteSpace(modelResponse))
        {
            return Failed("The Clause Extractor returned an empty response.");
        }

        try
        {
            modelResponse = StripJsonCodeFence(modelResponse);

            using var json = JsonDocument.Parse(modelResponse);

            if (json.RootElement.ValueKind != JsonValueKind.Object ||
                !json.RootElement.TryGetProperty(
                    "clauses",
                    out var clausesElement) ||
                clausesElement.ValueKind != JsonValueKind.Array)
            {
                return Failed(
                    "The Clause Extractor returned an invalid JSON contract.");
            }

            var sourceChunksById = sourceChunks.ToDictionary(
                chunk => chunk.DocumentChunkId);

            var extractedChunkIds = new HashSet<Guid>();
            var extractedClauses = new List<ExtractedClause>();

            foreach (var clauseElement in clausesElement.EnumerateArray())
            {
                if (!TryParseClause(
                        clauseElement,
                        sourceChunksById,
                        extractedChunkIds,
                        out var clause,
                        out var failureReason))
                {
                    return Failed(failureReason!);
                }

                extractedClauses.Add(clause!);
            }

            if (extractedClauses.Count != sourceChunks.Count)
            {
                return Failed(
                    "The Clause Extractor did not return exactly one result for every source chunk.");
            }

            if (!sourceChunksById.Keys.All(extractedChunkIds.Contains))
            {
                return Failed(
                    "The Clause Extractor omitted one or more source chunk IDs.");
            }

            return new ClauseExtractionResult(
                true,
                extractedClauses,
                null);
        }
        catch (JsonException)
        {
            return Failed(
                "The Clause Extractor returned malformed JSON.");
        }
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

    private static bool TryParseClause(
        JsonElement clauseElement,
        IReadOnlyDictionary<Guid, ReviewSourceChunk> sourceChunksById,
        ISet<Guid> extractedChunkIds,
        out ExtractedClause? clause,
        out string? failureReason)
    {
        clause = null;
        failureReason = null;

        if (clauseElement.ValueKind != JsonValueKind.Object)
        {
            failureReason =
                "The Clause Extractor returned an invalid clause item.";
            return false;
        }

        if (!TryGetGuid(
                clauseElement,
                "chunkId",
                out var documentChunkId))
        {
            failureReason =
                "The Clause Extractor returned a clause without a valid chunk ID.";
            return false;
        }

        if (!sourceChunksById.TryGetValue(
                documentChunkId,
                out var sourceChunk))
        {
            failureReason =
                "The Clause Extractor referenced a chunk that was not supplied.";
            return false;
        }

        if (!extractedChunkIds.Add(documentChunkId))
        {
            failureReason =
                "The Clause Extractor returned the same chunk more than once.";
            return false;
        }

        if (!TryGetString(
                clauseElement,
                "clauseType",
                out var clauseTypeText) ||
            !Enum.TryParse<LegalClauseType>(
                clauseTypeText,
                ignoreCase: true,
                out var clauseType))
        {
            failureReason =
     $"The Clause Extractor returned an unsupported clause type: '{clauseTypeText}'.";
            return false;
        }

        if (!TryGetString(
                clauseElement,
                "summary",
                out var summary) ||
            summary.Length > 1_200)
        {
            failureReason =
                "The Clause Extractor returned an invalid clause summary.";
            return false;
        }

        if (!clauseElement.TryGetProperty(
                "confidence",
                out var confidenceElement) ||
            confidenceElement.ValueKind != JsonValueKind.Number ||
            !confidenceElement.TryGetDouble(out var confidence) ||
            confidence is < 0d or > 1d)
        {
            failureReason =
                "The Clause Extractor returned an invalid confidence value.";
            return false;
        }

        clause = new ExtractedClause(
            sourceChunk.DocumentChunkId,
            clauseType,
            summary.Trim(),
            sourceChunk.Content,
            sourceChunk.ClauseOrSection,
            sourceChunk.PageNumber,
            sourceChunk.LowConfidence,
            confidence);

        return true;
    }

    private static bool TryGetGuid(
        JsonElement element,
        string propertyName,
        out Guid value)
    {
        value = Guid.Empty;

        return element.TryGetProperty(
                   propertyName,
                   out var property) &&
               property.ValueKind == JsonValueKind.String &&
               Guid.TryParse(property.GetString(), out value);
    }

    private static bool TryGetString(
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

    private static ClauseExtractionResult Failed(string failureReason)
    {
        return new ClauseExtractionResult(
            false,
            Array.Empty<ExtractedClause>(),
            failureReason);
    }
}
