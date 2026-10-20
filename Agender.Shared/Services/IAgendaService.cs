using Agender.Shared.Domain;

namespace Agender.Shared.Services;

public interface IAgendaService
{
    Task<IReadOnlyList<Agenda>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Agenda> CreateAsync(Agenda agenda, CancellationToken cancellationToken = default);

    Task UpdateAsync(Agenda agenda, CancellationToken cancellationToken = default);

    Task SetActiveAsync(Guid id, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
