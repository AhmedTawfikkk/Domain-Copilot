using System.Security.Claims;
using DomainCopilot.Api.Contracts;

namespace DomainCopilot.Api.Security;

public static class CurrentUserResponseFactory
{
    public static CurrentUserResponse? From(ClaimsPrincipal user)
    {
        if (!Guid.TryParse(
                user.FindFirstValue(ClaimTypes.NameIdentifier),
                out var userId))
        {
            return null;
        }

        return new CurrentUserResponse(
            userId,
            user.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
            user.Identity?.Name ?? string.Empty,
            user.FindFirstValue(ClaimTypes.Role) ?? string.Empty);
    }
}
