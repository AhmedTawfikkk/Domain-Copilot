namespace DomainCopilot.Application.Documents.Review;

public sealed class RiskAssessorAgent : IRiskAssessorAgent
{
    private readonly IContractReviewPlaybook _playbook;

    public RiskAssessorAgent(IContractReviewPlaybook playbook)
    {
        _playbook = playbook;
    }

    public Task<RiskAssessmentResult> AssessAsync(
        RiskAssessmentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        cancellationToken.ThrowIfCancellationRequested();

        var result = _playbook.Assess(request);

        return Task.FromResult(result);
    }
}