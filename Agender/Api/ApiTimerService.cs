using Agender.Auth;
using Agender.Shared.Contracts;
using Agender.Shared.Domain;
using Agender.Shared.Services;

namespace Agender.Api;

public sealed class ApiTimerService(IHttpClientFactory httpClientFactory, AuthSession session)
    : ApiClientBase(httpClientFactory, session), ITimerService
{
    public async Task<ActiveTimer?> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            ActiveTimerDto timer = await GetAsync<ActiveTimerDto>("/api/timer", cancellationToken);
            return timer.ToEntity();
        }
        catch (NotFoundException)
        {
            return null;
        }
    }

    public async Task<ActiveTimer> StartAsync(ActiveTimer timer, CancellationToken cancellationToken = default)
    {
        ActiveTimerDto started = await SendAsync<ActiveTimerDto>(HttpMethod.Post, "/api/timer/start", timer.ToDto(), cancellationToken);
        return started.ToEntity();
    }

    public async Task<IReadOnlyList<TimeEntry>> StopAsync(DateTime stoppedAt, CancellationToken cancellationToken = default)
    {
        TimeEntryDto[] entries = await SendAsync<TimeEntryDto[]>(HttpMethod.Post, "/api/timer/stop", new StopTimerRequest(stoppedAt), cancellationToken);
        return [.. entries.Select(e => e.ToEntity())];
    }
}
