using DomainCopilot.Application.Documents;
using DomainCopilot.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace DomainCopilot.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class IngestController : ControllerBase
{
    private const string DefaultSource = "UserUpload";

    private readonly IDocumentIngestionService _ingestionService;

    public IngestController(IDocumentIngestionService ingestionService)
    {
        _ingestionService = ingestionService;
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Ingest(
         IFormFile file,
        [FromForm] string? source,
        CancellationToken ct)
    {
        if (file.Length == 0)
            return BadRequest("File is empty.");

        var resolvedSource = string.IsNullOrWhiteSpace(source) ? DefaultSource : source;

        await using var stream = file.OpenReadStream();
        var command = new IngestDocumentCommand(file.FileName, resolvedSource, stream);

        try
        {
            var result = await _ingestionService.IngestAsync(command, ct);

            if (result.Status == DocumentStatus.Failed)
                return UnprocessableEntity(result);

            if (result.IsDuplicate)
                return Conflict(result);

            return CreatedAtAction(nameof(Ingest), new { id = result.DocumentId }, result);
        }
        catch (NotSupportedException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return UnprocessableEntity(ex.Message);
        }
    }
}