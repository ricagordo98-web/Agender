using Agender.Shared.Contracts;

namespace Agender.Shared.Services;

public interface ITraderTradeService
{
    Task<IReadOnlyList<TraderTradeDto>> GetRangeAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
}

public interface ITraderIntegrationService
{
    Task<TraderIntegrationStatus> GetStatusAsync(CancellationToken cancellationToken = default);

    Task<TraderKeyCreated> CreateKeyAsync(CancellationToken cancellationToken = default);

    Task RevokeKeyAsync(CancellationToken cancellationToken = default);
}
