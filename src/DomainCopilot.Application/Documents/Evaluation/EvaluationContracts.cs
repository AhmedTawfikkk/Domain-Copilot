using DomainCopilot.Application.Documents.Answering;
using DomainCopilot.Application.Documents.Retrieval;

namespace DomainCopilot.Application.Documents.Evaluation;

public enum EvaluationScenario
{
    Baseline,
    OutOfCorpus,
    Ambiguous,
    DirectPromptInjection,
    IndirectPromptInjection,
    ConflictingSources
}

public sealed record GoldenEvaluationCase(
    string Id,
    EvaluationScenario Scenario,
    string Question,
    IReadOnlyList<AnswerStatus> AcceptedStatuses,
    IReadOnlyList<string> ExpectedCitationSections,
    int MinimumDistinctDocuments = 1,
    RetreivalMode RetrievalMode = RetreivalMode.Hybrid,
    int RetrievalLimit = 5)
{
    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(Question);

        if (AcceptedStatuses.Count == 0)
        {
            throw new ArgumentException(
                "An evaluation case must define at least one accepted status.",
                nameof(AcceptedStatuses));
        }

        if (MinimumDistinctDocuments < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MinimumDistinctDocuments),
                "Minimum distinct documents must be at least one.");
        }

        if (RetrievalLimit is < 1 or > 10)
        {
            throw new ArgumentOutOfRangeException(
                nameof(RetrievalLimit),
                "Retrieval limit must be between 1 and 10.");
        }
    }

    public bool AcceptsOnly(AnswerStatus status)
    {
        return AcceptedStatuses.Count == 1 &&
               AcceptedStatuses[0] == status;
    }
}

public sealed record EvaluationCaseResult(
    string Id,
    EvaluationScenario Scenario,
    AnswerStatus? ActualStatus,
    bool OutcomeMatched,
    bool? CitationMatched,
    bool? Grounded,
    int CitationCount,
    int DistinctDocumentCount,
    string? ResponseReason,
    string? FailureReason);

public sealed record EvaluationMetrics(
    int TotalCases,
    int CompletedCases,
    int FailedCases,
    int OutcomeMatches,
    decimal OutcomeMatchRate,
    int StrictAnswerCases,
    int AnswerHits,
    decimal AnswerHitRate,
    int AnsweredCases,
    int GroundedAnsweredCases,
    decimal GroundednessRate,
    int StrictRefusalCases,
    int CorrectRefusals,
    decimal RefusalCorrectnessRate,
    int PromptInjectionCases,
    int PromptInjectionResistantCases,
    decimal PromptInjectionResistanceRate);

public sealed record EvaluationRunResult(
    IReadOnlyList<EvaluationCaseResult> Cases,
    EvaluationMetrics Metrics);

public interface IEvaluationHarness
{
    Task<EvaluationRunResult> RunAsync(
        IReadOnlyList<GoldenEvaluationCase> cases,
        CancellationToken cancellationToken = default);
}
public interface IGoldenEvaluationCaseCatalog
{
    IReadOnlyList<GoldenEvaluationCase> GetAll();
}