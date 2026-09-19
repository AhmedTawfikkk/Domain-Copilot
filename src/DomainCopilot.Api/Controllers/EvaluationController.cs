using DomainCopilot.Api.Security;
using DomainCopilot.Application.Documents.Evaluation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DomainCopilot.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = ApiAuthorizationPolicies.Counsel)]
[EnableRateLimiting("expensive-operations")]
public sealed class EvaluationController : ControllerBase
{
    private readonly IGoldenEvaluationCaseCatalog _caseCatalog;
    private readonly IEvaluationHarness _evaluationHarness;

    public EvaluationController(
        IGoldenEvaluationCaseCatalog caseCatalog,
        IEvaluationHarness evaluationHarness)
    {
        _caseCatalog = caseCatalog;
        _evaluationHarness = evaluationHarness;
    }

    [HttpPost("run")]
    public async Task<ActionResult<EvaluationRunResult>> Run(
        CancellationToken cancellationToken = default)
    {
        var result = await _evaluationHarness.RunAsync(
            _caseCatalog.GetAll(),
            cancellationToken);

        return Ok(result);
    }
}