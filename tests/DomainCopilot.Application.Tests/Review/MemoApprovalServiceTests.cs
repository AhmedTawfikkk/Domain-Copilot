using DomainCopilot.Application.Documents.Review;
using DomainCopilot.Domain.Entites;
using DomainCopilot.Domain.Enums;
using Moq;

namespace DomainCopilot.Application.Tests.Review;

public sealed class MemoApprovalServiceTests
{
    [Fact]
    public async Task ApproveAsync_WhenDraftExists_ApprovesMemo()
    {
        var memo = CreateDraft();

        var repository = CreateRepository(memo);

        var service = new MemoApprovalService(repository.Object);

        var result = await service.ApproveAsync(
            memo.Id,
            new ApproveMemoRequest(
                "Counsel One",
                "Approved for finalization."));

        Assert.Equal(MemoApprovalStatus.Approved, result.ApprovalStatus);
        Assert.Equal("Counsel One", result.DecidedBy);
        Assert.NotNull(result.DecidedAtUtc);

        repository.Verify(item => item.SaveChangesAsync(
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RejectAsync_WhenDraftExists_RejectsMemo()
    {
        var memo = CreateDraft();

        var repository = CreateRepository(memo);

        var service = new MemoApprovalService(repository.Object);

        var result = await service.RejectAsync(
            memo.Id,
            new RejectMemoRequest(
                "Counsel One",
                "Clarify the liability-cap analysis."));

        Assert.Equal(MemoApprovalStatus.Rejected, result.ApprovalStatus);
        Assert.Equal(
            "Clarify the liability-cap analysis.",
            result.DecisionComment);
    }

    [Fact]
    public async Task EditAndApproveAsync_PreservesOriginalDraftAndApprovesRevision()
    {
        var memo = CreateDraft();

        var repository = CreateRepository(memo);

        var service = new MemoApprovalService(repository.Object);

        var result = await service.EditAndApproveAsync(
            memo.Id,
            new EditAndApproveMemoRequest(
                "Counsel One",
                "## Executive Summary\n\nCounsel revised this memo.",
                "Revised before approval."));

        Assert.Equal(MemoApprovalStatus.Approved, result.ApprovalStatus);
        Assert.Equal(
            "Initial draft memo.",
            result.OriginalContent);
        Assert.Contains(
            "Counsel revised this memo.",
            result.Content);
    }

    [Fact]
    public async Task GetApprovedForFinalizationAsync_WhenMemoIsDraft_Throws()
    {
        var memo = CreateDraft();

        var repository = CreateRepository(memo);

        var service = new MemoApprovalService(repository.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetApprovedForFinalizationAsync(memo.Id));
    }

    [Fact]
    public async Task GetApprovedForFinalizationAsync_WhenMemoIsApproved_ReturnsMemo()
    {
        var memo = CreateDraft();

        memo.Approve(
            "Counsel One",
            null,
            DateTime.UtcNow);

        var repository = CreateRepository(memo);

        var service = new MemoApprovalService(repository.Object);

        var result = await service.GetApprovedForFinalizationAsync(
            memo.Id);

        Assert.Equal(MemoApprovalStatus.Approved, result.ApprovalStatus);
        Assert.Equal(memo.Id, result.MemoId);
    }

    private static ReviewMemo CreateDraft()
    {
        return ReviewMemo.CreateDraft(
            Guid.NewGuid(),
            "Initial draft memo.",
            new[] { Guid.NewGuid() },
            Array.Empty<ReviewMemoRiskFindingDraft>(),
            DateTime.UtcNow);
    }

    private static Mock<IReviewMemoRepository> CreateRepository(
        ReviewMemo memo)
    {
        var repository = new Mock<IReviewMemoRepository>();

        repository.Setup(item => item.GetByIdAsync(
                memo.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(memo);

        repository.Setup(item => item.SaveChangesAsync(
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return repository;
    }
}