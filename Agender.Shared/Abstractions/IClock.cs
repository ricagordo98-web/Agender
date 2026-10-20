namespace Agender.Shared.Abstractions;

public interface IClock
{
    DateTime Now { get; }

    DateOnly Today => DateOnly.FromDateTime(Now);

    Task EnsureInitializedAsync(CancellationToken cancellationToken = default);
}
