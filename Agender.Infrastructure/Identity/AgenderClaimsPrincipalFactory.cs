using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Agender.Infrastructure.Identity;

public class AgenderClaimsPrincipalFactory(UserManager<AgenderUser> userManager, IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<AgenderUser>(userManager, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(AgenderUser user)
    {
        ClaimsIdentity identity = await base.GenerateClaimsAsync(user);

        if (!string.IsNullOrWhiteSpace(user.DisplayName))
        {
            identity.AddClaim(new Claim(ClaimTypes.GivenName, user.DisplayName));
        }

        return identity;
    }
}
