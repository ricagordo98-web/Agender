using Agender.Shared.Domain;

namespace Agender.Shared.Services;

public interface ITimerService
{
    Task<ActiveTimer?> GetActiveAsync(CancellationToken cancellationToken = default);

    Task<ActiveTimer> StartAsync(ActiveTimer timer, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TimeEntry>> StopAsync(DateTime stoppedAt, CancellationToken cancellationToken = default);
}
