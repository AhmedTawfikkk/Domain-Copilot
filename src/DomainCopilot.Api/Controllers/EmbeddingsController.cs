using DomainCopilot.Application.Documents.Retrieval;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DomainCopilot.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public sealed class EmbeddingsController : ControllerBase
    {
        private readonly IEmbeddingIndexingService _indexingService;

        public EmbeddingsController(
            IEmbeddingIndexingService indexingService)
        {
            _indexingService = indexingService;
        }

        [HttpPost("index")]
        public async Task<IActionResult> IndexPendingChunks(
            [FromQuery] int batchSize = 32,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await _indexingService
                    .IndexPendingChunksAsync(
                        batchSize,
                        cancellationToken);

                return Ok(result);
            }
            catch (ArgumentOutOfRangeException exception)
            {
                return BadRequest(exception.Message);
            }
        }
    }
}
