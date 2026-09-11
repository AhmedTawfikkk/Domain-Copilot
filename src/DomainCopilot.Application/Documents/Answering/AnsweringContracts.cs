using DomainCopilot.Application.Documents.Retrieval;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainCopilot.Application.Documents.Answering
{
    public enum AnswerStatus
    {
        Answered,
        Refused
    }
    public sealed record AnswerRequest(
    string Question,
    RetreivalMode RetrievalMode = RetreivalMode.Hybrid,
    int RetrievalLimit = 5);

    public sealed record Citation(
    Guid DocumentChunkId,
    string FileName,
    string Source,
    string Version,
    string? ClauseOrSection,
    int? PageNumber,
    bool LowConfidence);

    public sealed record GroundedAnswerResult(
        AnswerStatus Status,
        string Answer,
        IReadOnlyList<Citation> Citations,
        string? RefusalReason);

    public interface IGroundedAnswerService
    {
        Task<GroundedAnswerResult> AnswerAsync(
            AnswerRequest request,
            CancellationToken cancellationToken = default);
    }

    public interface IGroundedAnswerPromptTemplate
    {
        string Version { get; }

        GroundedAnswerPrompts Render(
            string question,
            IReadOnlyList<RetrievedChunk> chunks);
    }
    public sealed record GroundedAnswerPrompts(
     string SystemPrompt,
     string UserPrompt);


}
