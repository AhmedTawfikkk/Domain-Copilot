using Microsoft.Extensions.Logging;

namespace DomainCopilot.Application.Documents.Review;

public sealed class RiskAssessorAgent : IRiskAssessorAgent
{
    private readonly IContractReviewPlaybook _playbook;
    private readonly ILogger<RiskAssessorAgent> _logger;

    public RiskAssessorAgent(
        IContractReviewPlaybook playbook,
        ILogger<RiskAssessorAgent> logger)
    {
        _playbook = playbook;
        _logger = logger;
    }

    public Task<RiskAssessmentResult> AssessAsync(
        RiskAssessmentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation(
            "Risk assessment started. DocumentId: {DocumentId}; ExtractedClauseCount: {ExtractedClauseCount}.",
            request.DocumentId,
            request.Clauses.Count);

        var result = _playbook.Assess(request);

        _logger.LogInformation(
            "Risk assessment completed. DocumentId: {DocumentId}; RiskFindingCount: {RiskFindingCount}.",
            request.DocumentId,
            result.Findings.Count);

        return Task.FromResult(result);
    }
}
