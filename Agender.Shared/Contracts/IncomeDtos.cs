using Agender.Shared.Domain;

namespace Agender.Shared.Contracts;

public record IncomeSourceDto(Guid Id, string Name, string Emoji, EntryColor Color, bool IsArchived, int SortOrder, IncomeCurrency Currency = IncomeCurrency.Eur, bool ExcludeFromReport = false);

public record IncomeSourceInput(string Name, string Emoji, EntryColor Color, bool IsArchived, IncomeCurrency Currency = IncomeCurrency.Eur, bool ExcludeFromReport = false);

public record IncomeEntryDto(Guid Id, Guid SourceId, DateOnly Date, decimal Amount, string Platform, string Note, IncomeEntryStatus Status = IncomeEntryStatus.Earned, decimal? ConvertedAmount = null);

public record IncomeEntryInput(Guid SourceId, DateOnly Date, decimal Amount, string Platform, string Note, IncomeEntryStatus Status = IncomeEntryStatus.Earned, decimal? ConvertedAmount = null);

public static class IncomeMappings
{
    public static IncomeSourceDto ToDto(this IncomeSource source) =>
        new(source.Id, source.Name, source.Emoji, source.Color, source.IsArchived, source.SortOrder, source.Currency, source.ExcludeFromReport);

    public static IncomeEntryDto ToDto(this IncomeEntry entry) =>
        new(entry.Id, entry.SourceId, entry.Date, entry.Amount, entry.Platform, entry.Note, entry.Status, entry.ConvertedAmount);
}
