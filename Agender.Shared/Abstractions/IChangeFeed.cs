using Agender.Shared.Contracts;

namespace Agender.Shared.Abstractions;

public interface IChangeFeed
{
    event Func<ChangeNotice, Task>? Changed;

    Task StartAsync(CancellationToken cancellationToken = default);
}
