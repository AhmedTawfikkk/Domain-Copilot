using System.Runtime.CompilerServices;
using System.Text;
using DomainCopilot.Application.Documents.Retrieval;
using DomainCopilot.Application.Providers;
using Microsoft.Extensions.Logging;

namespace DomainCopilot.Application.Documents.Answering;

public sealed class StreamingGroundedAnswerService : IStreamingGroundedAnswerService
{
    private const string InsufficientEvidenceMessage =
        "I do not have sufficient grounded evidence in the uploaded documents to answer that question.";

    private readonly IChunkRetrievalService _retrievalService;
    private readonly ILlmProvider _llmProvider;
    private readonly IGroundedAnswerPromptTemplate _promptTemplate;
    private readonly ILogger<StreamingGroundedAnswerService> _logger;

    public StreamingGroundedAnswerService(
        IChunkRetrievalService retrievalService,
        ILlmProvider llmProvider,
        IGroundedAnswerPromptTemplate promptTemplate,
        ILogger<StreamingGroundedAnswerService> logger)
    {
        _retrievalService = retrievalService;
        _llmProvider = llmProvider;
        _promptTemplate = promptTemplate;
        _logger = logger;
    }

    public async IAsyncEnumerable<GroundedAnswerStreamEvent> StreamAsync(
        AnswerRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
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
            yield return new GroundedAnswerStreamEvent(
                GroundedAnswerStreamEventType.Refused,
                Result: Refuse("No relevant source chunks were retrieved."));
            yield break;
        }

        var prompts = _promptTemplate.Render(request.Question, retrievedChunks);
        var responseBuilder = new StringBuilder();

        _logger.LogInformation(
            "Grounded answer stream started. RetrievedChunkCount: {RetrievedChunkCount}.",
            retrievedChunks.Count);

        yield return new GroundedAnswerStreamEvent(
            GroundedAnswerStreamEventType.Started);

        await foreach (var delta in _llmProvider.StreamCompleteAsync(
                           prompts.SystemPrompt,
                           prompts.UserPrompt,
                           cancellationToken)
                           .WithCancellation(cancellationToken))
        {
            responseBuilder.Append(delta);

            yield return new GroundedAnswerStreamEvent(
                GroundedAnswerStreamEventType.Delta,
                Delta: delta);
        }

        var result = GroundedAnswerService.BuildValidatedResult(
            responseBuilder.ToString(),
            retrievedChunks);

        _logger.LogInformation(
            "Grounded answer stream completed. Status: {AnswerStatus}; CitationCount: {CitationCount}.",
            result.Status,
            result.Citations.Count);

        yield return new GroundedAnswerStreamEvent(
            result.Status == AnswerStatus.Answered
                ? GroundedAnswerStreamEventType.Completed
                : GroundedAnswerStreamEventType.Refused,
            Result: result);
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
