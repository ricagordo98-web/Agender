using Microsoft.AspNetCore.Identity;

namespace Agender.Infrastructure.Identity;

public class AgenderUser : IdentityUser
{
    public const int DisplayNameMaxLength = 100;

    public string DisplayName { get; set; } = string.Empty;
}
