using Agender.Shared.Domain;

namespace Agender.Shared.Contracts;

public record IncomeSourceDto(Guid Id, string Name, string Emoji, EntryColor Color, bool IsArchived, int SortOrder);

public record IncomeSourceInput(string Name, string Emoji, EntryColor Color, bool IsArchived);

public record IncomeEntryDto(Guid Id, Guid SourceId, DateOnly Date, decimal Amount, string Platform, string Note);

public record IncomeEntryInput(Guid SourceId, DateOnly Date, decimal Amount, string Platform, string Note);

public static class IncomeMappings
{
    public static IncomeSourceDto ToDto(this IncomeSource source) =>
        new(source.Id, source.Name, source.Emoji, source.Color, source.IsArchived, source.SortOrder);

    public static IncomeEntryDto ToDto(this IncomeEntry entry) =>
        new(entry.Id, entry.SourceId, entry.Date, entry.Amount, entry.Platform, entry.Note);
}
