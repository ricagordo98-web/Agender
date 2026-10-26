using Agender.Shared.Contracts;

namespace Agender.Shared.Abstractions;

public sealed class NullChangeFeed : IChangeFeed
{
    public event Func<ChangeNotice, Task>? Changed
    {
        add { }
        remove { }
    }

    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
