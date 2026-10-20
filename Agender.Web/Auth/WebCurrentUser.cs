using System.Security.Claims;
using Agender.Infrastructure.Identity;
using Microsoft.AspNetCore.Components.Authorization;

namespace Agender.Web.Auth;

public class WebCurrentUser(IHttpContextAccessor httpContextAccessor, AuthenticationStateProvider authenticationStateProvider) : ICurrentUser
{
    public async Task<string> GetUserIdAsync(CancellationToken cancellationToken = default)
    {
        ClaimsPrincipal? principal = httpContextAccessor.HttpContext?.User;

        if (principal?.Identity?.IsAuthenticated != true)
        {
            AuthenticationState state = await authenticationStateProvider.GetAuthenticationStateAsync();
            principal = state.User;
        }

        return principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("There is no authenticated user.");
    }
}
