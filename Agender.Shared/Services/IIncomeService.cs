using Agender.Shared.Contracts;

namespace Agender.Shared.Services;

public interface IIncomeService
{
    Task<IReadOnlyList<IncomeSourceDto>> GetSourcesAsync(CancellationToken cancellationToken = default);

    Task<IncomeSourceDto> CreateSourceAsync(IncomeSourceInput input, CancellationToken cancellationToken = default);

    Task<IncomeSourceDto> UpdateSourceAsync(Guid id, IncomeSourceInput input, CancellationToken cancellationToken = default);

    Task DeleteSourceAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IncomeEntryDto>> GetEntriesAsync(DateOnly from, DateOnly to, Guid? sourceId = null, CancellationToken cancellationToken = default);

    Task<IncomeEntryDto> CreateEntryAsync(IncomeEntryInput input, CancellationToken cancellationToken = default);

    Task<IncomeEntryDto> UpdateEntryAsync(Guid id, IncomeEntryInput input, CancellationToken cancellationToken = default);

    Task DeleteEntryAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetPlatformsAsync(Guid sourceId, CancellationToken cancellationToken = default);
}
