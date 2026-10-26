using Agender.Infrastructure.Data;
using Agender.Infrastructure.Identity;
using Agender.Infrastructure.Sharing;
using Agender.Infrastructure.Services;
using Agender.Shared.Contracts;
using Agender.Shared.Domain;
using Agender.Shared.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Agender.Tests.Services;

public sealed class AgendaSharingTests : IDisposable
{
    private const string Owner = "owner";
    private const string Viewer = "viewer";
    private const string Stranger = "stranger";

    private static readonly DateOnly Day = new(2026, 10, 12);

    private readonly TestDb _db = new();
    private readonly MutableTimeProvider _time = new(new DateTimeOffset(2026, 10, 12, 10, 0, 0, TimeSpan.Zero));

    public AgendaSharingTests()
    {
        using AgenderDbContext context = _db.Create(string.Empty);
        context.Users.AddRange(NewUser(Owner, "Ricardo", "ricardo@example.com"), NewUser(Viewer, "Ana", "ana@example.com"), NewUser(Stranger, "Rui", "rui@example.com"));
        context.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task ShareAsync_RegisteredEmail_GivesTheViewerAccess()
    {
        await ShareService(Owner).ShareAsync("ANA@example.com");

        IReadOnlyList<SharedWithMeDto> received = await ShareService(Viewer).GetSharedWithMeAsync();

        received.Should().ContainSingle().Which.DisplayName.Should().Be("Ricardo");
    }

    [Fact]
    public async Task ShareAsync_SameViewerTwice_DoesNotDuplicate()
    {
        await ShareService(Owner).ShareAsync("ana@example.com");
        await ShareService(Owner).ShareAsync("ana@example.com");

        (await ShareService(Owner).GetSharesAsync()).Should().ContainSingle();
    }

    [Fact]
    public async Task ShareAsync_UnknownEmail_Throws()
    {
        Func<Task> act = () => ShareService(Owner).ShareAsync("nobody@example.com");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ShareAsync_OwnEmail_Throws()
    {
        Func<Task> act = () => ShareService(Owner).ShareAsync("ricardo@example.com");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetSharedAgendaAsync_Viewer_SeesTheOwnersEntriesProjectsAndNotes()
    {
        Guid shareId = await SeedSharedAgendaAsync();

        SharedAgendaDto agenda = await ShareService(Viewer).GetSharedAgendaAsync(shareId, Day, Day);

        agenda.OwnerName.Should().Be("Ricardo");
        agenda.Entries.Should().ContainSingle().Which.Description.Should().Be("Reunião");
        agenda.Projects.Should().ContainSingle().Which.Name.Should().Be("Cliente X");
        agenda.Notes.Should().ContainSingle().Which.Text.Should().Be("Nota do dia");
    }

    [Fact]
    public async Task GetSharedAgendaAsync_OtherUser_IsNotFound()
    {
        Guid shareId = await SeedSharedAgendaAsync();

        Func<Task> act = () => ShareService(Stranger).GetSharedAgendaAsync(shareId, Day, Day);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetSharedAgendaAsync_AfterRevoke_IsNotFound()
    {
        Guid shareId = await SeedSharedAgendaAsync();

        await ShareService(Owner).RevokeAsync(shareId);

        Func<Task> act = () => ShareService(Viewer).GetSharedAgendaAsync(shareId, Day, Day);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task RevokeAsync_ByNonOwner_DoesNothing()
    {
        Guid shareId = await SeedSharedAgendaAsync();

        await ShareService(Stranger).RevokeAsync(shareId);

        (await ShareService(Owner).GetSharesAsync()).Should().ContainSingle();
    }

    [Fact]
    public async Task GetSharedAgendaAsync_DoesNotChangeWhatTheViewerSeesInHisOwnAgenda()
    {
        Guid shareId = await SeedSharedAgendaAsync();
        await ShareService(Viewer).GetSharedAgendaAsync(shareId, Day, Day);

        IReadOnlyList<TimeEntry> own = await new TimeEntryService(_db.ForUser(Viewer)).GetRangeAsync(Day, Day);

        own.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateLinkAsync_Token_OpensThePublicAgenda()
    {
        await SeedSharedAgendaAsync();
        NewShareLinkDto link = await ShareService(Owner).CreateLinkAsync();

        SharedAgendaDto agenda = await PublicService().GetAsync(link.Token, Day, Day);

        agenda.Entries.Should().ContainSingle();
        link.ExpiresAt.Should().Be(_time.GetUtcNow().UtcDateTime + TimeSpan.FromDays(2));
    }

    [Fact]
    public async Task GetAsync_WrongToken_IsNotFound()
    {
        await ShareService(Owner).CreateLinkAsync();

        Func<Task> act = () => PublicService().GetAsync("not-the-token", Day, Day);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetAsync_AfterTwoDays_IsNotFound()
    {
        NewShareLinkDto link = await ShareService(Owner).CreateLinkAsync();

        _time.Advance(TimeSpan.FromDays(2) + TimeSpan.FromSeconds(1));

        Func<Task> act = () => PublicService().GetAsync(link.Token, Day, Day);
        await act.Should().ThrowAsync<NotFoundException>();
        (await ShareService(Owner).GetLinkAsync()).IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task CreateLinkAsync_NewLink_InvalidatesThePreviousOne()
    {
        NewShareLinkDto first = await ShareService(Owner).CreateLinkAsync();
        await ShareService(Owner).CreateLinkAsync();

        Func<Task> act = () => PublicService().GetAsync(first.Token, Day, Day);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task RevokeLinkAsync_DisablesTheLink()
    {
        NewShareLinkDto link = await ShareService(Owner).CreateLinkAsync();

        await ShareService(Owner).RevokeLinkAsync();

        Func<Task> act = () => PublicService().GetAsync(link.Token, Day, Day);
        await act.Should().ThrowAsync<NotFoundException>();
        (await ShareService(Owner).GetLinkAsync()).IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task CreateLinkAsync_StoresOnlyAHashOfTheToken()
    {
        NewShareLinkDto link = await ShareService(Owner).CreateLinkAsync();

        using AgenderDbContext context = _db.Create(string.Empty);
        ShareLink stored = await context.ShareLinks.SingleAsync();

        stored.TokenHash.Should().NotBe(link.Token).And.NotContain(link.Token);
    }

    [Fact]
    public async Task CreateLinkAsync_WithPublicUrlProvider_ReturnsTheBaseUrlAndKeepsTheTunnelUntilExpiry()
    {
        FakePublicUrl provider = new();

        NewShareLinkDto link = await new AgendaSharingService(_db.ForUser(Owner), _time, provider).CreateLinkAsync();

        link.BaseUrl.Should().Be("https://abc.trycloudflare.com");
        link.Notice.Should().BeNull();
        provider.AcquiredUntil.Should().Be(link.ExpiresAt);
    }

    [Fact]
    public async Task CreateLinkAsync_TunnelFails_StillCreatesTheLinkWithANotice()
    {
        FakePublicUrl provider = new() { Fail = true };

        NewShareLinkDto link = await new AgendaSharingService(_db.ForUser(Owner), _time, provider).CreateLinkAsync();

        link.BaseUrl.Should().BeNull();
        link.Notice.Should().NotBeNullOrWhiteSpace();
        (await ShareService(Owner).GetLinkAsync()).IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task RevokeLinkAsync_LastActiveLink_ReleasesTheTunnel()
    {
        FakePublicUrl provider = new();
        AgendaSharingService sut = new(_db.ForUser(Owner), _time, provider);
        await sut.CreateLinkAsync();

        await sut.RevokeLinkAsync();

        provider.Released.Should().BeTrue();
    }

    [Fact]
    public async Task RevokeLinkAsync_AnotherUserStillHasALink_KeepsTheTunnel()
    {
        FakePublicUrl provider = new();
        await new AgendaSharingService(_db.ForUser(Viewer), _time, provider).CreateLinkAsync();
        AgendaSharingService owner = new(_db.ForUser(Owner), _time, provider);
        await owner.CreateLinkAsync();

        await owner.RevokeLinkAsync();

        provider.Released.Should().BeFalse();
    }

    private AgendaSharingService ShareService(string userId) => new(_db.ForUser(userId), _time);

    private PublicAgendaService PublicService() => new(new ContextFactory(_db), _time);

    private async Task<Guid> SeedSharedAgendaAsync()
    {
        IAgendaDbContextFactory owner = _db.ForUser(Owner);
        Project project = await new ProjectService(owner).CreateAsync(new Project { Name = "Cliente X" });
        await new ProjectService(owner).CreateAsync(new Project { Name = "Outro projeto" });
        await new TimeEntryService(owner).CreateAsync(new TimeEntry { Date = Day, StartMinute = 540, EndMinute = 600, Description = "Reunião", ProjectId = project.Id });
        await new DayNoteService(owner).SaveAsync(Day, "Nota do dia");

        return (await ShareService(Owner).ShareAsync("ana@example.com")).Id;
    }

    private static AgenderUser NewUser(string id, string name, string email) => new()
    {
        Id = id,
        UserName = email,
        NormalizedUserName = email.ToUpperInvariant(),
        Email = email,
        NormalizedEmail = email.ToUpperInvariant(),
        DisplayName = name
    };

    private sealed class FakePublicUrl : IPublicUrlProvider
    {
        public bool Fail { get; set; }

        public bool Released { get; private set; }

        public DateTime? AcquiredUntil { get; private set; }

        public Task<string?> AcquireAsync(DateTime untilUtc, CancellationToken cancellationToken = default)
        {
            if (Fail)
            {
                throw new InvalidOperationException("tunnel failed");
            }

            AcquiredUntil = untilUtc;
            return Task.FromResult<string?>("https://abc.trycloudflare.com");
        }

        public Task ReleaseAsync()
        {
            Released = true;
            return Task.CompletedTask;
        }
    }

    private sealed class ContextFactory(TestDb db) : IDbContextFactory<AgenderDbContext>
    {
        public AgenderDbContext CreateDbContext() => db.Create(string.Empty);
    }

    private sealed class MutableTimeProvider(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}
