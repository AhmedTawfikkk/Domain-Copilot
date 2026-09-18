using DomainCopilot.Application.Documents.Review;
using DomainCopilot.Domain.Enums;

namespace DomainCopilot.Application.Tests.Review;

public sealed class MemoDraftPromptTemplateTests
{
    [Fact]
    public void Render_UsesTypedClauseSummaryWithoutRepeatingRawEvidence()
    {
        var rawEvidence = new string('x', 5_000);
        var clause = new ExtractedClause(
            Guid.NewGuid(),
            LegalClauseType.Payment,
            "Payment is due within thirty days.",
            rawEvidence,
            "SECTION 2 PAYMENT",
            1,
            false,
            0.95);

        var template = new MemoDraftPromptTemplate();

        var prompts = template.Render(new MemoDraftRequest(
            Guid.NewGuid(),
            "sample-agreement.pdf",
            "Dataset",
            "1.0",
            new[] { clause },
            Array.Empty<RiskFinding>()));

        Assert.Contains("Payment is due within thirty days.", prompts.UserPrompt);
        Assert.DoesNotContain(rawEvidence, prompts.UserPrompt);
    }
}
