using System.Security.Cryptography;
using System.Text;

namespace DomainCopilot.Api.Security;

public sealed class ApiKeyAuthenticationMiddleware
{
    private const string ApiKeyHeaderName = "X-Api-Key";

    private readonly RequestDelegate _next;
    private readonly ApiSecurityOptions _options;

    public ApiKeyAuthenticationMiddleware(
        RequestDelegate next,
        ApiSecurityOptions options)
    {
        _next = next;
        _options = options;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/swagger"))
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(
                ApiKeyHeaderName,
                out var suppliedApiKey) ||
            !IsValidApiKey(suppliedApiKey.ToString()))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "A valid X-Api-Key header is required."
            });
            return;
        }

        await _next(context);
    }

    private bool IsValidApiKey(string suppliedApiKey)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(_options.ApiKey);
        var suppliedBytes = Encoding.UTF8.GetBytes(suppliedApiKey);

        return expectedBytes.Length == suppliedBytes.Length &&
               CryptographicOperations.FixedTimeEquals(
                   expectedBytes,
                   suppliedBytes);
    }
}
