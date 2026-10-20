using Agender.Shared.Contracts;

namespace Agender.Shared.Services;

public interface IAgendaSharingService
{
    Task<IReadOnlyList<ShareDto>> GetSharesAsync(CancellationToken cancellationToken = default);

    Task<ShareDto> ShareAsync(string email, CancellationToken cancellationToken = default);

    Task RevokeAsync(Guid shareId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SharedWithMeDto>> GetSharedWithMeAsync(CancellationToken cancellationToken = default);

    Task<SharedAgendaDto> GetSharedAgendaAsync(Guid shareId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);

    Task<ShareLinkStatusDto> GetLinkAsync(CancellationToken cancellationToken = default);

    Task<NewShareLinkDto> CreateLinkAsync(CancellationToken cancellationToken = default);

    Task RevokeLinkAsync(CancellationToken cancellationToken = default);
}
