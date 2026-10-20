using Agender.Shared.Abstractions;
using Microsoft.AspNetCore.Components;

namespace Agender.Auth;

public sealed class MauiAuthNavigation(AuthSession session, NavigationManager navigation) : IAuthNavigation
{
    public string LoginPath => "/entrar";

    public string? ChangePasswordPath => null;

    public bool CanLogout => true;

    public async Task LogoutAsync()
    {
        await session.LogoutAsync();
        navigation.NavigateTo(LoginPath, forceLoad: true);
    }
}
