using Agender.Shared.Domain;
using Agender.Shared.Services;

namespace Agender.Shared.State;

public sealed class UserSettingsState(IUserSettingsService service, IToastService toasts)
{
    public event Action? Changed;

    public UserSettings Current { get; private set; } = new();

    public bool IsLoaded { get; private set; }

    public async Task EnsureLoadedAsync()
    {
        if (IsLoaded)
        {
            return;
        }

        try
        {
            Current = await service.GetAsync();
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            toasts.Show("Não foi possível carregar as definições.", ToastKind.Error);
        }

        IsLoaded = true;
        Changed?.Invoke();
    }

    public async Task<bool> SaveAsync(UserSettings settings)
    {
        try
        {
            await service.SaveAsync(settings);
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            toasts.Show("Não foi possível guardar as definições.", ToastKind.Error);
            return false;
        }

        Current = settings;
        Changed?.Invoke();
        return true;
    }

    private static bool IsExpected(Exception ex) =>
        ex is ArgumentException or NotFoundException or PersistenceException or ServerUnavailableException or UnauthorizedAccessException;
}
