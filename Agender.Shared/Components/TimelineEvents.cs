using Agender.Shared.Domain;

namespace Agender.Shared.Components;

public enum AgendaView
{
    Day,
    Week
}

public record EntryMove(Guid Id, DateOnly Date, int StartMinute, int EndMinute);

public record SlotSelection(DateOnly Date, int StartMinute, int EndMinute);

public record TaskDrop(Guid TaskId, DateOnly Date, int StartMinute);

public record EntryMenuRequest(TimeEntry Entry, double X, double Y);

public record ColorChoice(EntryColor Color, string? Custom);

public record TaskColorChange(TaskItem Task, ColorChoice Choice);

public record DayNoteChange(DateOnly Date, string Text);

public record FilterChange(Guid? ProjectId, string? Tag);

public record NewTask(string Title, Guid? ProjectId);

public record TaskProjectChange(TaskItem Task, Guid? ProjectId);
