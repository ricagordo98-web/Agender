using Agender.Infrastructure.Google;
using Agender.Shared.Contracts;
using Agender.Shared.Domain;
using FluentValidation;

namespace Agender.Web.Api;

public class TimeEntryDtoValidator : AbstractValidator<TimeEntryDto>
{
    public TimeEntryDtoValidator()
    {
        RuleFor(e => e.StartMinute).InclusiveBetween(0, TimeSlot.MinutesPerDay - 1);
        RuleFor(e => e.EndMinute).InclusiveBetween(1, TimeSlot.MinutesPerDay);
        RuleFor(e => e.EndMinute).GreaterThan(e => e.StartMinute).WithMessage("The end must be after the start.");
        RuleFor(e => e.Description).NotNull().MaximumLength(TimeEntry.DescriptionMaxLength);
        RuleFor(e => e.Color).IsInEnum();
        RuleFor(e => e.CustomColor).Must(ColorHex.IsValid).WithMessage("The color must be in the #RRGGBB format.");
        RuleFor(e => e.Tags).NotNull().Must(tags => tags.Count <= Tag.MaxPerEntry).WithMessage($"An entry can have at most {Tag.MaxPerEntry} tags.");
        RuleForEach(e => e.Tags).MaximumLength(Tag.NameMaxLength);
    }
}

public class ProjectDtoValidator : AbstractValidator<ProjectDto>
{
    public ProjectDtoValidator()
    {
        RuleFor(p => p.Name).NotEmpty().MaximumLength(Project.NameMaxLength);
        RuleFor(p => p.ClientName).MaximumLength(Project.ClientNameMaxLength);
        RuleFor(p => p.Color).IsInEnum();
        RuleFor(p => p.CustomColor).Must(ColorHex.IsValid).WithMessage("The color must be in the #RRGGBB format.");
    }
}

public class TaskDtoValidator : AbstractValidator<TaskDto>
{
    public TaskDtoValidator()
    {
        RuleFor(t => t.Title).NotEmpty().MaximumLength(TaskItem.TitleMaxLength);
        RuleFor(t => t.Color).IsInEnum();
        RuleFor(t => t.CustomColor).Must(ColorHex.IsValid).WithMessage("The color must be in the #RRGGBB format.");
    }
}

public class ActiveTimerDtoValidator : AbstractValidator<ActiveTimerDto>
{
    public ActiveTimerDtoValidator()
    {
        RuleFor(t => t.Description).NotNull().MaximumLength(TimeEntry.DescriptionMaxLength);
    }
}

public class GoogleConnectRequestValidator : AbstractValidator<GoogleConnectRequest>
{
    public GoogleConnectRequestValidator()
    {
        RuleFor(r => r.ServiceAccountJson).NotEmpty().MaximumLength(ServiceAccountKey.MaxJsonLength);
        RuleFor(r => r.CalendarId).NotEmpty().MaximumLength(GoogleConnection.CalendarIdMaxLength);
        RuleFor(r => r.WriteCalendarId).MaximumLength(GoogleConnection.CalendarIdMaxLength);
    }
}

public class SaveNoteRequestValidator : AbstractValidator<SaveNoteRequest>
{
    public SaveNoteRequestValidator()
    {
        RuleFor(n => n.Text).NotNull().MaximumLength(DayNote.TextMaxLength);
    }
}

public class ShareRequestValidator : AbstractValidator<ShareRequest>
{
    public ShareRequestValidator()
    {
        RuleFor(r => r.Email).NotEmpty().MaximumLength(256);
    }
}

public class UserSettingsDtoValidator : AbstractValidator<UserSettingsDto>
{
    public UserSettingsDtoValidator()
    {
        RuleFor(s => s.SnapMinutes).Must(UserSettings.SnapOptions.Contains);
        RuleFor(s => s.DefaultEntryMinutes).Must(UserSettings.EntryDurationOptions.Contains);
        RuleFor(s => s.StartScrollMinute).Must(UserSettings.StartScrollOptions.Contains);
        RuleFor(s => s.TimerRoundingMinutes).Must(UserSettings.TimerRoundingOptions.Contains);
    }
}

public class AgendaDtoValidator : AbstractValidator<AgendaDto>
{
    public AgendaDtoValidator()
    {
        RuleFor(a => a.Name).NotEmpty().MaximumLength(Agenda.NameMaxLength);
        RuleFor(a => a.Color).IsInEnum();
        RuleFor(a => a.CustomColor).Must(ColorHex.IsValid).WithMessage("The color must be in the #RRGGBB format.");
    }
}
