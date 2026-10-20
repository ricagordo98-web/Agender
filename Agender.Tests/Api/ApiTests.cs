using System.Net;
using System.Net.Http.Json;
using Agender.Shared.Contracts;
using Agender.Shared.Domain;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;

namespace Agender.Tests.Api;

public sealed class ApiTests : IClassFixture<AgenderApiFactory>
{
    private const string Range = "from=2026-10-12&to=2026-10-18";
    private static readonly DateOnly Day = new(2026, 10, 13);

    private readonly AgenderApiFactory _factory;

    public ApiTests(AgenderApiFactory factory) => _factory = factory;

    private static TimeEntryDto NewEntry(int start = 540, int end = 600, string description = "Standup") =>
        new(Guid.NewGuid(), Day, start, end, description, EntryColor.Teal, null, null, []);

    [Fact]
    public async Task GetEntries_WithoutToken_ReturnsUnauthorized()
    {
        using HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync($"/api/entries?{Range}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsUnauthorized()
    {
        await _factory.CreateAuthenticatedClientAsync("wrong-password@example.com");
        using HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/auth/login", new { email = "wrong-password@example.com", password = "nope" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Register_Endpoint_IsNotExposed()
    {
        using HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/auth/register", new { email = "x@example.com", password = AgenderApiFactory.Password });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Me_AuthenticatedUser_ReturnsDisplayName()
    {
        using HttpClient client = await _factory.CreateAuthenticatedClientAsync("me@example.com", "Maria Silva");

        UserInfoDto? me = await client.GetFromJsonAsync<UserInfoDto>("/api/me");

        me.Should().BeEquivalentTo(new UserInfoDto("me@example.com", "Maria Silva"));
    }

    [Fact]
    public async Task Entries_CreateUpdateDelete_RoundTrips()
    {
        using HttpClient client = await _factory.CreateAuthenticatedClientAsync("crud@example.com");
        TimeEntryDto entry = NewEntry();

        HttpResponseMessage created = await client.PostAsJsonAsync("/api/entries", entry);
        created.StatusCode.Should().Be(HttpStatusCode.Created);

        TimeEntryDto[] listed = (await client.GetFromJsonAsync<TimeEntryDto[]>($"/api/entries?{Range}"))!;
        listed.Should().ContainSingle().Which.Should().BeEquivalentTo(entry, options => options.Excluding(e => e.AgendaId));
        listed.Single().AgendaId.Should().NotBe(Guid.Empty);

        HttpResponseMessage updated = await client.PutAsJsonAsync($"/api/entries/{entry.Id}", entry with { EndMinute = 660 });
        updated.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetFromJsonAsync<TimeEntryDto[]>($"/api/entries?{Range}"))!.Single().EndMinute.Should().Be(660);

        HttpResponseMessage deleted = await client.DeleteAsync($"/api/entries/{entry.Id}");
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetFromJsonAsync<TimeEntryDto[]>($"/api/entries?{Range}"))!.Should().BeEmpty();
    }

    [Fact]
    public async Task Entries_InvalidSlot_ReturnsValidationProblem()
    {
        using HttpClient client = await _factory.CreateAuthenticatedClientAsync("invalid@example.com");

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/entries", NewEntry(start: 600, end: 540));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ValidationProblemDetails? problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        problem!.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Entries_RangeTooLarge_ReturnsProblemDetails()
    {
        using HttpClient client = await _factory.CreateAuthenticatedClientAsync("range@example.com");

        HttpResponseMessage response = await client.GetAsync("/api/entries?from=2020-01-01&to=2026-01-01");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Status.Should().Be(400);
    }

    [Fact]
    public async Task Entries_ArePrivatePerUser()
    {
        using HttpClient owner = await _factory.CreateAuthenticatedClientAsync("owner@example.com");
        using HttpClient intruder = await _factory.CreateAuthenticatedClientAsync("intruder@example.com");
        TimeEntryDto entry = NewEntry(description: "Private");
        await owner.PostAsJsonAsync("/api/entries", entry);

        (await intruder.GetFromJsonAsync<TimeEntryDto[]>($"/api/entries?{Range}"))!.Should().BeEmpty();
        (await intruder.PutAsJsonAsync($"/api/entries/{entry.Id}", entry with { Description = "hijacked" })).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await owner.GetFromJsonAsync<TimeEntryDto[]>($"/api/entries?{Range}"))!.Single().Description.Should().Be("Private");
    }

    [Fact]
    public async Task Tasks_CreateAndList_RoundTrips()
    {
        using HttpClient client = await _factory.CreateAuthenticatedClientAsync("tasks@example.com");
        TaskDto task = new(Guid.NewGuid(), "Write report", EntryColor.Rose, false, DateTime.UtcNow, null);

        (await client.PostAsJsonAsync("/api/tasks", task)).StatusCode.Should().Be(HttpStatusCode.Created);

        TaskDto[] tasks = (await client.GetFromJsonAsync<TaskDto[]>("/api/tasks"))!;
        tasks.Should().ContainSingle().Which.Title.Should().Be("Write report");
    }

    [Fact]
    public async Task Notes_SaveAndList_RoundTrips()
    {
        using HttpClient client = await _factory.CreateAuthenticatedClientAsync("notes@example.com");

        (await client.PutAsJsonAsync($"/api/notes/{Day:yyyy-MM-dd}", new SaveNoteRequest("Focus day"))).StatusCode.Should().Be(HttpStatusCode.NoContent);

        DayNoteDto[] notes = (await client.GetFromJsonAsync<DayNoteDto[]>($"/api/notes?{Range}"))!;
        notes.Should().ContainSingle().Which.Should().BeEquivalentTo(new DayNoteDto(Day, "Focus day"));
    }

    [Fact]
    public async Task Projects_CreateEntryWithProjectAndTags_RoundTrips()
    {
        using HttpClient client = await _factory.CreateAuthenticatedClientAsync("projects@example.com");
        ProjectDto project = new(Guid.NewGuid(), "Website", "ACME", EntryColor.Sky, false, DateTime.UtcNow);

        (await client.PostAsJsonAsync("/api/projects", project)).StatusCode.Should().Be(HttpStatusCode.Created);

        TimeEntryDto entry = NewEntry() with { ProjectId = project.Id, Tags = ["urgent", "client"] };
        (await client.PostAsJsonAsync("/api/entries", entry)).StatusCode.Should().Be(HttpStatusCode.Created);

        TimeEntryDto saved = (await client.GetFromJsonAsync<TimeEntryDto[]>($"/api/entries?{Range}"))!.Single();
        saved.ProjectId.Should().Be(project.Id);
        saved.Tags.Should().BeEquivalentTo(["client", "urgent"]);
        (await client.GetFromJsonAsync<string[]>("/api/tags"))!.Should().BeEquivalentTo(["client", "urgent"]);

        ProjectMinutesDto[] stats = (await client.GetFromJsonAsync<ProjectMinutesDto[]>($"/api/projects/stats?{Range}"))!;
        stats.Should().ContainSingle().Which.Minutes.Should().Be(60);
    }

    [Fact]
    public async Task Search_FindsEntriesByText()
    {
        using HttpClient client = await _factory.CreateAuthenticatedClientAsync("search@example.com");
        await client.PostAsJsonAsync("/api/entries", NewEntry(description: "Quarterly report"));
        await client.PostAsJsonAsync("/api/entries", NewEntry(description: "Lunch"));

        TimeEntryDto[] found = (await client.GetFromJsonAsync<TimeEntryDto[]>("/api/entries/search?q=report"))!;

        found.Should().ContainSingle().Which.Description.Should().Be("Quarterly report");
    }

    [Fact]
    public async Task Projects_DuplicateName_ReturnsBadRequest()
    {
        using HttpClient client = await _factory.CreateAuthenticatedClientAsync("dupes@example.com");
        await client.PostAsJsonAsync("/api/projects", new ProjectDto(Guid.NewGuid(), "Website", null, EntryColor.Sky, false, DateTime.UtcNow));

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/projects", new ProjectDto(Guid.NewGuid(), "website", null, EntryColor.Sky, false, DateTime.UtcNow));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Timer_StartAndStop_RoundTripsAndCreatesEntry()
    {
        using HttpClient client = await _factory.CreateAuthenticatedClientAsync("timer@example.com");
        DateTime start = new(2026, 10, 13, 9, 0, 0);

        (await client.GetAsync("/api/timer")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        HttpResponseMessage started = await client.PostAsJsonAsync("/api/timer/start", new ActiveTimerDto(Guid.NewGuid(), start, "Review", null, null));
        started.StatusCode.Should().Be(HttpStatusCode.Created);

        ActiveTimerDto active = (await client.GetFromJsonAsync<ActiveTimerDto>("/api/timer"))!;
        active.StartedAt.Should().Be(start);
        active.Description.Should().Be("Review");

        HttpResponseMessage second = await client.PostAsJsonAsync("/api/timer/start", new ActiveTimerDto(Guid.NewGuid(), start, "Again", null, null));
        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        HttpResponseMessage stopped = await client.PostAsJsonAsync("/api/timer/stop", new StopTimerRequest(start.AddMinutes(45)));
        stopped.StatusCode.Should().Be(HttpStatusCode.OK);
        TimeEntryDto[] created = (await stopped.Content.ReadFromJsonAsync<TimeEntryDto[]>())!;
        created.Should().ContainSingle().Which.Should().Match<TimeEntryDto>(e => e.StartMinute == 540 && e.EndMinute == 585 && e.Description == "Review");

        (await client.GetAsync("/api/timer")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.GetFromJsonAsync<TimeEntryDto[]>($"/api/entries?{Range}"))!.Should().ContainSingle();
    }

    [Fact]
    public async Task Timer_StopWithoutRunningTimer_ReturnsNotFound()
    {
        using HttpClient client = await _factory.CreateAuthenticatedClientAsync("timer-none@example.com");

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/timer/stop", new StopTimerRequest(new DateTime(2026, 10, 13, 10, 0, 0)));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Google_Status_NotConnected_ReportsNotConnected()
    {
        using HttpClient client = await _factory.CreateAuthenticatedClientAsync("google-status@example.com");

        GoogleStatusDto? status = await client.GetFromJsonAsync<GoogleStatusDto>("/api/google/status");

        status.Should().Be(new GoogleStatusDto(false, null, null, null));
    }

    [Fact]
    public async Task Google_Connect_InvalidKey_ReturnsBadRequest()
    {
        using HttpClient client = await _factory.CreateAuthenticatedClientAsync("google-badkey@example.com");

        HttpResponseMessage response = await client.PutAsJsonAsync("/api/google", new GoogleConnectRequest("not a key", "me@example.com", null));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.GetFromJsonAsync<GoogleStatusDto>("/api/google/status"))!.IsConnected.Should().BeFalse();
    }

    [Fact]
    public async Task Google_Connect_MissingCalendar_ReturnsValidationProblem()
    {
        using HttpClient client = await _factory.CreateAuthenticatedClientAsync("google-nocal@example.com");

        HttpResponseMessage response = await client.PutAsJsonAsync("/api/google", new GoogleConnectRequest("{}", string.Empty, null));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>())!.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Google_Events_WithoutConnection_ReturnsBadRequest()
    {
        using HttpClient client = await _factory.CreateAuthenticatedClientAsync("google-events@example.com");

        HttpResponseMessage response = await client.GetAsync($"/api/google/events?{Range}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Google_Status_WithoutToken_ReturnsUnauthorized()
    {
        using HttpClient client = _factory.CreateClient();

        (await client.GetAsync("/api/google/status")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Google_Disconnect_WhenNotConnected_ReturnsNoContent()
    {
        using HttpClient client = await _factory.CreateAuthenticatedClientAsync("google-disconnect@example.com");

        (await client.DeleteAsync("/api/google")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Tasks_WithAnEntry_AreReportedAsScheduled()
    {
        using HttpClient client = await _factory.CreateAuthenticatedClientAsync("task-scheduled@example.com");
        TaskDto task = new(Guid.NewGuid(), "Write report", EntryColor.Rose, false, DateTime.UtcNow, null);
        await client.PostAsJsonAsync("/api/tasks", task);
        (await client.GetFromJsonAsync<TaskDto[]>("/api/tasks"))!.Single().IsScheduled.Should().BeFalse();

        TimeEntryDto entry = new(Guid.NewGuid(), Day, 540, 570, "Write report", EntryColor.Rose, task.Id, null, []);
        await client.PostAsJsonAsync("/api/entries", entry);

        (await client.GetFromJsonAsync<TaskDto[]>("/api/tasks"))!.Single().IsScheduled.Should().BeTrue();

        await client.DeleteAsync($"/api/entries/{entry.Id}");

        (await client.GetFromJsonAsync<TaskDto[]>("/api/tasks"))!.Single().IsScheduled.Should().BeFalse();
    }

    [Fact]
    public async Task Google_DeleteEvent_WithoutConnection_ReturnsBadRequest()
    {
        using HttpClient client = await _factory.CreateAuthenticatedClientAsync("google-delete@example.com");

        HttpResponseMessage response = await client.DeleteAsync("/api/google/events?calendarId=me%40example.com&eventId=abc");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Google_DeleteEvent_WithoutToken_ReturnsUnauthorized()
    {
        using HttpClient client = _factory.CreateClient();

        (await client.DeleteAsync("/api/google/events?calendarId=me%40example.com&eventId=abc")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
