using Agender.Infrastructure.Services;
using Agender.Shared.Abstractions;
using Agender.Shared.Components;
using Agender.Shared.Domain;
using Agender.Shared.State;
using Agender.Tests.Services;
using FluentAssertions;

namespace Agender.Tests.State;

public sealed class AgendaStoreTests : IDisposable
{
    private static readonly DateTime FixedNow = new(2026, 10, 14, 15, 30, 0);

    private readonly TestDb _db = new();
    private readonly ToastService _toasts = new();
    private readonly FakeClock _clock = new(FixedNow);
    private readonly AgendaStore _sut;

    public AgendaStoreTests()
    {
        _sut = new AgendaStore(
            new TimeEntryService(_db),
            new TaskService(_db),
            new DayNoteService(_db),
            new ProjectService(_db),
            new TagService(_db),
            new TimerService(_db),
            new AgendaService(_db),
            _clock,
            _toasts,
            new NullAppActivation(),
            new AgendaScope(),
            new NullChangeFeed());
    }

    public void Dispose()
    {
        _sut.Dispose();
        _toasts.Dispose();
        _db.Dispose();
    }

    [Fact]
    public async Task InitializeAsync_WeekView_LoadsMondayToSunday()
    {
        await _sut.InitializeAsync();

        _sut.IsInitialized.Should().BeTrue();
        _sut.Days.Should().HaveCount(7);
        _sut.Days[0].Should().Be(new DateOnly(2026, 10, 12));
        _sut.Days[^1].Should().Be(new DateOnly(2026, 10, 18));
    }

    [Fact]
    public async Task ShiftAsync_WeekView_MovesByAWeek()
    {
        await _sut.InitializeAsync();

        await _sut.ShiftAsync(1);

        _sut.Days[0].Should().Be(new DateOnly(2026, 10, 19));
    }

    [Fact]
    public async Task ChangeViewAsync_DayView_ShowsOnlyTheAnchorDay()
    {
        await _sut.InitializeAsync();

        await _sut.ChangeViewAsync(AgendaView.Day);

        _sut.Days.Should().ContainSingle().Which.Should().Be(new DateOnly(2026, 10, 14));
    }

    [Fact]
    public async Task SaveEntryAsync_NewEntry_AppearsInTheVisibleRange()
    {
        await _sut.InitializeAsync();
        TimeEntry entry = new() { Date = new DateOnly(2026, 10, 13), StartMinute = 540, EndMinute = 600, Description = "Planning" };

        bool saved = await _sut.SaveEntryAsync(entry, isNew: true);

        saved.Should().BeTrue();
        _sut.Entries.Should().ContainSingle().Which.Description.Should().Be("Planning");
    }

    [Fact]
    public async Task SaveEntryAsync_InvalidSlot_ShowsAnErrorToastAndReturnsFalse()
    {
        await _sut.InitializeAsync();
        TimeEntry entry = new() { Date = new DateOnly(2026, 10, 13), StartMinute = 600, EndMinute = 540 };

        bool saved = await _sut.SaveEntryAsync(entry, isNew: true);

        saved.Should().BeFalse();
        _toasts.Messages.Should().ContainSingle().Which.Kind.Should().Be(ToastKind.Error);
    }

    [Fact]
    public async Task DropTaskAsync_ExistingTask_CreatesAnEntryWithTheTaskTitle()
    {
        await _sut.InitializeAsync();
        await _sut.AddTaskAsync("Write report");
        TaskItem task = _sut.Tasks.Single();

        await _sut.DropTaskAsync(new TaskDrop(task.Id, new DateOnly(2026, 10, 13), 600));

        TimeEntry entry = _sut.Entries.Should().ContainSingle().Subject;
        entry.Description.Should().Be("Write report");
        entry.TaskItemId.Should().Be(task.Id);
        entry.Slot.Should().Be(new TimeSlot(600, 630));
    }

    [Fact]
    public async Task ApplySettings_BeforeInitialize_UsesTheDefaultViewAndDuration()
    {
        await _sut.ApplySettingsAsync(new UserSettings { DefaultWeekView = false, DefaultEntryMinutes = 60, SnapMinutes = 15 }, applyView: true);

        await _sut.InitializeAsync();
        await _sut.AddTaskAsync("Plan");
        await _sut.DropTaskAsync(new TaskDrop(_sut.Tasks.Single().Id, _sut.Today, 600));

        _sut.View.Should().Be(AgendaView.Day);
        _sut.SnapMinutes.Should().Be(15);
        _sut.Entries.Single().Slot.Should().Be(new TimeSlot(600, 660));
    }

