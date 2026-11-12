using Agender.Shared.Domain;

namespace Agender.Shared.Contracts;

public class DailyReminderDto
{
    public DailySection Section { get; set; }

    public bool IsEnabled { get; set; }

    public string? Destination { get; set; }

    public TimeOnly SendTime { get; set; } = DailyCatalog.DefaultTime;

    public DateOnly? LastSentOn { get; set; }

    public string? LastError { get; set; }
}

public class DailyRemindersDto
{
    public bool WhatsAppReady { get; set; }

    public List<DailyReminderDto> Reminders { get; set; } = [];
}

public record WhatsAppGroupDto(string Id, string Name);

public record DailyDigestSection(DailySection Section, string Title, List<string> Lines);

public record DailyDigestDto(DateOnly Date, List<DailyDigestSection> Sections);
