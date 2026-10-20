namespace Agender.Shared.State;

public sealed class ToastService : IToastService, IDisposable
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ActionLifetime = TimeSpan.FromSeconds(8);
    private const int MaxVisible = 4;

    private readonly object _gate = new();
    private readonly List<ToastMessage> _messages = [];
    private readonly Dictionary<Guid, Timer> _timers = [];

    public event Action? Changed;

    public IReadOnlyList<ToastMessage> Messages
    {
        get
        {
            lock (_gate)
            {
                return [.. _messages];
            }
        }
    }

    public void Show(string text, ToastKind kind = ToastKind.Info, ToastAction? action = null)
    {
        ToastMessage message = new(Guid.NewGuid(), text, kind, action);

        lock (_gate)
        {
            _messages.Add(message);
            while (_messages.Count > MaxVisible)
            {
                RemoveLocked(_messages[0].Id);
            }

            _timers[message.Id] = new Timer(_ => Dismiss(message.Id), null, action is null ? Lifetime : ActionLifetime, Timeout.InfiniteTimeSpan);
        }

        Changed?.Invoke();
    }

    public void Dismiss(Guid id)
    {
        bool removed;

        lock (_gate)
        {
            removed = RemoveLocked(id);
        }

        if (removed)
        {
            Changed?.Invoke();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            foreach (Timer timer in _timers.Values)
            {
                timer.Dispose();
            }

            _timers.Clear();
            _messages.Clear();
        }
    }

    private bool RemoveLocked(Guid id)
    {
        if (_timers.Remove(id, out Timer? timer))
        {
            timer.Dispose();
        }

        return _messages.RemoveAll(m => m.Id == id) > 0;
    }
}
