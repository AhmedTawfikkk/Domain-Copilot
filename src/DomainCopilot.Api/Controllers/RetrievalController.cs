using DomainCopilot.Api.Security;
using DomainCopilot.Application.Documents.Retrieval;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DomainCopilot.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = ApiAuthorizationPolicies.Lawyer)]
    public sealed class RetrievalController : ControllerBase
    {
        private readonly IChunkRetrievalService _retrievalService;

        public RetrievalController(
            IChunkRetrievalService retrievalService)
        {
            _retrievalService = retrievalService;
        }

        [HttpGet]
        [EnableRateLimiting("retrieval")]
        public async Task<ActionResult<IReadOnlyList<RetrievedChunk>>> Search(
            [FromQuery] string query,
            [FromQuery] RetreivalMode mode = RetreivalMode.Hybrid,
            [FromQuery] int limit = 5,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var results = await _retrievalService.SearchAsync(
                    query,
                    mode,
                    limit,
                    cancellationToken);

                return Ok(results);
            }
            catch (ArgumentException exception)
            {
                return BadRequest(exception.Message);
            }
        }
    }
}
