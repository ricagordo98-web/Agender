using Agender.Shared.Contracts;

namespace Agender.Shared.Services;

public interface IPublicAgendaService
{
    Task<SharedAgendaDto> GetAsync(string token, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
}
