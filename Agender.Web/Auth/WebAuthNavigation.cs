using Agender.Shared.Abstractions;
using Microsoft.AspNetCore.Components;

namespace Agender.Web.Auth;

public class WebAuthNavigation(NavigationManager navigation) : IAuthNavigation
{
    public string LoginPath => "/conta/entrar";

    public string? ChangePasswordPath => "/conta/palavra-passe";

    public bool CanLogout => true;

    public Task LogoutAsync()
    {
        navigation.NavigateTo("/conta/sair", forceLoad: true);
        return Task.CompletedTask;
    }
}
