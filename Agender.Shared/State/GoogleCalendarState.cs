using Agender.Shared.Abstractions;
using Agender.Shared.Google;
using Agender.Shared.Services;

namespace Agender.Shared.State;

public sealed class GoogleCalendarState(
    IGoogleCalendarService service,
    IToastService toasts,
    IAppActivation activation) : IDisposable
{
    private readonly Dictionary<DateOnly, IReadOnlyList<GoogleEvent>> _eventsByDate = [];
    private (DateOnly From, DateOnly To)? _loadedRange;
    private bool _hooked;

    public event Action? Changed;

    public GoogleStatus Status { get; private set; } = new(false);

    public bool IsLoaded { get; private set; }

    public bool ShowGoogle { get; private set; }

    public bool IsLoadingEvents { get; private set; }

    public bool HasEventsError { get; private set; }

    public async Task EnsureStatusAsync()
    {
        Hook();

        if (!IsLoaded)
        {
            await RefreshStatusAsync();
        }
    }

    public async Task RefreshStatusAsync()
    {
        try
        {
            Status = await service.GetStatusAsync();
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            Status = new GoogleStatus(false);
        }

        if (!Status.IsConnected)
        {
            ShowGoogle = false;
        }

        IsLoaded = true;
        Changed?.Invoke();
    }

    public void SetShowGoogle(bool show)
    {
        ShowGoogle = show && Status.IsConnected;
        Changed?.Invoke();
    }

    public IReadOnlyList<GoogleEvent> EventsOn(DateOnly date) =>
        _eventsByDate.TryGetValue(date, out IReadOnlyList<GoogleEvent>? events) ? events : [];

    public async Task LoadRangeAsync(DateOnly from, DateOnly to, bool force = false)
    {
        if (!Status.IsConnected || !ShowGoogle || (!force && _loadedRange == (from, to)))
        {
            return;
        }

        IsLoadingEvents = true;
        HasEventsError = false;
        Changed?.Invoke();

        try
        {
            IReadOnlyList<GoogleEvent> events = await service.GetEventsAsync(from, to, includeOwn: true);

            _eventsByDate.Clear();

            foreach (IGrouping<DateOnly, GoogleEvent> day in events.GroupBy(e => e.Date))
            {
                _eventsByDate[day.Key] = [.. day];
            }

            _loadedRange = (from, to);
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            _eventsByDate.Clear();
            _loadedRange = null;
            HasEventsError = true;
        }

        IsLoadingEvents = false;
        Changed?.Invoke();
    }

    public async Task<bool> DeleteEventAsync(GoogleEvent googleEvent)
    {
        if (googleEvent.CalendarId is null)
        {
            return false;
        }

        try
        {
            await service.DeleteEventAsync(googleEvent.CalendarId, googleEvent.Id);
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            toasts.Show("Não foi possível apagar o evento. Confirma que a conta de serviço tem a permissão “Fazer alterações a eventos” nesse calendário.", ToastKind.Error);
            return false;
        }

        if (_eventsByDate.TryGetValue(googleEvent.Date, out IReadOnlyList<GoogleEvent>? sameDay))
        {
            _eventsByDate[googleEvent.Date] = [.. sameDay.Where(e => e.Id != googleEvent.Id)];
        }

        toasts.Show("Evento apagado do Google Calendar.", ToastKind.Success);
        Changed?.Invoke();
        return true;
    }

    public async Task<bool> ConnectAsync(string serviceAccountJson, string calendarId, string? writeCalendarId)
    {
        try
        {
            await service.ConnectAsync(serviceAccountJson, calendarId, writeCalendarId);
            ResetEvents();
            await RefreshStatusAsync();
            toasts.Show("Google Calendar ligado.", ToastKind.Success);
            return true;
        }
        catch (ArgumentException)
        {
            toasts.Show("Não foi possível validar a chave ou o acesso ao calendário. Confirma o JSON e se partilhaste o calendário com o email da conta de serviço.", ToastKind.Error);
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            toasts.Show("Não foi possível ligar ao Google Calendar. Tenta novamente.", ToastKind.Error);
        }

        return false;
    }

    public async Task DisconnectAsync()
    {
        try
        {
            await service.DisconnectAsync();
            ResetEvents();
            await RefreshStatusAsync();
            toasts.Show("Google Calendar desligado.", ToastKind.Success);
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            toasts.Show("Não foi possível desligar o Google Calendar.", ToastKind.Error);
        }
    }

    public async Task RetryFailedAsync()
    {
        try
        {
            int failed = await service.RetryFailedAsync();

            if (failed == 0)
            {
                toasts.Show("Tudo sincronizado com o Google Calendar.", ToastKind.Success);
            }
            else
            {
                toasts.Show($"{failed} registo(s) continuam sem sincronizar. Tenta novamente mais tarde.", ToastKind.Error);
            }
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            toasts.Show("Não foi possível sincronizar com o Google Calendar.", ToastKind.Error);
        }
    }

    public void Dispose() => activation.Activated -= OnActivated;

    private static bool IsExpected(Exception ex) =>
        ex is ArgumentException or NotFoundException or PersistenceException or ServerUnavailableException or UnauthorizedAccessException;

    private void ResetEvents()
    {
        _eventsByDate.Clear();
        _loadedRange = null;
    }

    private void Hook()
    {
        if (_hooked)
        {
            return;
        }

        activation.Activated += OnActivated;
        _hooked = true;
    }

    private void OnActivated() => _ = RefreshAfterActivationAsync();

    private async Task RefreshAfterActivationAsync()
    {
        bool wasConnected = Status.IsConnected;
        await RefreshStatusAsync();

        if (Status.IsConnected != wasConnected)
        {
            ResetEvents();
            Changed?.Invoke();
        }
    }
}
