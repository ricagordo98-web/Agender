using Agender.Shared.Abstractions;
using Agender.Shared.Contracts;
using Agender.Shared.Services;
using Agender.Shared.State;
using Microsoft.AspNetCore.Components;

namespace Agender.Shared.Components;

public abstract class CasaPageBase : ComponentBase, IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan RemoteRefreshDelay = TimeSpan.FromMilliseconds(400);

    private readonly CancellationTokenSource _lifetime = new();
    private int _refreshScheduled;
    private bool _feedHooked;

    [Inject]
    protected IHouseholdService Households { get; set; } = default!;

    [Inject]
    protected IToastService Toasts { get; set; } = default!;

    [Inject]
    protected IChangeFeed Feed { get; set; } = default!;

    [Inject]
    protected NavigationManager Navigation { get; set; } = default!;

    protected HouseholdDto? Current { get; private set; }

    protected bool Loading { get; private set; } = true;

    protected CancellationToken Cancellation => _lifetime.Token;

    protected virtual bool IsEditing => false;

    protected abstract Task LoadAsync(CancellationToken cancellationToken);

    protected override async Task OnInitializedAsync()
    {
        await RefreshAsync(silent: false);
        Loading = false;
        _ = PollAsync();
        await HookFeedAsync();
    }

    protected async Task RefreshAsync(bool silent = true)
    {
        try
        {
            Current = await Households.GetAsync(Cancellation);

            if (Current is not null)
            {
                await LoadAsync(Cancellation);
            }
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            if (!silent)
            {
                Toasts.Show("Não foi possível carregar os dados.", ToastKind.Error);
            }
        }
    }

    protected async Task OnHouseholdReadyAsync()
    {
        await RefreshAsync(silent: false);
        StateHasChanged();
    }

    protected async Task<bool> RunAsync(Func<Task> action, string? success = null, bool reload = true)
    {
        try
        {
            await action();

            if (success is not null)
            {
                Toasts.Show(success, ToastKind.Success);
            }

            if (reload)
            {
                await RefreshAsync(silent: false);
            }

            return true;
        }
        catch (ArgumentException)
        {
            Toasts.Show("Não foi possível guardar. Confirma os dados e tenta novamente.", ToastKind.Error);
        }
        catch (NotFoundException)
        {
            Toasts.Show("O item já não existe ou já não tens acesso.", ToastKind.Error);
            await RefreshAsync(silent: true);
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            Toasts.Show("Não foi possível concluir a operação. Tenta novamente.", ToastKind.Error);
        }

        return false;
    }

    public virtual void Dispose()
    {
        Feed.Changed -= OnRemoteChangeAsync;
        _lifetime.Cancel();
        _lifetime.Dispose();
        GC.SuppressFinalize(this);
    }

    protected static bool IsExpected(Exception ex) =>
        ex is ArgumentException or NotFoundException or PersistenceException or ServerUnavailableException or UnauthorizedAccessException;

    private async Task HookFeedAsync()
    {
        if (_feedHooked)
        {
            return;
        }

        _feedHooked = true;
        Feed.Changed += OnRemoteChangeAsync;

        try
        {
            await Feed.StartAsync(Cancellation);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
        }
    }

    private Task OnRemoteChangeAsync(ChangeNotice notice)
    {
        if (Current is not null && Interlocked.Exchange(ref _refreshScheduled, 1) == 0)
        {
            _ = RefreshSoonAsync();
        }

        return Task.CompletedTask;
    }

    private async Task RefreshSoonAsync()
    {
        try
        {
            await Task.Delay(RemoteRefreshDelay, Cancellation);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        finally
        {
            Interlocked.Exchange(ref _refreshScheduled, 0);
        }

        if (IsEditing)
        {
            return;
        }

        await InvokeAsync(async () =>
        {
            await RefreshAsync(silent: true);
            StateHasChanged();
        });
    }

    private async Task PollAsync()
    {
        using PeriodicTimer timer = new(PollInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(Cancellation))
            {
                if (IsEditing || Current is null)
                {
                    continue;
                }

                await InvokeAsync(async () =>
                {
                    await RefreshAsync(silent: true);
                    StateHasChanged();
                });
            }
        }
        catch (OperationCanceledException)
        {
        }
    }
}
