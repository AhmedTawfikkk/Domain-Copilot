using System.Diagnostics;

namespace DomainCopilot.Api.Observability;

public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-ID";

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ILogger<CorrelationIdMiddleware> logger)
    {
        var correlationId = ResolveCorrelationId(context);

        context.TraceIdentifier = correlationId;

        context.Response.Headers[HeaderName] = correlationId;

        Activity.Current?.SetTag(
            "correlation.id",
            correlationId);

        using (logger.BeginScope(
      "CorrelationId: {CorrelationId}",
      correlationId))
        {
            var stopwatch = Stopwatch.StartNew();

            logger.LogInformation(
                "Request started. Method: {Method}; Path: {Path}.",
                context.Request.Method,
                context.Request.Path);

            try
            {
                await _next(context);
            }
            finally
            {
                stopwatch.Stop();

                logger.LogInformation(
                    "Request completed. Method: {Method}; Path: {Path}; " +
                    "StatusCode: {StatusCode}; ElapsedMilliseconds: " +
                    "{ElapsedMilliseconds}.",
                    context.Request.Method,
                    context.Request.Path,
                    context.Response.StatusCode,
                    stopwatch.ElapsedMilliseconds);
            }
        }
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(
                HeaderName,
                out var suppliedValue) &&
            Guid.TryParse(
                suppliedValue.ToString(),
                out var suppliedCorrelationId))
        {
            return suppliedCorrelationId.ToString("D");
        }

        return Guid.NewGuid().ToString("D");
    }
}