    [Fact]
    public async Task ToggleEntryCompletedAsync_ByDefault_KeepsTheEntryAndCompletesTheTask()
    {
        await _sut.InitializeAsync();
        await _sut.AddTaskAsync("Report");
        await _sut.DropTaskAsync(new TaskDrop(_sut.Tasks.Single().Id, new DateOnly(2026, 10, 13), 600));

        await _sut.ToggleEntryCompletedAsync(_sut.Entries.Single().Id);

        _sut.Entries.Single().IsCompleted.Should().BeTrue();
        _sut.Tasks.Single().IsCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task ToggleEntryCompletedAsync_RemoveSettingOn_RemovesTheEntryAndCompletesTheTask()
    {
        await _sut.ApplySettingsAsync(new UserSettings { RemoveFromAgendaWhenCompleted = true }, applyView: true);
        await _sut.InitializeAsync();
        await _sut.AddTaskAsync("Report");
        await _sut.DropTaskAsync(new TaskDrop(_sut.Tasks.Single().Id, new DateOnly(2026, 10, 13), 600));

        await _sut.ToggleEntryCompletedAsync(_sut.Entries.Single().Id);

        _sut.Entries.Should().BeEmpty();
        _sut.Tasks.Single().IsCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task InitializeAsync_DefaultSettings_WeekRunsMondayToSunday()
    {
        await _sut.ApplySettingsAsync(new UserSettings(), applyView: true);
        await _sut.InitializeAsync();

        _sut.Days.Should().HaveCount(7);
        _sut.Days[0].DayOfWeek.Should().Be(DayOfWeek.Monday);
    }

    [Fact]
    public async Task ApplySettingsAsync_WeekStartsOnSunday_ReloadsTheWeekFromSunday()
    {
        await _sut.InitializeAsync();

        await _sut.ApplySettingsAsync(new UserSettings { WeekStartsOnSunday = true }, applyView: false);

        _sut.Days.Should().HaveCount(7);
        _sut.Days[0].DayOfWeek.Should().Be(DayOfWeek.Sunday);
    }

    [Fact]
    public async Task ApplySettingsAsync_HideWeekends_ShowsOnlyWeekdays()
    {
        await _sut.InitializeAsync();

        await _sut.ApplySettingsAsync(new UserSettings { HideWeekends = true }, applyView: false);

        _sut.Days.Should().HaveCount(5);
        _sut.Days.Should().NotContain(d => d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday);
    }

    [Fact]
    public async Task StartTimerAsync_SetsTheActiveTimerAtTheCurrentTime()
    {
        await _sut.InitializeAsync();

        await _sut.StartTimerAsync("Review", null, null);

        _sut.ActiveTimer.Should().NotBeNull();
        _sut.ActiveTimer!.StartedAt.Should().Be(FixedNow);
        _sut.ActiveTimer.Description.Should().Be("Review");
    }

    [Fact]
    public async Task StopTimerAsync_CreatesAnEntryAndClearsTheTimer()
    {
        await _sut.InitializeAsync();
        await _sut.StartTimerAsync("Review", null, null);
        _clock.Now = FixedNow.AddMinutes(45);

        bool stopped = await _sut.StopTimerAsync();

        stopped.Should().BeTrue();
        _sut.ActiveTimer.Should().BeNull();
        TimeEntry entry = _sut.Entries.Should().ContainSingle().Subject;
        entry.Description.Should().Be("Review");
        entry.Slot.Should().Be(new TimeSlot(930, 975));
    }

    [Fact]
    public async Task StartTimerAsync_TimerAlreadyRunning_StopsTheCurrentOneFirst()
    {
        await _sut.InitializeAsync();
        await _sut.StartTimerAsync("First", null, null);
        _clock.Now = FixedNow.AddMinutes(30);

        await _sut.StartTimerAsync("Second", null, null);

        _sut.ActiveTimer!.Description.Should().Be("Second");
        _sut.Entries.Should().ContainSingle().Which.Description.Should().Be("First");
    }

    [Fact]
    public async Task InitializeAsync_TimerRunningOnTheServer_LoadsIt()
    {
        await new TimerService(_db).StartAsync(new ActiveTimer { StartedAt = FixedNow.AddHours(-2), Description = "Background" });

        await _sut.InitializeAsync();

        _sut.ActiveTimer.Should().NotBeNull();
        _sut.ActiveTimer!.Description.Should().Be("Background");
    }

    [Fact]
    public async Task ToggleTaskAsync_CompletingTheTimedTask_StopsTheTimer()
    {
        await _sut.InitializeAsync();
        await _sut.AddTaskAsync("Write report");
        TaskItem task = _sut.Tasks.Single();
        await _sut.StartTimerAsync(task.Title, null, task.Id);
        _clock.Now = FixedNow.AddMinutes(20);

        await _sut.ToggleTaskAsync(task);

        _sut.ActiveTimer.Should().BeNull();
        _sut.Tasks.Single().IsCompleted.Should().BeTrue();
        _sut.Entries.Should().ContainSingle().Which.TaskItemId.Should().Be(task.Id);
    }

    [Fact]
    public async Task ToggleTaskAsync_ReopeningATask_LeavesTheTimerAlone()
    {
        await _sut.InitializeAsync();
        await _sut.AddTaskAsync("Write report");
        await _sut.ToggleTaskAsync(_sut.Tasks.Single());
        TaskItem done = _sut.Tasks.Single();
        await _sut.StartTimerAsync("Other", null, done.Id);

        await _sut.ToggleTaskAsync(done);

        _sut.ActiveTimer.Should().NotBeNull();
    }

    [Fact]
    public async Task SaveEntryAsync_GoogleSyncFailed_WarnsButKeepsTheEntry()
    {
        await _sut.InitializeAsync();
        TimeEntry entry = new()
        {
            Date = new DateOnly(2026, 10, 13),
            StartMinute = 540,
            EndMinute = 600,
            Description = "Planning",
            SyncToGoogle = true,
            GoogleSyncFailed = true
        };

        bool saved = await _sut.SaveEntryAsync(entry, isNew: true);

        saved.Should().BeTrue();
        _sut.Entries.Should().ContainSingle().Which.GoogleSyncFailed.Should().BeTrue();
        _toasts.Messages.Should().ContainSingle().Which.Kind.Should().Be(ToastKind.Error);
    }

    [Fact]
    public async Task SaveEntryAsync_NotFlaggedForGoogle_DoesNotWarn()
    {
        await _sut.InitializeAsync();

        await _sut.SaveEntryAsync(new TimeEntry { Date = new DateOnly(2026, 10, 13), StartMinute = 540, EndMinute = 600 }, isNew: true);

        _toasts.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task DropTaskAsync_ScheduledTask_LeavesTheOpenList()
    {
        await _sut.InitializeAsync();
        await _sut.AddTaskAsync("Write report");
        TaskItem task = _sut.Tasks.Single();
        task.IsScheduled.Should().BeFalse();

        await _sut.DropTaskAsync(new TaskDrop(task.Id, new DateOnly(2026, 10, 13), 600));

        _sut.Tasks.Single().IsScheduled.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteEntryAsync_EntryOfATask_BringsTheTaskBack()
    {
        await _sut.InitializeAsync();
        await _sut.AddTaskAsync("Write report");
        await _sut.DropTaskAsync(new TaskDrop(_sut.Tasks.Single().Id, new DateOnly(2026, 10, 13), 600));

        await _sut.DeleteEntryAsync(_sut.Entries.Single().Id);

        _sut.Tasks.Single().IsScheduled.Should().BeFalse();
    }

    [Fact]
    public async Task StopTimerAsync_TimerOfATask_SchedulesTheTask()
    {
        await _sut.InitializeAsync();
        await _sut.AddTaskAsync("Write report");
        TaskItem task = _sut.Tasks.Single();
        await _sut.StartTimerAsync(task.Title, null, task.Id);
        _clock.Now = FixedNow.AddMinutes(20);
        _sut.Tasks.Single().IsScheduled.Should().BeFalse();

        await _sut.StopTimerAsync();

        _sut.Tasks.Single().IsScheduled.Should().BeTrue();
    }

    [Fact]
    public async Task ReturnToTaskListAsync_EntryOfATask_RemovesTheEntryAndRestoresTheTask()
    {
        await _sut.InitializeAsync();
        await _sut.AddTaskAsync("Write report");
        await _sut.DropTaskAsync(new TaskDrop(_sut.Tasks.Single().Id, new DateOnly(2026, 10, 13), 600));
        _sut.Tasks.Single().IsScheduled.Should().BeTrue();

        bool returned = await _sut.ReturnToTaskListAsync(_sut.Entries.Single().Id);

        returned.Should().BeTrue();
        _sut.Entries.Should().BeEmpty();
        _sut.Tasks.Single().IsScheduled.Should().BeFalse();
        _toasts.Messages.Should().ContainSingle().Which.Kind.Should().Be(ToastKind.Success);
    }

    [Fact]
    public async Task ReturnToTaskListAsync_EntryWithoutATask_DoesNothing()
    {
        await _sut.InitializeAsync();
        TimeEntry entry = new() { Date = new DateOnly(2026, 10, 13), StartMinute = 540, EndMinute = 600, Description = "Planning" };
        await _sut.SaveEntryAsync(entry, isNew: true);

        bool returned = await _sut.ReturnToTaskListAsync(entry.Id);

        returned.Should().BeFalse();
        _sut.Entries.Should().ContainSingle();
    }

    [Fact]
    public async Task ReturnToTaskListAsync_TaskWithOtherEntries_StaysScheduledAndDoesNotAnnounceIt()
    {
        await _sut.InitializeAsync();
        await _sut.AddTaskAsync("Write report");
        TaskItem task = _sut.Tasks.Single();
        await _sut.StartTimerAsync(task.Title, null, task.Id);
        _clock.Now = FixedNow.AddMinutes(10);
        await _sut.StopTimerAsync();
        await _sut.DropTaskAsync(new TaskDrop(task.Id, new DateOnly(2026, 10, 13), 600));
        _toasts.Messages.Should().BeEmpty();

        await _sut.ReturnToTaskListAsync(_sut.Entries.First(e => e.Date == new DateOnly(2026, 10, 13)).Id);

        _sut.Tasks.Single().IsScheduled.Should().BeTrue();
        _toasts.Messages.Should().BeEmpty();
    }

    private sealed class FakeClock(DateTime now) : IClock
    {
        public DateTime Now { get; set; } = now;

        public Task EnsureInitializedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
