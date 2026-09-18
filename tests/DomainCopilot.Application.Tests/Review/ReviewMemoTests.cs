using DomainCopilot.Domain.Entites;

namespace DomainCopilot.Application.Tests.Review;

public sealed class ReviewMemoTests
{
    [Fact]
    public void CreateDraft_AssignsMemoIdToPersistedEvidenceChildren()
    {
        var citationChunkId = Guid.NewGuid();

        var memo = ReviewMemo.CreateDraft(
            Guid.NewGuid(),
            "Draft memo content.",
            new[] { citationChunkId },
            new[]
            {
                new ReviewMemoRiskFindingDraft(
                    "PB-LIAB-003",
                    "LimitationOfLiability",
                    "Medium",
                    "Indirect damages exclusion is not evident",
                    "The clause does not exclude indirect damages.",
                    "Add an exclusion.",
                    citationChunkId)
            },
            DateTime.UtcNow);

        var citation = Assert.Single(memo.Citations);
        var finding = Assert.Single(memo.RiskFindings);

        Assert.Equal(memo.Id, citation.ReviewMemoId);
        Assert.Equal(memo.Id, finding.ReviewMemoId);
        Assert.Equal(citationChunkId, citation.DocumentChunkId);
        Assert.Equal(citationChunkId, finding.DocumentChunkId);
    }
}
