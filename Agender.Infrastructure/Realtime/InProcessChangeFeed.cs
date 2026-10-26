using Agender.Infrastructure.Identity;
using Agender.Shared.Abstractions;
using Agender.Shared.Contracts;

namespace Agender.Infrastructure.Realtime;

public sealed class InProcessChangeFeed(ChangeBus bus, ICurrentUser currentUser) : IChangeFeed, IDisposable
{
    private string? _me;
    private bool _started;

    public event Func<ChangeNotice, Task>? Changed;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_started)
        {
            return;
        }

        _me = await currentUser.GetUserIdAsync(cancellationToken);
        bus.Received += OnReceivedAsync;
        _started = true;
    }

    public void Dispose()
    {
        if (_started)
        {
            bus.Received -= OnReceivedAsync;
        }
    }

    private Task OnReceivedAsync(ChangeNotice notice, IReadOnlyCollection<string> recipients) =>
        _me is not null && recipients.Contains(_me) ? Changed?.Invoke(notice) ?? Task.CompletedTask : Task.CompletedTask;
}
