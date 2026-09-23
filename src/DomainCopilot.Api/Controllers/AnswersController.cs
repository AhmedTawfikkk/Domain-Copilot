using DomainCopilot.Api.Security;
using DomainCopilot.Application.Documents.Answering;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Text.Json;

namespace DomainCopilot.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = ApiAuthorizationPolicies.Lawyer)]
public sealed class AnswersController : ControllerBase
{
    private readonly IGroundedAnswerService _groundedAnswerService;
    private readonly IStreamingGroundedAnswerService _streamingGroundedAnswerService;
    private readonly ILogger<AnswersController> _logger;

    public AnswersController(
        IGroundedAnswerService groundedAnswerService,
        IStreamingGroundedAnswerService streamingGroundedAnswerService,
        ILogger<AnswersController> logger)
    {
        _groundedAnswerService = groundedAnswerService;
        _streamingGroundedAnswerService = streamingGroundedAnswerService;
        _logger = logger;
    }

    [HttpPost]
    [EnableRateLimiting("expensive-operations")]
    public async Task<ActionResult<GroundedAnswerResult>> Answer(
        [FromBody] AnswerRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _groundedAnswerService.AnswerAsync(
                request,
                cancellationToken);

            return result.Status == AnswerStatus.Refused
                ? UnprocessableEntity(result)
                : Ok(result);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { error = exception.Message });
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(exception, "Answer failed due to an LLM provider error.");

            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { error = "The AI provider is temporarily unavailable. Please try again." });
        }
    }

    [HttpPost("stream")]
    [EnableRateLimiting("expensive-operations")]
    public async Task Stream(
        [FromBody] AnswerRequest request,
        CancellationToken cancellationToken = default)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Append("X-Accel-Buffering", "no");

        try
        {
            await foreach (var streamEvent in _streamingGroundedAnswerService
                               .StreamAsync(request, cancellationToken)
                               .WithCancellation(cancellationToken))
            {
                var eventName = streamEvent.Type switch
                {
                    GroundedAnswerStreamEventType.Started => "started",
                    GroundedAnswerStreamEventType.Delta => "delta",
                    GroundedAnswerStreamEventType.Completed => "completed",
                    _ => "refused"
                };

                object payload = streamEvent.Type == GroundedAnswerStreamEventType.Delta
                    ? JsonSerializer.Serialize(new { delta = streamEvent.Delta })
                    : (object?)streamEvent.Result ?? new { };

                var data = payload is string serializedPayload
                    ? serializedPayload
                    : JsonSerializer.Serialize(payload);

                await Response.WriteAsync(
                    $"event: {eventName}\n" +
                    $"data: {data}\n\n",
                    cancellationToken);

                await Response.Body.FlushAsync(cancellationToken);
            }
        }
        catch (ArgumentException exception)
        {
            await WriteErrorAsync("bad-request", exception.Message, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            await WriteErrorAsync("unavailable", exception.Message, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(exception, "Answer stream failed due to an LLM provider error.");
            await WriteErrorAsync("unavailable", "The AI provider is temporarily unavailable. Please try again.", cancellationToken);
        }
    }

    private async Task WriteErrorAsync(
        string code,
        string message,
        CancellationToken cancellationToken)
    {
        var data = JsonSerializer.Serialize(new { code, message });

        await Response.WriteAsync(
            $"event: error\n" +
            $"data: {data}\n\n",
            cancellationToken);

        await Response.Body.FlushAsync(cancellationToken);
    }
}
