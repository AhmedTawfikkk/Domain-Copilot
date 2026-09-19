using DomainCopilot.Application.Documents.Evaluation;

namespace DomainCopilot.Application.Tests.Evaluation;

public class GoldenEvaluationCaseCatalogTests
{
    [Fact]
    public void GetAll_ReturnsTwentyFiveUniqueCasesAcrossAllScenarios()
    {
        var catalog = new GoldenEvaluationCaseCatalog();

        var cases = catalog.GetAll();

        Assert.Equal(25, cases.Count);

        Assert.Equal(
            25,
            cases.Select(evaluationCase => evaluationCase.Id)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count());

        Assert.Contains(
            cases,
            evaluationCase =>
                evaluationCase.Scenario ==
                EvaluationScenario.Baseline);

        Assert.Contains(
            cases,
            evaluationCase =>
                evaluationCase.Scenario ==
                EvaluationScenario.OutOfCorpus);

        Assert.Contains(
            cases,
            evaluationCase =>
                evaluationCase.Scenario ==
                EvaluationScenario.Ambiguous);

        Assert.Contains(
            cases,
            evaluationCase =>
                evaluationCase.Scenario ==
                EvaluationScenario.DirectPromptInjection);

        Assert.Contains(
            cases,
            evaluationCase =>
                evaluationCase.Scenario ==
                EvaluationScenario.IndirectPromptInjection);

        Assert.Contains(
            cases,
            evaluationCase =>
                evaluationCase.Scenario ==
                EvaluationScenario.ConflictingSources);
    }
}