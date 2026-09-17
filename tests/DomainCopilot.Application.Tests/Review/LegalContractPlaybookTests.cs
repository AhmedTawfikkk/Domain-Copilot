using DomainCopilot.Application.Documents.Review;

namespace DomainCopilot.Application.Tests.Review;

public sealed class LegalContractPlaybookTests
{
    [Fact]
    public void Assess_WhenLiabilityClauseSaysDamagesAreNotExcluded_AddsFinding()
    {
        var clause = CreateClause(
            LegalClauseType.LimitationOfLiability,
            "Each party's aggregate liability shall not exceed fees paid. " +
            "Indirect, incidental, special, and consequential damages are not excluded.");

        var result = Assess(clause);

        Assert.Contains(
            result.Findings,
            finding => finding.RuleId == "PB-LIAB-003" &&
                       finding.DocumentChunkId == clause.DocumentChunkId);
    }

    [Fact]
    public void Assess_WhenIntellectualPropertyOwnershipIsDeferred_AddsFinding()
    {
        var clause = CreateClause(
            LegalClauseType.IntellectualProperty,
            "The parties shall discuss and agree in good faith on ownership of any such intellectual property. " +
            "Neither party shall have any obligation to assign newly created intellectual property.");

        var result = Assess(clause);

        Assert.Contains(
            result.Findings,
            finding => finding.RuleId == "PB-IP-001" &&
                       finding.DocumentChunkId == clause.DocumentChunkId);
    }

    [Fact]
    public void Assess_WhenTerminationForConvenienceIsImmediate_AddsFinding()
    {
        var clause = CreateClause(
            LegalClauseType.Termination,
            "Either party may terminate this Agreement for convenience by delivering written notice. " +
            "Termination is effective immediately upon delivery, without any cure period or advance notice.");

        var result = Assess(clause);

        Assert.Contains(
            result.Findings,
            finding => finding.RuleId == "PB-TERM-003" &&
                       finding.DocumentChunkId == clause.DocumentChunkId);
    }

    [Fact]
    public void Assess_WhenDefinitionIsMisclassifiedAsConfidentiality_DoesNotAssessItAsAnObligation()
    {
        var definition = CreateClause(
            LegalClauseType.Confidentiality,
            "Confidential Information means all non-public business, technical, and financial information.");
        var obligation = CreateClause(
            LegalClauseType.Confidentiality,
            "Each receiving party shall keep Confidential Information confidential. " +
            "These confidentiality obligations survive termination for three years.");

        var result = Assess(definition, obligation);

        Assert.DoesNotContain(
            result.Findings,
            finding => finding.RuleId is "PB-CONF-001" or "PB-CONF-002");
    }

    private static RiskAssessmentResult Assess(params ExtractedClause[] clauses)
    {
        var playbook = new LegalContractPlaybook();

        return playbook.Assess(new RiskAssessmentRequest(
            Guid.NewGuid(),
            "sample-contract.txt",
            clauses));
    }

    private static ExtractedClause CreateClause(
        LegalClauseType clauseType,
        string evidenceText)
    {
        return new ExtractedClause(
            Guid.NewGuid(),
            clauseType,
            "Test clause.",
            evidenceText,
            null,
            null,
            false,
            1.0);
    }
}
