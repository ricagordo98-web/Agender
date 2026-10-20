using System.Security.Claims;
using Agender.Infrastructure.Identity;
using Agender.Shared.Contracts;
using Agender.Shared.Domain;
using Agender.Shared.Google;
using Agender.Shared.Services;

namespace Agender.Web.Api;

public static class ApiEndpoints
{
    private const int MaxRangeDays = 400;
    private const int DefaultSearchPageSize = 20;

    private static readonly string[] AllowedAuthPaths = ["/api/auth/login", "/api/auth/refresh"];

    public static IApplicationBuilder UseAuthApiAllowlist(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        PathString path = context.Request.Path;
        bool isAuthApi = path.StartsWithSegments("/api/auth");

        if (isAuthApi && !AllowedAuthPaths.Any(allowed => path.Equals(allowed, StringComparison.OrdinalIgnoreCase)))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        await next();
    });

    public static IEndpointRouteBuilder MapAgenderApi(this IEndpointRouteBuilder app)
    {
        app.MapGroup("/api/auth").MapIdentityApi<AgenderUser>();

        RouteGroupBuilder api = app.MapGroup("/api").RequireAuthorization();

        api.MapGet("/me", (ClaimsPrincipal user) => new UserInfoDto(
            user.Identity?.Name ?? string.Empty,
            user.FindFirstValue(ClaimTypes.GivenName) ?? user.Identity?.Name ?? string.Empty));

        MapEntries(api.MapGroup("/entries"));
        MapTasks(api.MapGroup("/tasks"));
        MapNotes(api.MapGroup("/notes"));
        MapProjects(api.MapGroup("/projects"));
        MapTimer(api.MapGroup("/timer"));
        MapGoogle(api.MapGroup("/google"));
        MapSharing(api.MapGroup("/sharing"));
        MapAgendas(api.MapGroup("/agendas"));

        api.MapGet("/settings", async (IUserSettingsService service, CancellationToken ct) => (await service.GetAsync(ct)).ToDto());

        api.MapPut("/settings", async (UserSettingsDto dto, IUserSettingsService service, CancellationToken ct) =>
        {
            await service.SaveAsync(dto.ToEntity(), ct);
            return Results.NoContent();
        }).AddEndpointFilter<ValidationFilter<UserSettingsDto>>();

        api.MapGet("/tags", async (ITagService service, CancellationToken ct) => await service.GetAllAsync(ct));

        return app;
    }

    private static void MapEntries(RouteGroupBuilder group)
    {
        group.MapGet("/", async (DateOnly from, DateOnly to, ITimeEntryService service, CancellationToken ct) =>
        {
            EnsureValidRange(from, to);
            IReadOnlyList<TimeEntry> entries = await service.GetRangeAsync(from, to, ct);
            return entries.Select(e => e.ToDto());
        });

        group.MapGet("/search", async (string q, int? skip, int? take, ITimeEntryService service, CancellationToken ct) =>
        {
            IReadOnlyList<TimeEntry> entries = await service.SearchAsync(q, skip ?? 0, take ?? DefaultSearchPageSize, ct);
            return entries.Select(e => e.ToDto());
        });

        group.MapPost("/", async (TimeEntryDto dto, ITimeEntryService service, CancellationToken ct) =>
        {
            TimeEntry created = await service.CreateAsync(dto.ToEntity(), ct);
            return Results.Created($"/api/entries/{created.Id}", created.ToDto());
        }).AddEndpointFilter<ValidationFilter<TimeEntryDto>>();

        group.MapPut("/{id:guid}", async (Guid id, TimeEntryDto dto, ITimeEntryService service, CancellationToken ct) =>
        {
            EnsureSameId(id, dto.Id);
            await service.UpdateAsync(dto.ToEntity(), ct);
            return Results.NoContent();
        }).AddEndpointFilter<ValidationFilter<TimeEntryDto>>();

        group.MapDelete("/{id:guid}", async (Guid id, ITimeEntryService service, CancellationToken ct) =>
        {
            await service.DeleteAsync(id, ct);
            return Results.NoContent();
        });
    }

    private static void MapTasks(RouteGroupBuilder group)
    {
        group.MapGet("/", async (ITaskService service, CancellationToken ct) =>
            (await service.GetAllAsync(ct)).Select(t => t.ToDto()));

        group.MapPost("/", async (TaskDto dto, ITaskService service, CancellationToken ct) =>
        {
            TaskItem created = await service.CreateAsync(dto.ToEntity(), ct);
            return Results.Created($"/api/tasks/{created.Id}", created.ToDto());
        }).AddEndpointFilter<ValidationFilter<TaskDto>>();

        group.MapPut("/{id:guid}", async (Guid id, TaskDto dto, ITaskService service, CancellationToken ct) =>
        {
            EnsureSameId(id, dto.Id);
            await service.UpdateAsync(dto.ToEntity(), ct);
            return Results.NoContent();
        }).AddEndpointFilter<ValidationFilter<TaskDto>>();

        group.MapDelete("/{id:guid}", async (Guid id, ITaskService service, CancellationToken ct) =>
        {
            await service.DeleteAsync(id, ct);
            return Results.NoContent();
        });
    }

    private static void MapNotes(RouteGroupBuilder group)
    {
        group.MapGet("/", async (DateOnly from, DateOnly to, IDayNoteService service, CancellationToken ct) =>
        {
            EnsureValidRange(from, to);
            IReadOnlyList<DayNote> notes = await service.GetRangeAsync(from, to, ct);
            return notes.Select(n => n.ToDto());
        });

        group.MapPut("/{date}", async (DateOnly date, SaveNoteRequest request, IDayNoteService service, CancellationToken ct) =>
        {
            await service.SaveAsync(date, request.Text, ct);
            return Results.NoContent();
        }).AddEndpointFilter<ValidationFilter<SaveNoteRequest>>();
    }

    private static void MapProjects(RouteGroupBuilder group)
    {
        group.MapGet("/", async (IProjectService service, CancellationToken ct) =>
            (await service.GetAllAsync(ct)).Select(p => p.ToDto()));

        group.MapGet("/stats", async (DateOnly from, DateOnly to, IProjectService service, CancellationToken ct) =>
        {
            EnsureValidRange(from, to);
            IReadOnlyDictionary<Guid, int> totals = await service.GetMinutesByProjectAsync(from, to, ct);
            return totals.Select(t => new ProjectMinutesDto(t.Key, t.Value));
        });

        group.MapPost("/", async (ProjectDto dto, IProjectService service, CancellationToken ct) =>
        {
            Project created = await service.CreateAsync(dto.ToEntity(), ct);
            return Results.Created($"/api/projects/{created.Id}", created.ToDto());
        }).AddEndpointFilter<ValidationFilter<ProjectDto>>();

        group.MapPut("/{id:guid}", async (Guid id, ProjectDto dto, IProjectService service, CancellationToken ct) =>
        {
            EnsureSameId(id, dto.Id);
            await service.UpdateAsync(dto.ToEntity(), ct);
            return Results.NoContent();
        }).AddEndpointFilter<ValidationFilter<ProjectDto>>();

        group.MapDelete("/{id:guid}", async (Guid id, IProjectService service, CancellationToken ct) =>
        {
            await service.DeleteAsync(id, ct);
            return Results.NoContent();
        });
    }

    private static void MapGoogle(RouteGroupBuilder group)
    {
        group.MapGet("/status", async (IGoogleCalendarService service, CancellationToken ct) =>
        {
            GoogleStatus status = await service.GetStatusAsync(ct);
            return new GoogleStatusDto(status.IsConnected, status.ServiceAccountEmail, status.CalendarId, status.WriteCalendarId);
        });

        group.MapPut("/", async (GoogleConnectRequest request, IGoogleCalendarService service, CancellationToken ct) =>
        {
            await service.ConnectAsync(request.ServiceAccountJson, request.CalendarId, request.WriteCalendarId, ct);
            return Results.NoContent();
        }).AddEndpointFilter<ValidationFilter<GoogleConnectRequest>>();

        group.MapDelete("/", async (IGoogleCalendarService service, CancellationToken ct) =>
        {
            await service.DisconnectAsync(ct);
            return Results.NoContent();
        });

        group.MapGet("/events", async (DateOnly from, DateOnly to, bool? includeOwn, IGoogleCalendarService service, CancellationToken ct) =>
        {
            EnsureValidRange(from, to);
            IReadOnlyList<GoogleEvent> events = await service.GetEventsAsync(from, to, includeOwn ?? false, ct);
            return events.Select(e => new GoogleEventDto(e.Id, e.Title, e.Date, e.StartMinute, e.EndMinute, e.IsAgender, e.EntryId, e.CalendarId));
        });

        group.MapDelete("/events", async (string calendarId, string eventId, IGoogleCalendarService service, CancellationToken ct) =>
        {
            await service.DeleteEventAsync(calendarId, eventId, ct);
            return Results.NoContent();
        });

        group.MapPost("/retry", async (IGoogleCalendarService service, CancellationToken ct) =>
            new GoogleRetryDto(await service.RetryFailedAsync(ct)));
    }

    private static void MapAgendas(RouteGroupBuilder group)
    {
        group.MapGet("/", async (IAgendaService service, CancellationToken ct) =>
            (await service.GetAllAsync(ct)).Select(a => a.ToDto()));

        group.MapPost("/", async (AgendaDto dto, IAgendaService service, CancellationToken ct) =>
        {
            Agenda created = await service.CreateAsync(dto.ToEntity(), ct);
            return Results.Created($"/api/agendas/{created.Id}", created.ToDto());
        }).AddEndpointFilter<ValidationFilter<AgendaDto>>();

        group.MapPut("/{id:guid}", async (Guid id, AgendaDto dto, IAgendaService service, CancellationToken ct) =>
        {
            EnsureSameId(id, dto.Id);
            await service.UpdateAsync(dto.ToEntity(), ct);
            return Results.NoContent();
        }).AddEndpointFilter<ValidationFilter<AgendaDto>>();

        group.MapPost("/{id:guid}/activate", async (Guid id, IAgendaService service, CancellationToken ct) =>
        {
            await service.SetActiveAsync(id, ct);
            return Results.NoContent();
        });

        group.MapDelete("/{id:guid}", async (Guid id, IAgendaService service, CancellationToken ct) =>
        {
            await service.DeleteAsync(id, ct);
            return Results.NoContent();
        });
    }

    private static void MapSharing(RouteGroupBuilder group)
    {
        group.MapGet("/", async (IAgendaSharingService service, CancellationToken ct) => await service.GetSharesAsync(ct));

        group.MapPost("/", async (ShareRequest request, IAgendaSharingService service, CancellationToken ct) =>
            Results.Ok(await service.ShareAsync(request.Email, ct))).AddEndpointFilter<ValidationFilter<ShareRequest>>();

        group.MapDelete("/{id:guid}", async (Guid id, IAgendaSharingService service, CancellationToken ct) =>
        {
            await service.RevokeAsync(id, ct);
            return Results.NoContent();
        });

        group.MapGet("/received", async (IAgendaSharingService service, CancellationToken ct) => await service.GetSharedWithMeAsync(ct));

        group.MapGet("/received/{id:guid}/agenda", async (Guid id, DateOnly from, DateOnly to, IAgendaSharingService service, CancellationToken ct) =>
        {
            EnsureValidRange(from, to);
            return await service.GetSharedAgendaAsync(id, from, to, ct);
        });

        group.MapGet("/link", async (IAgendaSharingService service, CancellationToken ct) => await service.GetLinkAsync(ct));

        group.MapPost("/link", async (IAgendaSharingService service, CancellationToken ct) => await service.CreateLinkAsync(ct));

        group.MapDelete("/link", async (IAgendaSharingService service, CancellationToken ct) =>
        {
            await service.RevokeLinkAsync(ct);
            return Results.NoContent();
        });
    }

    private static void MapTimer(RouteGroupBuilder group)
    {
        group.MapGet("/", async (ITimerService service, CancellationToken ct) =>
        {
            ActiveTimer? timer = await service.GetActiveAsync(ct);
            return timer is null ? Results.NotFound() : Results.Ok(timer.ToDto());
        });

        group.MapPost("/start", async (ActiveTimerDto dto, ITimerService service, CancellationToken ct) =>
        {
            ActiveTimer started = await service.StartAsync(dto.ToEntity(), ct);
            return Results.Created("/api/timer", started.ToDto());
        }).AddEndpointFilter<ValidationFilter<ActiveTimerDto>>();

        group.MapPost("/stop", async (StopTimerRequest request, ITimerService service, CancellationToken ct) =>
        {
            IReadOnlyList<TimeEntry> entries = await service.StopAsync(request.StoppedAt, ct);
            return entries.Select(e => e.ToDto());
        });
    }

    private static void EnsureSameId(Guid routeId, Guid bodyId)
    {
        if (routeId != bodyId)
        {
            throw new ArgumentException("The identifier in the route does not match the body.");
        }
    }

    private static void EnsureValidRange(DateOnly from, DateOnly to)
    {
        if (to < from || to.DayNumber - from.DayNumber > MaxRangeDays)
        {
            throw new ArgumentException($"The range must be ascending and span at most {MaxRangeDays} days.");
        }
    }
}
