using DomainCopilot.Api.Security;
using DomainCopilot.Application.Documents.Ingestion;
using DomainCopilot.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace DomainCopilot.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = ApiAuthorizationPolicies.Lawyer)]
public class IngestController : ControllerBase
{
    private const string DefaultSource = "UserUpload";
    private const long MaximumUploadBytes = 20 * 1024 * 1024;

    private static readonly HashSet<string> AllowedExtensions = new(
        StringComparer.OrdinalIgnoreCase)
    {
        ".pdf",
        ".docx",
        ".txt"
    };

    private readonly IDocumentIngestionService _ingestionService;

    public IngestController(IDocumentIngestionService ingestionService)
    {
        _ingestionService = ingestionService;
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaximumUploadBytes)]
    [EnableRateLimiting("expensive-operations")]
    public async Task<IActionResult> Ingest(
         IFormFile file,
        [FromForm] string? source,
        CancellationToken ct)
    {
        if (file.Length == 0)
            return BadRequest("File is empty.");

        if (file.Length > MaximumUploadBytes)
        {
            return StatusCode(
                StatusCodes.Status413PayloadTooLarge,
                "File exceeds the 20 MB upload limit.");
        }

        var extension = Path.GetExtension(file.FileName);

        if (!AllowedExtensions.Contains(extension))
        {
            return BadRequest("Only PDF, DOCX, and TXT files are supported.");
        }

        var resolvedSource = string.IsNullOrWhiteSpace(source) ? DefaultSource : source;

        if (resolvedSource.Length > 500)
        {
            return BadRequest("Source must not exceed 500 characters.");
        }

        await using var stream = file.OpenReadStream();
        var ownerId = Guid.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out var parsedOwnerId)
            ? parsedOwnerId
            : (Guid?)null;
        var command = new IngestDocumentCommand(file.FileName, resolvedSource, stream, ownerId);

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
