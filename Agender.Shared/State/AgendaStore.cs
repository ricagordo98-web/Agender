using Agender.Shared.Abstractions;
using Agender.Shared.Components;
using Agender.Shared.Domain;
using Agender.Shared.Services;

namespace Agender.Shared.State;

public sealed class AgendaStore(
    ITimeEntryService entryService,
    ITaskService taskService,
    IDayNoteService noteService,
    IProjectService projectService,
    ITagService tagService,
    ITimerService timerService,
    IAgendaService agendaService,
    IClock clock,
    IToastService toasts,
    IAppActivation activation) : IDisposable
{
    private static readonly TimeSpan HighlightDuration = TimeSpan.FromSeconds(4);

    private readonly CancellationTokenSource _lifetime = new();
    private List<TimeEntry> _entries = [];
    private Dictionary<DateOnly, string> _notes = [];
    private bool _initialized;
    private bool _activationHooked;

    public event Action? Changed;

    public event Action? NewEntryRequested;

    public DateOnly Anchor { get; private set; }

    public AgendaView View { get; private set; } = AgendaView.Week;

    public int SnapMinutes { get; private set; } = 5;

    public int DefaultEntryMinutes { get; private set; } = TimelineMetrics.DefaultEntryMinutes;

    public bool HasAppliedSettings { get; private set; }

    public UserSettings Preferences { get; private set; } = new();

    public bool ConfirmBeforeDelete { get; private set; } = true;

    public bool RemoveFromAgendaWhenCompleted { get; private set; }

    public bool IsInitialized => _initialized;

    public IReadOnlyList<DateOnly> Days { get; private set; } = [];

    public IReadOnlyList<TimeEntry> Entries => _entries;

    public IReadOnlyDictionary<DateOnly, string> Notes => _notes;

    public IReadOnlyList<TaskItem> Tasks { get; private set; } = [];

    public IReadOnlyList<Project> Projects { get; private set; } = [];

    public IReadOnlyList<Agenda> Agendas { get; private set; } = [];

    public Agenda? ActiveAgenda => Agendas.FirstOrDefault(a => a.IsActive);

    public Guid ActiveAgendaId => ActiveAgenda?.Id ?? Guid.Empty;

    public IReadOnlyList<TimeEntry> VisibleEntries => [.. _entries.Where(e => IsAgendaVisible(e.AgendaId))];

    public IReadOnlyList<TaskItem> ActiveTasks => [.. Tasks.Where(t => t.AgendaId == ActiveAgendaId || ActiveAgendaId == Guid.Empty)];

    public IReadOnlyList<Project> ActiveProjects => [.. Projects.Where(p => p.AgendaId == ActiveAgendaId || ActiveAgendaId == Guid.Empty)];

    public IReadOnlyList<string> Tags { get; private set; } = [];

    public IReadOnlyDictionary<Guid, Project> ProjectsById { get; private set; } = new Dictionary<Guid, Project>();

    public ActiveTimer? ActiveTimer { get; private set; }

    public bool SyncNewEntries { get; set; }

    public Guid? FilterProjectId { get; private set; }

    public string? FilterTag { get; private set; }

    public Guid? HighlightedEntryId { get; private set; }

    public bool HasPendingNewEntry { get; private set; }

    public DateOnly Today => clock.Today;

    public DateTime Now => clock.Now;

    public Task InitializeAsync() => RunSafeAsync(async () =>
    {
        if (_initialized)
        {
            return;
        }

        await clock.EnsureInitializedAsync(_lifetime.Token);
        HookActivation();
        Anchor = clock.Today;
        Days = BuildDays();
        Tasks = await taskService.GetAllAsync(_lifetime.Token);
        await LoadReferenceDataAsync();
        ActiveTimer = await timerService.GetActiveAsync(_lifetime.Token);
        await LoadRangeAsync();
        _initialized = true;
    });

    public Task RefreshAsync() => RunSafeAsync(async () =>
    {
        if (!_initialized)
        {
            return;
        }

        Tasks = await taskService.GetAllAsync(_lifetime.Token);
        await LoadReferenceDataAsync();
        ActiveTimer = await timerService.GetActiveAsync(_lifetime.Token);
        await LoadRangeAsync();
    });

    public Task ChangeViewAsync(AgendaView view) => RunSafeAsync(async () =>
    {
        View = view;
        await LoadRangeAsync();
    });

    public Task ShiftAsync(int direction) => RunSafeAsync(async () =>
    {
        Anchor = Anchor.AddDays(direction * (View == AgendaView.Day ? 1 : 7));
        await LoadRangeAsync();
    });

    public Task GoToTodayAsync() => RunSafeAsync(async () =>
    {
        Anchor = clock.Today;
        await LoadRangeAsync();
    });

    public Task GoToDateAsync(DateOnly date) => RunSafeAsync(async () =>
    {
        Anchor = date;
        await LoadRangeAsync();
    });

    public Task GoToEntryAsync(TimeEntry entry) => RunSafeAsync(async () =>
    {
        Anchor = entry.Date;
        await LoadRangeAsync();
        HighlightedEntryId = entry.Id;
        _ = ClearHighlightAsync(entry.Id);
    });

    public void SetFilter(Guid? projectId, string? tag)
    {
        FilterProjectId = projectId;
        FilterTag = string.IsNullOrWhiteSpace(tag) ? null : tag;
        NotifyChanged();
    }

    public void RequestNewEntry()
    {
        HasPendingNewEntry = true;
        NewEntryRequested?.Invoke();
    }

    public bool ConsumePendingNewEntry()
    {
        bool pending = HasPendingNewEntry;
        HasPendingNewEntry = false;
        return pending;
    }

    public async Task<IReadOnlyList<TimeEntry>> SearchEntriesAsync(string query, int skip, int take, CancellationToken cancellationToken)
    {
        IReadOnlyList<TimeEntry> results = [];

        await RunSafeAsync(async () => results = await entryService.SearchAsync(query, skip, take, cancellationToken), notify: false);

        return results;
    }

    public async Task ApplySettingsAsync(UserSettings settings, bool applyView)
    {
        bool layoutChanged = Preferences.WeekStartsOnSunday != settings.WeekStartsOnSunday || Preferences.HideWeekends != settings.HideWeekends;
        Preferences = settings;
        SnapMinutes = settings.SnapMinutes;
        DefaultEntryMinutes = settings.DefaultEntryMinutes;
        ConfirmBeforeDelete = settings.ConfirmBeforeDelete;
        RemoveFromAgendaWhenCompleted = settings.RemoveFromAgendaWhenCompleted;
        HasAppliedSettings = true;

        AgendaView view = settings.DefaultWeekView ? AgendaView.Week : AgendaView.Day;

        if (applyView && view != View)
        {
            if (_initialized)
            {
                await ChangeViewAsync(view);
                return;
            }

            View = view;
        }

        if (_initialized && layoutChanged)
        {
            await RunSafeAsync(async () => await LoadRangeAsync());
            return;
        }

        NotifyChanged();
    }

    public void SetSnap(int minutes)
    {
        SnapMinutes = minutes;
        NotifyChanged();
    }

    public Task<bool> SaveEntryAsync(TimeEntry entry, bool isNew) => RunSafeAsync(async () =>
    {
        if (isNew)
        {
            await entryService.CreateAsync(entry, _lifetime.Token);
        }
        else
        {
            await entryService.UpdateAsync(entry, _lifetime.Token);
        }

        Upsert(entry);
        await ReloadTagsAsync();
        await ConfirmGoogleSyncAsync(entry);
    });

    public Task MoveEntryAsync(EntryMove move) => RunSafeAsync(async () =>
    {
        TimeEntry? current = _entries.Find(e => e.Id == move.Id);
        if (current is null)
        {
            return;
        }

        TimeEntry moved = current.Clone();
        moved.Date = move.Date;
        moved.StartMinute = move.StartMinute;
        moved.EndMinute = move.EndMinute;

        await entryService.UpdateAsync(moved, _lifetime.Token);
        Upsert(moved);
    });

    public async Task ToggleEntryCompletedAsync(Guid id)
    {
        TimeEntry? current = _entries.Find(e => e.Id == id);

        if (current is null)
        {
            return;
        }

        if (RemoveFromAgendaWhenCompleted && !current.IsCompleted)
        {
            await CompleteAndRemoveAsync(current);
            return;
        }

        TimeEntry updated = current.Clone();
        updated.IsCompleted = !current.IsCompleted;

        if (!await SaveEntryAsync(updated, false))
        {
            return;
        }

        if (updated.TaskItemId is Guid taskId && Tasks.FirstOrDefault(t => t.Id == taskId) is TaskItem task && task.IsCompleted != updated.IsCompleted)
        {
            await ToggleTaskAsync(task);
        }
    }

    private async Task CompleteAndRemoveAsync(TimeEntry entry)
    {
        if (entry.TaskItemId is Guid taskId && Tasks.FirstOrDefault(t => t.Id == taskId) is TaskItem { IsCompleted: false } task)
        {
            await ToggleTaskAsync(task);
        }

        await DeleteEntryAsync(entry.Id);
    }

    public Task<bool> DeleteEntryAsync(Guid id) => RunSafeAsync(async () =>
    {
        bool hadTask = _entries.Find(e => e.Id == id)?.TaskItemId is not null;

        await entryService.DeleteAsync(id, _lifetime.Token);
        _entries.RemoveAll(e => e.Id == id);

        if (hadTask)
        {
            Tasks = await taskService.GetAllAsync(_lifetime.Token);
        }
    });

    public async Task<bool> ReturnToTaskListAsync(Guid entryId)
    {
        Guid? taskId = _entries.Find(e => e.Id == entryId)?.TaskItemId;

        if (taskId is null)
        {
            return false;
        }

        if (!await DeleteEntryAsync(entryId))
        {
            return false;
        }

        if (Tasks.FirstOrDefault(t => t.Id == taskId) is { IsScheduled: false })
        {
            toasts.Show("Tarefa devolvida à lista.", ToastKind.Success);
        }

        return true;
    }

    public Task DropTaskAsync(TaskDrop drop) => RunSafeAsync(async () =>
    {
        TaskItem? task = Tasks.FirstOrDefault(t => t.Id == drop.TaskId);
        if (task is null)
        {
            return;
        }

        int start = Math.Min(drop.StartMinute, TimeSlot.MinutesPerDay - DefaultEntryMinutes);
        TimeEntry entry = await entryService.CreateAsync(new TimeEntry
        {
            Date = drop.Date,
            StartMinute = start,
            EndMinute = start + DefaultEntryMinutes,
            Description = task.Title,
            Color = task.Color,
            CustomColor = task.CustomColor,
            AgendaId = task.AgendaId,
            TaskItemId = task.Id,
            ProjectId = task.ProjectId,
            SyncToGoogle = SyncNewEntries
        }, _lifetime.Token);

        Upsert(entry);
        Tasks = await taskService.GetAllAsync(_lifetime.Token);
        await ConfirmGoogleSyncAsync(entry);
    });

    public Task SaveNoteAsync(DayNoteChange change) => RunSafeAsync(async () =>
    {
        await noteService.SaveAsync(change.Date, change.Text, _lifetime.Token);

        if (string.IsNullOrWhiteSpace(change.Text))
        {
            _notes.Remove(change.Date);
        }
        else
        {
            _notes[change.Date] = change.Text.Trim();
        }
    });

    public Task SetActiveAgendaAsync(Guid id) => RunSafeAsync(async () =>
    {
        await agendaService.SetActiveAsync(id, _lifetime.Token);
        await ReloadAgendasAsync();

        if (FilterProjectId is Guid filter && ProjectsById.TryGetValue(filter, out Project? project) && project.AgendaId != ActiveAgendaId)
        {
            FilterProjectId = null;
        }
    });

    public Task SelectOnlyAgendaAsync(Guid id) => RunSafeAsync(async () =>
    {
        await agendaService.SetActiveAsync(id, _lifetime.Token);
        foreach (Agenda other in Agendas.Where(a => a.Id != id && a.IsVisible))
        {
            await agendaService.UpdateAsync(new Agenda
            {
                Id = other.Id,
                Name = other.Name,
                Color = other.Color,
                CustomColor = other.CustomColor,
                IsVisible = false
            }, _lifetime.Token);
        }

        await ReloadAgendasAsync();

        if (FilterProjectId is Guid filter && ProjectsById.TryGetValue(filter, out Project? project) && project.AgendaId != ActiveAgendaId)
        {
            FilterProjectId = null;
        }
    });

    public Task ToggleAgendaVisibilityAsync(Agenda agenda) => RunSafeAsync(async () =>
    {
        await agendaService.UpdateAsync(new Agenda
        {
            Id = agenda.Id,
            Name = agenda.Name,
            Color = agenda.Color,
            CustomColor = agenda.CustomColor,
            IsVisible = !agenda.IsVisible
        }, _lifetime.Token);
        await ReloadAgendasAsync();
    });

    public Task<bool> SaveAgendaAsync(Agenda agenda, bool isNew) => RunSafeAsync(async () =>
    {
        if (isNew)
        {
            await agendaService.CreateAsync(agenda, _lifetime.Token);
        }
        else
        {
            await agendaService.UpdateAsync(agenda, _lifetime.Token);
        }

        await ReloadAgendasAsync();
    });

    public Task<bool> DeleteAgendaAsync(Guid id) => RunSafeAsync(async () =>
    {
        await agendaService.DeleteAsync(id, _lifetime.Token);
        await ReloadAgendasAsync();
        Tasks = await taskService.GetAllAsync(_lifetime.Token);
        await ReloadProjectsAsync();
        await LoadRangeAsync();
    });

    public Task AddTaskAsync(string title, Guid? projectId = null) => RunSafeAsync(async () =>
    {
        Project? project = Projects.FirstOrDefault(p => p.Id == projectId);
        EntryColor color = project?.Color ?? EntryColorStyle.Palette[Tasks.Count % EntryColorStyle.Palette.Count];
        await taskService.CreateAsync(new TaskItem { Title = title, Color = color, CustomColor = project?.CustomColor, ProjectId = projectId, AgendaId = ActiveAgendaId }, _lifetime.Token);
        Tasks = await taskService.GetAllAsync(_lifetime.Token);
    });

    public Task StartTimerAsync(string description, Guid? projectId, Guid? taskItemId) => RunSafeAsync(async () =>
    {
        if (ActiveTimer is not null)
        {
            await StopActiveTimerAsync();
        }

        DateTime now = clock.Now;

        ActiveTimer = await timerService.StartAsync(new ActiveTimer
        {
            StartedAt = new DateTime(now.Ticks - now.Ticks % TimeSpan.TicksPerSecond),
            Description = description,
            ProjectId = projectId,
            TaskItemId = taskItemId,
            AgendaId = ActiveAgendaId
        }, _lifetime.Token);
    });

    public Task<bool> StopTimerAsync() => RunSafeAsync(StopActiveTimerAsync);

    public async Task ToggleTaskAsync(TaskItem task)
    {
        if (!task.IsCompleted && ActiveTimer?.TaskItemId == task.Id)
        {
            await StopTimerAsync();
        }

        await UpdateTaskAsync(task, copy => copy.IsCompleted = !copy.IsCompleted);
    }

    public Task SetTaskProjectAsync(TaskItem task, Guid? projectId) => UpdateTaskAsync(task, copy =>
    {
        copy.ProjectId = projectId;

        if (Projects.FirstOrDefault(p => p.Id == projectId) is Project project)
        {
            copy.Color = project.Color;
            copy.CustomColor = project.CustomColor;
        }
    });

    public Task<bool> SaveProjectAsync(Project project, bool isNew) => RunSafeAsync(async () =>
    {
        if (isNew)
        {
            await projectService.CreateAsync(project, _lifetime.Token);
        }
        else
        {
            await projectService.UpdateAsync(project, _lifetime.Token);
        }

        await ReloadProjectsAsync();
    });

    public Task<bool> DeleteProjectAsync(Guid id) => RunSafeAsync(async () =>
    {
        await projectService.DeleteAsync(id, _lifetime.Token);

        if (FilterProjectId == id)
        {
            FilterProjectId = null;
        }

        await ReloadProjectsAsync();
        Tasks = await taskService.GetAllAsync(_lifetime.Token);

        if (_initialized)
        {
            await LoadRangeAsync();
        }
    });

    public Task SetTaskColorAsync(TaskItem task, ColorChoice choice) => UpdateTaskAsync(task, copy =>
    {
        copy.Color = choice.Color;
        copy.CustomColor = choice.Custom;
    });

    public Task CycleTaskColorAsync(TaskItem task) => UpdateTaskAsync(task, copy =>
    {
        copy.Color = EntryColorStyle.Next(copy.Color);
        copy.CustomColor = null;
    });

    public Task DeleteTaskAsync(TaskItem task) => RunSafeAsync(async () =>
    {
        await taskService.DeleteAsync(task.Id, _lifetime.Token);
        Tasks = await taskService.GetAllAsync(_lifetime.Token);
        await LoadRangeAsync();
    });

    public void Dispose()
    {
        activation.Activated -= OnActivated;
        _lifetime.Cancel();
        _lifetime.Dispose();
    }

    private Task UpdateTaskAsync(TaskItem task, Action<TaskItem> change) => RunSafeAsync(async () =>
    {
        TaskItem copy = new()
        {
            Id = task.Id,
            Title = task.Title,
            Color = task.Color,
            CustomColor = task.CustomColor,
            AgendaId = task.AgendaId,
            IsCompleted = task.IsCompleted,
            CreatedAt = task.CreatedAt,
            ProjectId = task.ProjectId
        };

        change(copy);
        await taskService.UpdateAsync(copy, _lifetime.Token);
        Tasks = await taskService.GetAllAsync(_lifetime.Token);
    });

    private async Task ConfirmGoogleSyncAsync(TimeEntry entry)
    {
        if (!entry.SyncToGoogle)
        {
            return;
        }

        IReadOnlyList<TimeEntry> stored = await entryService.GetRangeAsync(entry.Date, entry.Date, _lifetime.Token);
        TimeEntry? synced = stored.FirstOrDefault(e => e.Id == entry.Id);

        if (synced is null)
        {
            return;
        }

        Upsert(synced);

        if (synced.GoogleSyncFailed)
        {
            toasts.Show("Registo guardado, mas não foi possível enviá-lo para o Google Calendar.", ToastKind.Error);
        }
    }

    private async Task StopActiveTimerAsync()
    {
        try
        {
            IReadOnlyList<TimeEntry> created = await timerService.StopAsync(clock.Now, _lifetime.Token);

            foreach (TimeEntry entry in created)
            {
                Upsert(entry);
            }

            if (created.Any(e => e.TaskItemId is not null))
            {
                Tasks = await taskService.GetAllAsync(_lifetime.Token);
            }
        }
        catch (NotFoundException)
        {
            await LoadRangeAsync();
        }

        ActiveTimer = null;
    }

    private async Task LoadReferenceDataAsync()
    {
        await ReloadAgendasAsync();
        await ReloadProjectsAsync();
        await ReloadTagsAsync();
    }

    private bool IsAgendaVisible(Guid agendaId) =>
        agendaId == Guid.Empty || Agendas.FirstOrDefault(a => a.Id == agendaId)?.IsVisible != false;

    private async Task ReloadAgendasAsync() => Agendas = await agendaService.GetAllAsync(_lifetime.Token);

    private async Task ReloadProjectsAsync()
    {
        Projects = await projectService.GetAllAsync(_lifetime.Token);
        ProjectsById = Projects.ToDictionary(p => p.Id);
    }

    private async Task ReloadTagsAsync() => Tags = await tagService.GetAllAsync(_lifetime.Token);

    private async Task ClearHighlightAsync(Guid entryId)
    {
        try
        {
            await Task.Delay(HighlightDuration, _lifetime.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (HighlightedEntryId == entryId)
        {
            HighlightedEntryId = null;
            NotifyChanged();
        }
    }

    private List<DateOnly> BuildDays()
    {
        if (View == AgendaView.Day)
        {
            return [Anchor];
        }

        int offset = Preferences.WeekStartsOnSunday ? (int)Anchor.DayOfWeek : ((int)Anchor.DayOfWeek + 6) % 7;
        DateOnly first = Anchor.AddDays(-offset);
        IEnumerable<DateOnly> week = Enumerable.Range(0, 7).Select(first.AddDays);

        return [.. Preferences.HideWeekends ? week.Where(d => d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)) : week];
    }

    private async Task LoadRangeAsync()
    {
        List<DateOnly> days = BuildDays();
        DateOnly from = days[0];
        DateOnly to = days[^1];

        IReadOnlyList<TimeEntry> entries = await entryService.GetRangeAsync(from, to, _lifetime.Token);
        IReadOnlyList<DayNote> notes = await noteService.GetRangeAsync(from, to, _lifetime.Token);

        Days = days;
        _entries = [.. entries];
        _notes = notes.ToDictionary(n => n.Date, n => n.Text);
    }

    private void Upsert(TimeEntry entry)
    {
        _entries.RemoveAll(e => e.Id == entry.Id);

        if (Days.Contains(entry.Date))
        {
            _entries.Add(entry);
        }
    }

    private async Task<bool> RunSafeAsync(Func<Task> action, bool notify = true)
    {
        bool succeeded = false;

        try
        {
            await action();
            succeeded = true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            toasts.Show("Dados inválidos. Verifica os campos e tenta novamente.", ToastKind.Error);
        }
        catch (NotFoundException)
        {
            toasts.Show("O item já não existe. A vista foi atualizada.", ToastKind.Error);
        }
        catch (UnauthorizedAccessException)
        {
            toasts.Show("A sessão expirou. Volta a entrar.", ToastKind.Error);
        }
        catch (ServerUnavailableException)
        {
            toasts.Show("Sem ligação ao servidor. Verifica a tua ligação e tenta novamente.", ToastKind.Error);
        }
        catch (PersistenceException)
        {
            toasts.Show("Não foi possível guardar as alterações. Tenta novamente.", ToastKind.Error);
        }

        if (notify)
        {
            NotifyChanged();
        }

        return succeeded;
    }

    private void HookActivation()
    {
        if (_activationHooked)
        {
            return;
        }

        activation.Activated += OnActivated;
        _activationHooked = true;
    }

    private void OnActivated() => _ = RefreshAsync();

    private void NotifyChanged() => Changed?.Invoke();
}
