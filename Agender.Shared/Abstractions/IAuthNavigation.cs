namespace Agender.Shared.Abstractions;

public interface IAuthNavigation
{
    string LoginPath { get; }

    string? ChangePasswordPath { get; }

    bool CanLogout { get; }

    Task LogoutAsync();
}
