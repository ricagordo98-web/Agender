using Agender.Shared.Domain;

namespace Agender.Shared.Contracts;

public record TimeEntryDto(
    Guid Id,
    DateOnly Date,
    int StartMinute,
    int EndMinute,
    string Description,
    EntryColor Color,
    Guid? TaskItemId,
    Guid? ProjectId,
    IReadOnlyList<string> Tags,
    bool SyncToGoogle = false,
    bool GoogleSyncFailed = false,
    bool IsCompleted = false,
    string? CustomColor = null,
    Guid AgendaId = default);

public record TaskDto(Guid Id, string Title, EntryColor Color, bool IsCompleted, DateTime CreatedAt, Guid? ProjectId, bool IsScheduled = false, string? CustomColor = null, Guid AgendaId = default);

public record ProjectDto(Guid Id, string Name, string? ClientName, EntryColor Color, bool IsArchived, DateTime CreatedAt, string? CustomColor = null, Guid AgendaId = default);

public record ProjectMinutesDto(Guid ProjectId, int Minutes);

public record DayNoteDto(DateOnly Date, string Text);

public record SaveNoteRequest(string Text);

public record ActiveTimerDto(Guid Id, DateTime StartedAt, string Description, Guid? ProjectId, Guid? TaskItemId, Guid AgendaId = default);

public record StopTimerRequest(DateTime StoppedAt);

public record GoogleStatusDto(bool IsConnected, string? ServiceAccountEmail, string? CalendarId, string? WriteCalendarId);

public record GoogleConnectRequest(string ServiceAccountJson, string CalendarId, string? WriteCalendarId);

public record GoogleEventDto(string Id, string Title, DateOnly Date, int? StartMinute, int? EndMinute, bool IsAgender = false, Guid? EntryId = null, string? CalendarId = null);

public record GoogleRetryDto(int Failed);

public record UserInfoDto(string Email, string DisplayName);

public record ShareDto(Guid Id, string Email, string DisplayName);

public record ShareRequest(string Email);

public record SharedWithMeDto(Guid ShareId, string Email, string DisplayName);

public record SharedAgendaDto(
    string OwnerName,
    IReadOnlyList<TimeEntryDto> Entries,
    IReadOnlyList<ProjectDto> Projects,
    IReadOnlyList<DayNoteDto> Notes);

public record ShareLinkStatusDto(bool IsActive, DateTime? ExpiresAt);

public record NewShareLinkDto(string Token, DateTime ExpiresAt, string? BaseUrl = null, string? Notice = null);

public record UserSettingsDto(
    bool DefaultWeekView,
    int SnapMinutes,
    int DefaultEntryMinutes,
    bool ConfirmBeforeDelete,
    bool RemoveFromAgendaWhenCompleted,
    bool WeekStartsOnSunday = false,
    bool HideWeekends = false,
    bool HideDayNotes = false,
    bool HideEntryDetails = false,
    int StartScrollMinute = 0,
    int TimerRoundingMinutes = 0);

public record AgendaDto(Guid Id, string Name, EntryColor Color, bool IsVisible, bool IsActive, bool IsDefault, DateTime CreatedAt, string? CustomColor = null);
