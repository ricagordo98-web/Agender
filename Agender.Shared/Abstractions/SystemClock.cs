namespace Agender.Shared.Abstractions;

public class SystemClock : IClock
{
    public DateTime Now => DateTime.Now;

    public Task EnsureInitializedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
