using Agender.Infrastructure.Services;
using Agender.Shared.Domain;
using FluentAssertions;

namespace Agender.Tests.Services;

public sealed class AgendaServiceTests : IDisposable
{
    private static readonly DateOnly Day = new(2026, 10, 12);

    private readonly TestDb _db = new();
    private readonly AgendaService _sut;

    public AgendaServiceTests() => _sut = new AgendaService(_db);

    public void Dispose() => _db.Dispose();

    private async Task<Guid> DefaultIdAsync() => (await _sut.GetAllAsync()).Single(a => a.IsDefault).Id;

    [Fact]
    public async Task GetAllAsync_NewUser_CreatesTheDefaultActiveAgenda()
    {
        IReadOnlyList<Agenda> agendas = await _sut.GetAllAsync();

        Agenda agenda = agendas.Should().ContainSingle().Subject;
        agenda.Name.Should().Be(Agenda.DefaultName);
        agenda.IsDefault.Should().BeTrue();
        agenda.IsActive.Should().BeTrue();
        agenda.IsVisible.Should().BeTrue();
    }

    [Fact]
    public async Task GetAllAsync_CalledTwice_DoesNotDuplicateTheDefault()
    {
        await _sut.GetAllAsync();

        (await _sut.GetAllAsync()).Should().ContainSingle();
    }

