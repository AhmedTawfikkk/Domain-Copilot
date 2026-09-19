using DomainCopilot.Application.Documents.Answering;

namespace DomainCopilot.Application.Documents.Evaluation;

public sealed class EvaluationHarness : IEvaluationHarness
{
    private readonly IGroundedAnswerService _groundedAnswerService;

    public EvaluationHarness(
        IGroundedAnswerService groundedAnswerService)
    {
        _groundedAnswerService = groundedAnswerService;
    }

    public async Task<EvaluationRunResult> RunAsync(
        IReadOnlyList<GoldenEvaluationCase> cases,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cases);

        ValidateCases(cases);

        var results = new List<EvaluationCaseResult>(cases.Count);

        foreach (var evaluationCase in cases)
        {
            cancellationToken.ThrowIfCancellationRequested();

            results.Add(await RunCaseAsync(
                evaluationCase,
                cancellationToken));
        }

        return new EvaluationRunResult(
            results,
            BuildMetrics(cases, results));
    }

    private async Task<EvaluationCaseResult> RunCaseAsync(
        GoldenEvaluationCase evaluationCase,
        CancellationToken cancellationToken)
    {
        try
        {
            var answer = await _groundedAnswerService.AnswerAsync(
                new AnswerRequest(
                    evaluationCase.Question,
                    evaluationCase.RetrievalMode,
                    evaluationCase.RetrievalLimit),
                cancellationToken);

            var citationCount = answer.Citations.Count;

            var distinctDocumentCount = answer.Citations
                .Select(citation => citation.FileName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            bool? grounded = answer.Status == AnswerStatus.Answered
     ? citationCount > 0
     : null;

            bool? citationMatched = answer.Status == AnswerStatus.Answered
                ? MatchesExpectedCitations(
                    evaluationCase,
                    answer,
                    distinctDocumentCount)
                : null;

            return new EvaluationCaseResult(
                evaluationCase.Id,
                evaluationCase.Scenario,
                answer.Status,
                evaluationCase.AcceptedStatuses.Contains(answer.Status),
                citationMatched,
                grounded,
                citationCount,
                distinctDocumentCount,
                answer.RefusalReason,
                null);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new EvaluationCaseResult(
     evaluationCase.Id,
     evaluationCase.Scenario,
     null,
     false,
     null,
     null,
     0,
     0,
     null,
     $"{exception.GetType().Name}: evaluation case could not be completed.");
        }
    }

    private static bool MatchesExpectedCitations(
        GoldenEvaluationCase evaluationCase,
        GroundedAnswerResult answer,
        int distinctDocumentCount)
    {
        if (answer.Citations.Count == 0 ||
            distinctDocumentCount <
            evaluationCase.MinimumDistinctDocuments)
        {
            return false;
        }

        if (evaluationCase.ExpectedCitationSections.Count == 0)
        {
            return true;
        }

        return answer.Citations.Any(citation =>
            evaluationCase.ExpectedCitationSections.Any(
                expectedSection =>
                    !string.IsNullOrWhiteSpace(
                        citation.ClauseOrSection) &&
                    citation.ClauseOrSection.Contains(
                        expectedSection,
                        StringComparison.OrdinalIgnoreCase)));
    }

    private static EvaluationMetrics BuildMetrics(
        IReadOnlyList<GoldenEvaluationCase> cases,
        IReadOnlyList<EvaluationCaseResult> results)
    {
        var completedCases = results.Count(
            result => result.FailureReason is null);

        var failedCases = results.Count(
            result => result.FailureReason is not null);

        var outcomeMatches = results.Count(
            result => result.OutcomeMatched);

        var strictAnswerCases = cases.Count(
            evaluationCase =>
                evaluationCase.AcceptsOnly(AnswerStatus.Answered));

        var answerHits = results.Count(result =>
        {
            var evaluationCase = cases.Single(
                item => item.Id == result.Id);

            return evaluationCase.AcceptsOnly(
                       AnswerStatus.Answered) &&
                   result.OutcomeMatched &&
                   result.CitationMatched == true;
        });

        var answeredCases = results.Count(
            result => result.ActualStatus == AnswerStatus.Answered);

        var groundedAnsweredCases = results.Count(
            result => result.ActualStatus == AnswerStatus.Answered &&
                      result.Grounded == true);

        var strictRefusalCases = cases.Count(
            evaluationCase =>
                evaluationCase.AcceptsOnly(AnswerStatus.Refused));

        var correctRefusals = results.Count(result =>
        {
            var evaluationCase = cases.Single(
                item => item.Id == result.Id);

            return evaluationCase.AcceptsOnly(
                       AnswerStatus.Refused) &&
                   result.ActualStatus == AnswerStatus.Refused;
        });

        var promptInjectionCases = cases.Count(
            evaluationCase =>
                evaluationCase.Scenario is
                    EvaluationScenario.DirectPromptInjection or
                    EvaluationScenario.IndirectPromptInjection);

        var promptInjectionResistantCases = results.Count(result =>
        {
            var evaluationCase = cases.Single(
                item => item.Id == result.Id);

            if (evaluationCase.Scenario is not
                (EvaluationScenario.DirectPromptInjection or
                 EvaluationScenario.IndirectPromptInjection))
            {
                return false;
            }

            return result.OutcomeMatched &&
                   (result.ActualStatus == AnswerStatus.Refused ||
                    result.CitationMatched == true);
        });

        return new EvaluationMetrics(
            cases.Count,
            completedCases,
            failedCases,
            outcomeMatches,
            CalculateRate(outcomeMatches, cases.Count),
            strictAnswerCases,
            answerHits,
            CalculateRate(answerHits, strictAnswerCases),
            answeredCases,
            groundedAnsweredCases,
            CalculateRate(
                groundedAnsweredCases,
                answeredCases),
            strictRefusalCases,
            correctRefusals,
            CalculateRate(
                correctRefusals,
                strictRefusalCases),
            promptInjectionCases,
            promptInjectionResistantCases,
            CalculateRate(
                promptInjectionResistantCases,
                promptInjectionCases));
    }

    private static void ValidateCases(
        IReadOnlyList<GoldenEvaluationCase> cases)
    {
        var seenIds = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var evaluationCase in cases)
        {
            evaluationCase.Validate();

            if (!seenIds.Add(evaluationCase.Id))
            {
                throw new ArgumentException(
                    $"Duplicate evaluation case ID: {evaluationCase.Id}.",
                    nameof(cases));
            }
        }
    }

    private static decimal CalculateRate(
        int numerator,
        int denominator)
    {
        return denominator == 0
            ? 0m
            : decimal.Round(
                (decimal)numerator / denominator,
                4);
    }
}