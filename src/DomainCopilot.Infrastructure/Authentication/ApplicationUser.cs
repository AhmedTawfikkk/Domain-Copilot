using Microsoft.AspNetCore.Identity;

namespace DomainCopilot.Infrastructure.Authentication;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
}