    [Fact]
    public async Task CreateAsync_NewAgenda_IsVisibleButNotActive()
    {
        Agenda created = await _sut.CreateAsync(new Agenda { Name = "  Pessoal ", CustomColor = "#ff0000" });

        created.Name.Should().Be("Pessoal");
        created.CustomColor.Should().Be("#FF0000");
        created.IsVisible.Should().BeTrue();
        created.IsActive.Should().BeFalse();
        created.IsDefault.Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsync_DuplicateName_Throws()
    {
        await _sut.CreateAsync(new Agenda { Name = "Trabalho" });

        Func<Task> act = () => _sut.CreateAsync(new Agenda { Name = "trabalho" });

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SetActiveAsync_MakesOnlyThatAgendaActiveAndVisible()
    {
        Agenda work = await _sut.CreateAsync(new Agenda { Name = "Trabalho" });
        await _sut.UpdateAsync(new Agenda { Id = work.Id, Name = "Trabalho", IsVisible = false });

        await _sut.SetActiveAsync(work.Id);

        IReadOnlyList<Agenda> agendas = await _sut.GetAllAsync();
        agendas.Where(a => a.IsActive).Should().ContainSingle().Which.Id.Should().Be(work.Id);
        agendas.Single(a => a.Id == work.Id).IsVisible.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateAsync_HidingTheActiveAgenda_Throws()
    {
        Agenda active = (await _sut.GetAllAsync()).Single(a => a.IsActive);

        Func<Task> act = () => _sut.UpdateAsync(new Agenda { Id = active.Id, Name = active.Name, IsVisible = false });

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task UpdateAsync_HidingAnInactiveAgenda_Works()
    {
        Agenda work = await _sut.CreateAsync(new Agenda { Name = "Trabalho" });

        await _sut.UpdateAsync(new Agenda { Id = work.Id, Name = "Trabalho", IsVisible = false });

        (await _sut.GetAllAsync()).Single(a => a.Id == work.Id).IsVisible.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_DefaultAgenda_Throws()
    {
        Func<Task> act = async () => await _sut.DeleteAsync(await DefaultIdAsync());

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task DeleteAsync_MovesItsContentToTheDefaultAgenda()
    {
        Agenda work = await _sut.CreateAsync(new Agenda { Name = "Trabalho" });
        Project project = await new ProjectService(_db).CreateAsync(new Project { Name = "Site", AgendaId = work.Id });
        await new TaskService(_db).CreateAsync(new TaskItem { Title = "T", AgendaId = work.Id });
        await new TimeEntryService(_db).CreateAsync(new TimeEntry { Date = Day, StartMinute = 60, EndMinute = 120, ProjectId = project.Id });
        Guid defaultId = await DefaultIdAsync();

        await _sut.DeleteAsync(work.Id);

        (await new ProjectService(_db).GetAllAsync()).Single().AgendaId.Should().Be(defaultId);
        (await new TaskService(_db).GetAllAsync()).Single().AgendaId.Should().Be(defaultId);
        (await new TimeEntryService(_db).GetRangeAsync(Day, Day)).Single().AgendaId.Should().Be(defaultId);
        (await _sut.GetAllAsync()).Should().ContainSingle();
    }

    [Fact]
    public async Task DeleteAsync_ActiveAgenda_ReturnsTheActiveFlagToTheDefault()
    {
        Agenda work = await _sut.CreateAsync(new Agenda { Name = "Trabalho" });
        await _sut.SetActiveAsync(work.Id);

        await _sut.DeleteAsync(work.Id);

        (await _sut.GetAllAsync()).Single().IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Agendas_OfAnotherUser_AreNotVisible()
    {
        await new AgendaService(_db.ForUser("user-a")).CreateAsync(new Agenda { Name = "Só da A" });

        IReadOnlyList<Agenda> other = await new AgendaService(_db.ForUser("user-b")).GetAllAsync();

        other.Should().ContainSingle().Which.Name.Should().Be(Agenda.DefaultName);
    }

    [Fact]
    public async Task ProjectService_CreateWithoutAgenda_GoesToTheDefaultAgenda()
    {
        Project project = await new ProjectService(_db).CreateAsync(new Project { Name = "Site" });

        project.AgendaId.Should().Be(await DefaultIdAsync());
    }

    [Fact]
    public async Task ProjectService_UnknownAgenda_Throws()
    {
        Func<Task> act = () => new ProjectService(_db).CreateAsync(new Project { Name = "Site", AgendaId = Guid.NewGuid() });

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ProjectService_Update_DoesNotMoveTheProjectToAnotherAgenda()
    {
        Agenda work = await _sut.CreateAsync(new Agenda { Name = "Trabalho" });
        Project project = await new ProjectService(_db).CreateAsync(new Project { Name = "Site", AgendaId = work.Id });

        await new ProjectService(_db).UpdateAsync(new Project { Id = project.Id, Name = "Site 2", AgendaId = Guid.NewGuid() });

        (await new ProjectService(_db).GetAllAsync()).Single().AgendaId.Should().Be(work.Id);
    }

    [Fact]
    public async Task TaskService_ProjectFromAnotherAgenda_Throws()
    {
        Agenda work = await _sut.CreateAsync(new Agenda { Name = "Trabalho" });
        Project project = await new ProjectService(_db).CreateAsync(new Project { Name = "Site", AgendaId = work.Id });
        Guid defaultId = await DefaultIdAsync();

        Func<Task> act = () => new TaskService(_db).CreateAsync(new TaskItem { Title = "T", ProjectId = project.Id, AgendaId = defaultId });

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task TaskService_WithProjectAndNoAgenda_TakesTheProjectAgenda()
    {
        Agenda work = await _sut.CreateAsync(new Agenda { Name = "Trabalho" });
        Project project = await new ProjectService(_db).CreateAsync(new Project { Name = "Site", AgendaId = work.Id });

        TaskItem task = await new TaskService(_db).CreateAsync(new TaskItem { Title = "T", ProjectId = project.Id });

        task.AgendaId.Should().Be(work.Id);
    }

    [Fact]
    public async Task TimeEntryService_EntryWithProject_FollowsTheProjectAgenda()
    {
        Agenda work = await _sut.CreateAsync(new Agenda { Name = "Trabalho" });
        Project project = await new ProjectService(_db).CreateAsync(new Project { Name = "Site", AgendaId = work.Id });

        TimeEntry entry = await new TimeEntryService(_db).CreateAsync(new TimeEntry { Date = Day, StartMinute = 60, EndMinute = 120, ProjectId = project.Id });

        entry.AgendaId.Should().Be(work.Id);
    }

    [Fact]
    public async Task TimeEntryService_EntryWithoutProjectOrTask_UsesTheRequestedAgenda()
    {
        Agenda work = await _sut.CreateAsync(new Agenda { Name = "Trabalho" });

        TimeEntry entry = await new TimeEntryService(_db).CreateAsync(new TimeEntry { Date = Day, StartMinute = 60, EndMinute = 120, AgendaId = work.Id });

        entry.AgendaId.Should().Be(work.Id);
    }

    [Fact]
    public async Task TimerService_Stop_EntryUsesTheTimerAgenda()
    {
        Agenda work = await _sut.CreateAsync(new Agenda { Name = "Trabalho" });
        TimerService timer = new(_db);
        await timer.StartAsync(new ActiveTimer { StartedAt = new DateTime(2026, 10, 14, 9, 0, 0), AgendaId = work.Id });

        IReadOnlyList<TimeEntry> entries = await timer.StopAsync(new DateTime(2026, 10, 14, 9, 30, 0));

        entries.Single().AgendaId.Should().Be(work.Id);
    }
}
