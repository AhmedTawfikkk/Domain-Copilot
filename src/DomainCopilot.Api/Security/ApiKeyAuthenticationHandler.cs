using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using System.Text.Encodings.Web;

namespace DomainCopilot.Api.Security;

public sealed class ApiKeyAuthenticationHandler
    : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "ApiKey";

    private const string ApiKeyHeaderName = "X-Api-Key";

    private readonly ApiSecurityOptions _apiSecurityOptions;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ApiSecurityOptions apiSecurityOptions)
        : base(options, logger, encoder)
    {
        _apiSecurityOptions = apiSecurityOptions;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(
                ApiKeyHeaderName,
                out StringValues suppliedApiKeyValues))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var suppliedApiKey = suppliedApiKeyValues.ToString();

        if (string.IsNullOrWhiteSpace(suppliedApiKey))
        {
            return Task.FromResult(
                AuthenticateResult.Fail(
                    "The X-Api-Key header must not be empty."));
        }

        var role = ResolveRole(suppliedApiKey);

        if (role is null)
        {
            return Task.FromResult(
                AuthenticateResult.Fail(
                    "The supplied API key is invalid."));
        }

        var claims = new[]
        {
            new Claim(
                ClaimTypes.Name,
                role == ApiRoles.Lawyer ? "lawyer-api-client" : "counsel-api-client"),
            new Claim(ClaimTypes.Role, role)
        };

        var identity = new ClaimsIdentity(
            claims,
            SchemeName);

        var principal = new ClaimsPrincipal(identity);

        var ticket = new AuthenticationTicket(
            principal,
            SchemeName);

        return Task.FromResult(
            AuthenticateResult.Success(ticket));
    }

    private string? ResolveRole(string suppliedApiKey)
    {
        if (Matches(
                suppliedApiKey,
                _apiSecurityOptions.LawyerApiKey))
        {
            return ApiRoles.Lawyer;
        }

        if (Matches(
                suppliedApiKey,
                _apiSecurityOptions.CounselApiKey))
        {
            return ApiRoles.Counsel;
        }

        return null;
    }

    private static bool Matches(
        string suppliedApiKey,
        string expectedApiKey)
    {
        var suppliedBytes = Encoding.UTF8.GetBytes(suppliedApiKey);
        var expectedBytes = Encoding.UTF8.GetBytes(expectedApiKey);

        return suppliedBytes.Length == expectedBytes.Length &&
               CryptographicOperations.FixedTimeEquals(
                   suppliedBytes,
                   expectedBytes);
    }
}