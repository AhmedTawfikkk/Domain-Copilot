using System.Text.Json;
using DomainCopilot.Application.Documents.Retrieval;
using DomainCopilot.Application.Providers;

namespace DomainCopilot.Application.Documents.Answering;

public sealed class GroundedAnswerService : IGroundedAnswerService
{
    private const string InsufficientEvidenceMessage =
        "I do not have sufficient grounded evidence in the uploaded documents to answer that question.";

    private readonly IChunkRetrievalService _retrievalService;
    private readonly ILlmProvider _llmProvider;
    private readonly IGroundedAnswerPromptTemplate _promptTemplate;

    public GroundedAnswerService(
        IChunkRetrievalService retrievalService,
        ILlmProvider llmProvider,
        IGroundedAnswerPromptTemplate promptTemplate)
    {
        _retrievalService = retrievalService;
        _llmProvider = llmProvider;
        _promptTemplate = promptTemplate;
    }

    public async Task<GroundedAnswerResult> AnswerAsync(
        AnswerRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Question);

        if (request.RetrievalLimit is < 1 or > 10)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.RetrievalLimit),
                "Retrieval limit must be between 1 and 10.");
        }

        var retrievedChunks = await _retrievalService.SearchAsync(
            request.Question,
            request.RetrievalMode,
            request.RetrievalLimit,
            cancellationToken);

        if (retrievedChunks.Count == 0)
        {
            return Refuse("No relevant source chunks were retrieved.");
        }

        var prompts = _promptTemplate.Render(request.Question, retrievedChunks);

        var modelResponse = await _llmProvider.CompleteAsync(
            systemPrompt: prompts.SystemPrompt,
            userPrompt: prompts.UserPrompt,
            ct: cancellationToken);

        return BuildValidatedResult(modelResponse, retrievedChunks);
    }

    internal static GroundedAnswerResult BuildValidatedResult(
        string modelResponse,
        IReadOnlyList<RetrievedChunk> retrievedChunks)
    {
        if (!TryParseModelResponse(
                modelResponse,
                out var decision,
                out var answer,
                out var citationChunkIds,
                out var refusalReason))
        {
            return Refuse(
                "The model returned an invalid grounded-answer format.");
        }

        if (string.Equals(
                decision,
                "refuse",
                StringComparison.OrdinalIgnoreCase))
        {
            return Refuse(refusalReason ?? "The evidence was insufficient.");
        }

        if (!string.Equals(
                decision,
                "answer",
                StringComparison.OrdinalIgnoreCase))
        {
            return Refuse("The model returned an unsupported decision.");
        }

        if (string.IsNullOrWhiteSpace(answer))
        {
            return Refuse("The model returned an empty answer.");
        }

        if (citationChunkIds.Count == 0)
        {
            return Refuse("The model answered without citations.");
        }

        var chunksById = retrievedChunks.ToDictionary(
            chunk => chunk.DocumentChunkId);

        var citations = new List<Citation>();

        foreach (var citationId in citationChunkIds.Distinct())
        {
            if (!chunksById.TryGetValue(citationId, out var chunk))
            {
                return Refuse(
                    "The model referenced a chunk that was not retrieved.");
            }

            citations.Add(new Citation(
                chunk.DocumentChunkId,
                chunk.FileName,
                chunk.Source,
                chunk.Version,
                chunk.ClauseOrSection,
                chunk.PageNumber,
                chunk.LowConfidence));
        }

        return new GroundedAnswerResult(
            AnswerStatus.Answered,
            answer.Trim(),
            citations,
            null);
    }

    private static bool TryParseModelResponse(
        string modelResponse,
        out string? decision,
        out string? answer,
        out IReadOnlyList<Guid> citationChunkIds,
        out string? reason)
    {
        decision = null;
        answer = null;
        reason = null;
        citationChunkIds = Array.Empty<Guid>();

        if (string.IsNullOrWhiteSpace(modelResponse))
        {
            return false;
        }

        try
        {
            using var json = JsonDocument.Parse(modelResponse);

            var root = json.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            decision = GetOptionalString(root, "decision");
            answer = GetOptionalString(root, "answer");
            reason = GetOptionalString(root, "reason");

            if (!root.TryGetProperty(
                    "citationChunkIds",
                    out var citationsElement) ||
                citationsElement.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var parsedIds = new List<Guid>();

            foreach (var citationElement in citationsElement.EnumerateArray())
            {
                if (citationElement.ValueKind != JsonValueKind.String ||
                    !Guid.TryParse(citationElement.GetString(), out var id))
                {
                    return false;
                }

                parsedIds.Add(id);
            }

            citationChunkIds = parsedIds;

            return !string.IsNullOrWhiteSpace(decision);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string? GetOptionalString(
        JsonElement element,
        string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) &&
               property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static GroundedAnswerResult Refuse(string reason)
    {
        return new GroundedAnswerResult(
            AnswerStatus.Refused,
            InsufficientEvidenceMessage,
            Array.Empty<Citation>(),
            reason);
    }
}
