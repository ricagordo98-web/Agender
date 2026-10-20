using Agender.Shared.Domain;

namespace Agender.Shared.Contracts;

public static class DtoMappings
{
    public static TimeEntryDto ToDto(this TimeEntry entry) => new(
        entry.Id,
        entry.Date,
        entry.StartMinute,
        entry.EndMinute,
        entry.Description,
        entry.Color,
        entry.TaskItemId,
        entry.ProjectId,
        [.. entry.Tags.Select(t => t.Name).Order(StringComparer.CurrentCultureIgnoreCase)],
        entry.SyncToGoogle,
        entry.GoogleSyncFailed,
        entry.IsCompleted,
        entry.CustomColor,
        entry.AgendaId);

    public static TimeEntry ToEntity(this TimeEntryDto dto) => new()
    {
        Id = dto.Id,
        Date = dto.Date,
        StartMinute = dto.StartMinute,
        EndMinute = dto.EndMinute,
        Description = dto.Description,
        Color = dto.Color,
        TaskItemId = dto.TaskItemId,
        ProjectId = dto.ProjectId,
        SyncToGoogle = dto.SyncToGoogle,
        GoogleSyncFailed = dto.GoogleSyncFailed,
        IsCompleted = dto.IsCompleted,
        CustomColor = dto.CustomColor,
        AgendaId = dto.AgendaId,
        Tags = [.. (dto.Tags ?? []).Select(name => new Tag { Name = name })]
    };

    public static TaskDto ToDto(this TaskItem task) => new(task.Id, task.Title, task.Color, task.IsCompleted, task.CreatedAt, task.ProjectId, task.IsScheduled, task.CustomColor, task.AgendaId);

    public static TaskItem ToEntity(this TaskDto dto) => new()
    {
        Id = dto.Id,
        Title = dto.Title,
        Color = dto.Color,
        CustomColor = dto.CustomColor,
        AgendaId = dto.AgendaId,
        IsCompleted = dto.IsCompleted,
        CreatedAt = dto.CreatedAt,
        ProjectId = dto.ProjectId,
        IsScheduled = dto.IsScheduled
    };

    public static ProjectDto ToDto(this Project project) =>
        new(project.Id, project.Name, project.ClientName, project.Color, project.IsArchived, project.CreatedAt, project.CustomColor, project.AgendaId);

    public static Project ToEntity(this ProjectDto dto) => new()
    {
        Id = dto.Id,
        Name = dto.Name,
        ClientName = dto.ClientName,
        Color = dto.Color,
        CustomColor = dto.CustomColor,
        AgendaId = dto.AgendaId,
        IsArchived = dto.IsArchived,
        CreatedAt = dto.CreatedAt
    };

    public static ActiveTimerDto ToDto(this ActiveTimer timer) =>
        new(timer.Id, timer.StartedAt, timer.Description, timer.ProjectId, timer.TaskItemId, timer.AgendaId);

    public static ActiveTimer ToEntity(this ActiveTimerDto dto) => new()
    {
        Id = dto.Id,
        StartedAt = dto.StartedAt,
        Description = dto.Description,
        ProjectId = dto.ProjectId,
        TaskItemId = dto.TaskItemId,
        AgendaId = dto.AgendaId
    };

    public static DayNoteDto ToDto(this DayNote note) => new(note.Date, note.Text);

    public static DayNote ToEntity(this DayNoteDto dto) => new() { Date = dto.Date, Text = dto.Text };

    public static UserSettingsDto ToDto(this UserSettings settings) => new(
        settings.DefaultWeekView,
        settings.SnapMinutes,
        settings.DefaultEntryMinutes,
        settings.ConfirmBeforeDelete,
        settings.RemoveFromAgendaWhenCompleted,
        settings.WeekStartsOnSunday,
        settings.HideWeekends,
        settings.HideDayNotes,
        settings.HideEntryDetails,
        settings.StartScrollMinute,
        settings.TimerRoundingMinutes);

    public static UserSettings ToEntity(this UserSettingsDto dto) => new()
    {
        DefaultWeekView = dto.DefaultWeekView,
        SnapMinutes = dto.SnapMinutes,
        DefaultEntryMinutes = dto.DefaultEntryMinutes,
        ConfirmBeforeDelete = dto.ConfirmBeforeDelete,
        RemoveFromAgendaWhenCompleted = dto.RemoveFromAgendaWhenCompleted,
        WeekStartsOnSunday = dto.WeekStartsOnSunday,
        HideWeekends = dto.HideWeekends,
        HideDayNotes = dto.HideDayNotes,
        HideEntryDetails = dto.HideEntryDetails,
        StartScrollMinute = dto.StartScrollMinute,
        TimerRoundingMinutes = dto.TimerRoundingMinutes
    };

    public static AgendaDto ToDto(this Agenda agenda) => new(
        agenda.Id,
        agenda.Name,
        agenda.Color,
        agenda.IsVisible,
        agenda.IsActive,
        agenda.IsDefault,
        agenda.CreatedAt,
        agenda.CustomColor);

    public static Agenda ToEntity(this AgendaDto dto) => new()
    {
        Id = dto.Id,
        Name = dto.Name,
        Color = dto.Color,
        CustomColor = dto.CustomColor,
        IsVisible = dto.IsVisible,
        IsActive = dto.IsActive,
        IsDefault = dto.IsDefault,
        CreatedAt = dto.CreatedAt
    };
}
