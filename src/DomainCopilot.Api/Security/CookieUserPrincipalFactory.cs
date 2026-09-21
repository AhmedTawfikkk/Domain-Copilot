using System.Security.Claims;
using DomainCopilot.Application.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace DomainCopilot.Api.Security;

public interface ICookieUserPrincipalFactory
{
    ClaimsPrincipal Create(AuthenticatedUser user);
}

public sealed class CookieUserPrincipalFactory : ICookieUserPrincipalFactory
{
    public ClaimsPrincipal Create(AuthenticatedUser user)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString("D")),
            new Claim(ClaimTypes.Name, user.DisplayName),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role)
        };

        var identity = new ClaimsIdentity(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme);

        return new ClaimsPrincipal(identity);
    }
}
