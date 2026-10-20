using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace Agender.Auth;

public sealed class ApiAuthenticationStateProvider : AuthenticationStateProvider, IDisposable
{
    private static readonly ClaimsPrincipal Anonymous = new(new ClaimsIdentity());

    private readonly AuthSession _session;

    public ApiAuthenticationStateProvider(AuthSession session)
    {
        _session = session;
        _session.Changed += OnSessionChanged;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        await _session.InitializeAsync();
        return new AuthenticationState(BuildPrincipal());
    }

    public void Dispose() => _session.Changed -= OnSessionChanged;

    private void OnSessionChanged() => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());

    private ClaimsPrincipal BuildPrincipal()
    {
        if (!_session.IsAuthenticated)
        {
            return Anonymous;
        }

        string email = _session.User?.Email ?? string.Empty;
        string displayName = _session.User?.DisplayName ?? email;

        return new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, email),
                new Claim(ClaimTypes.Name, email),
                new Claim(ClaimTypes.GivenName, displayName)
            ],
            authenticationType: "Bearer"));
    }
}
