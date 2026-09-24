using DomainCopilot.Application.Documents.Review;
using DomainCopilot.Domain.Enums;
using Moq;

namespace DomainCopilot.Application.Tests.Review;

public sealed class ReviewMemoExportServiceTests
{
    [Fact]
    public async Task ExportApprovedDocxAsync_WhenMemoIsDraft_ThrowsBeforeLoadingSource()
    {
        var approvalService = new Mock<IMemoApprovalService>();
        var repository = new Mock<IReviewMemoRepository>();
        var renderer = new Mock<IReviewMemoDocxRenderer>();

        var memoId = Guid.NewGuid();

        approvalService
            .Setup(service => service.GetApprovedForFinalizationAsync(
                memoId,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(
                "Counsel approval is required."));

        var service = new ReviewMemoExportService(
            approvalService.Object,
            repository.Object,
            renderer.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ExportApprovedDocxAsync(memoId));

        repository.Verify(repository => repository.GetExportSourceAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        renderer.Verify(renderer => renderer.Render(
                It.IsAny<ReviewMemoExportSource>()),
            Times.Never);
    }

    [Fact]
    public async Task ExportApprovedDocxAsync_WhenApprovedWithCitations_ReturnsDocx()
    {
        var memoId = Guid.NewGuid();

        var approvalService = new Mock<IMemoApprovalService>();
        var repository = new Mock<IReviewMemoRepository>();
        var renderer = new Mock<IReviewMemoDocxRenderer>();

        approvalService
            .Setup(service => service.GetApprovedForFinalizationAsync(
                memoId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReviewMemoDetails(
                memoId,
                Guid.NewGuid(),
                "Original memo.",
                "Approved memo.",
                MemoApprovalStatus.Approved,
                DateTime.UtcNow,
                DateTime.UtcNow,
                "Counsel One",
                null,
                new MemoCitationExportSource[0]));

        repository
            .Setup(repository => repository.GetExportSourceAsync(
                memoId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSource(memoId));

        renderer
            .Setup(renderer => renderer.Render(
                It.IsAny<ReviewMemoExportSource>()))
            .Returns(new byte[] { 1, 2, 3 });

        var service = new ReviewMemoExportService(
            approvalService.Object,
            repository.Object,
            renderer.Object);

        var result = await service.ExportApprovedDocxAsync(memoId);

        Assert.Equal(
            $"review-memo-{memoId:N}.docx",
            result.FileName);

        Assert.Equal(new byte[] { 1, 2, 3 }, result.Content);

        renderer.Verify(renderer => renderer.Render(
                It.Is<ReviewMemoExportSource>(
                    source => source.MemoId == memoId)),
            Times.Once);
    }

    private static ReviewMemoExportSource CreateSource(
        Guid memoId)
    {
        return new ReviewMemoExportSource(
            memoId,
            Guid.NewGuid(),
            "## Executive Summary\n\nA cited risk was identified.",
            DateTime.UtcNow,
            DateTime.UtcNow,
            "Counsel One",
            new[]
            {
                new MemoCitationExportSource(
                    Guid.NewGuid(),
                    0,
                    "sample-agreement.pdf",
                    "SECTION 7 LIMITATION OF LIABILITY",
                    4,
                    0.96,
                    false)
            },
            new[]
            {
                new MemoRiskFindingExportSource(
                    "PB-LIAB-003",
                    "LimitationOfLiability",
                    "Medium",
                    "Indirect-damages exclusion is not evident",
                    "The clause does not clearly exclude consequential damages.",
                    "Consider an indirect-damages exclusion.",
                    null,
                    0)
            });
    }
}