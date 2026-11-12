using System.Net;
using System.Text;
using System.Text.Json;
using Agender.Infrastructure.Data;
using Agender.Infrastructure.Identity;
using Agender.Infrastructure.Services;
using Agender.Shared.Contracts;
using Agender.Shared.Domain;
using Agender.Shared.Services;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Agender.Tests.Services;

public sealed class DailyReminderTests : IDisposable
{
    private static readonly TimeProvider Time = new FixedTime(new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero));
    private static readonly DateOnly Today = new(2026, 10, 15);

    private readonly TestDb _db = new();
    private readonly IUserDbContextFactory _ana;
    private readonly EphemeralDataProtectionProvider _protection = new();
    private readonly RecordingHandler _handler = new();
    private readonly WhatsAppSender _sender;

    public DailyReminderTests()
    {
        _ana = _db.ForUser("ana");
        _sender = new WhatsAppSender(new HttpClient(_handler));

        using AgenderDbContext context = _db.Create(string.Empty);
        context.Users.Add(new AgenderUser { Id = "ana", UserName = "ana@casa.pt", NormalizedUserName = "ANA@CASA.PT", Email = "ana@casa.pt", NormalizedEmail = "ANA@CASA.PT", DisplayName = "Ana" });
        context.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    private DailyReminderService Service() => new(_ana, _sender, _protection, Time);

    private DailyReminderDispatcher Dispatcher() => new(new ContextFactory(_db), _sender, _protection, Time);

    private async Task<Guid> HouseholdAsync(bool connected = true)
    {
        HouseholdDto household = await new HouseholdService(_ana, Time).CreateAsync("Casa");

        if (connected)
        {
            await using AgenderDbContext context = _db.Create(string.Empty);
            context.MealPreferences.Add(new MealSettings
            {
                HouseholdId = household.Id,
                WhatsAppApiUrl = "http://evo.local",
                WhatsAppInstance = "agender",
                WhatsAppApiKey = _protection.CreateProtector("Agender.Meals.WhatsApp").Protect("secret")
            });
            await context.SaveChangesAsync();
        }

        return household.Id;
    }

    private async Task SeedTodayAsync(Guid householdId)
    {
        await using AgenderDbContext context = _db.Create(string.Empty);
        CleaningRoom room = new() { HouseholdId = householdId, Name = "Casa de banho", Emoji = "🛁" };
        context.CleaningRooms.Add(room);
        context.CleaningChores.Add(new CleaningChore { HouseholdId = householdId, RoomId = room.Id, Title = "Limpar sanita", Unit = CleaningUnit.Week, CreatedAt = new DateTime(2026, 10, 1) });
        context.MealPlanEntries.Add(new MealPlanEntry { HouseholdId = householdId, Date = Today, Slot = MealSlot.Dinner, Title = "Bacalhau à Brás" });
        context.MealPlanEntries.Add(new MealPlanEntry { HouseholdId = householdId, Date = Today.AddDays(1), Slot = MealSlot.Lunch, Title = "Amanhã" });
        context.InventoryItems.Add(new InventoryItem { HouseholdId = householdId, Name = "Iogurte", ExpiresOn = Today.AddDays(1) });
        context.InventoryItems.Add(new InventoryItem { HouseholdId = householdId, Name = "Arroz", ExpiresOn = Today.AddDays(90) });
        context.FamilyEvents.Add(new FamilyEvent { HouseholdId = householdId, Title = "Dentista", Start = Today.ToDateTime(new TimeOnly(18, 30)), Location = "Clínica" });
        await context.SaveChangesAsync();
    }

    private async Task AddReminderAsync(Guid householdId, DailySection section, TimeOnly sendTime, DateOnly? lastSentOn = null)
    {
        await using AgenderDbContext context = _db.Create(string.Empty);
        context.DailyReminders.Add(new DailyReminder { HouseholdId = householdId, Section = section, IsEnabled = true, Destination = "1203@g.us", SendTime = sendTime, LastSentOn = lastSentOn });
        await context.SaveChangesAsync();
    }

    private async Task<DailyReminder> ReminderAsync(DailySection section)
    {
        await using AgenderDbContext context = _db.Create(string.Empty);
        return await context.DailyReminders.AsNoTracking().SingleAsync(r => r.Section == section);
    }

    [Fact]
    public async Task GetDigestAsync_ThingsForToday_ReturnsOnlyTodaysLinesPerSection()
    {
        Guid householdId = await HouseholdAsync();
        await SeedTodayAsync(householdId);

        DailyDigestDto digest = await Service().GetDigestAsync();

        digest.Date.Should().Be(Today);
        digest.Sections.Select(s => s.Section).Should().Equal(DailySection.Cleaning, DailySection.Meals, DailySection.Events, DailySection.Inventory);
        digest.Sections[0].Lines.Should().Equal("🛁 Casa de banho: Limpar sanita (atrasada)");
        digest.Sections[1].Lines.Should().ContainSingle().Which.Should().EndWith("Jantar: Bacalhau à Brás");
        digest.Sections[2].Lines.Should().Equal("18:30 Dentista · Clínica");
        digest.Sections[3].Lines.Should().Equal("Iogurte — expira amanhã");
    }

    [Fact]
    public async Task GetDigestAsync_NothingForToday_ReturnsNoSections()
    {
        await HouseholdAsync();

        (await Service().GetDigestAsync()).Sections.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveAsync_EnabledWithoutDestination_Throws()
    {
        await HouseholdAsync();

        Func<Task> act = () => Service().SaveAsync([new DailyReminderDto { Section = DailySection.Meals, IsEnabled = true }]);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SaveAsync_EnabledWithoutWhatsAppConnection_Throws()
    {
        await HouseholdAsync(connected: false);

        Func<Task> act = () => Service().SaveAsync([new DailyReminderDto { Section = DailySection.Meals, IsEnabled = true, Destination = "1203@g.us" }]);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SaveAsync_EnabledAfterTodaysTime_StartsTomorrow()
    {
        await HouseholdAsync();

        DailyRemindersDto saved = await Service().SaveAsync(
        [
            new DailyReminderDto { Section = DailySection.Meals, IsEnabled = true, Destination = "1203@g.us", SendTime = new TimeOnly(8, 0) },
            new DailyReminderDto { Section = DailySection.Cleaning, IsEnabled = true, Destination = "+351 912 345 678", SendTime = new TimeOnly(19, 0) }
        ]);

        saved.WhatsAppReady.Should().BeTrue();
        saved.Reminders.Should().HaveCount(DailyCatalog.Sections.Length);
        saved.Reminders.Single(r => r.Section == DailySection.Meals).LastSentOn.Should().Be(Today);
        saved.Reminders.Single(r => r.Section == DailySection.Cleaning).LastSentOn.Should().BeNull();
        saved.Reminders.Single(r => r.Section == DailySection.Cleaning).Destination.Should().Be("351912345678");
    }

    [Fact]
    public async Task DispatchDueAsync_DueReminder_SendsOnceToItsGroup()
    {
        Guid householdId = await HouseholdAsync();
        await SeedTodayAsync(householdId);
        await AddReminderAsync(householdId, DailySection.Meals, new TimeOnly(8, 0));

        await Dispatcher().DispatchDueAsync(CancellationToken.None);
        await Dispatcher().DispatchDueAsync(CancellationToken.None);

        _handler.Requests.Should().ContainSingle();
        _handler.Requests[0].Url.Should().Be("http://evo.local/message/sendText/agender");
        using JsonDocument body = JsonDocument.Parse(_handler.Requests[0].Body);
        body.RootElement.GetProperty("number").GetString().Should().Be("1203@g.us");
        body.RootElement.GetProperty("text").GetString().Should().StartWith("🍽️ *Refeições de hoje*").And.Contain("Jantar: Bacalhau à Brás");
        (await ReminderAsync(DailySection.Meals)).LastSentOn.Should().Be(Today);
    }

    [Fact]
    public async Task DispatchDueAsync_BeforeSendTime_DoesNothing()
    {
        Guid householdId = await HouseholdAsync();
        await SeedTodayAsync(householdId);
        await AddReminderAsync(householdId, DailySection.Meals, new TimeOnly(19, 0));

        await Dispatcher().DispatchDueAsync(CancellationToken.None);

        _handler.Requests.Should().BeEmpty();
        (await ReminderAsync(DailySection.Meals)).LastSentOn.Should().BeNull();
    }

    [Fact]
    public async Task DispatchDueAsync_NothingForToday_MarksTheDayWithoutSending()
    {
        Guid householdId = await HouseholdAsync();
        await AddReminderAsync(householdId, DailySection.Cleaning, new TimeOnly(8, 0), Today.AddDays(-1));

        await Dispatcher().DispatchDueAsync(CancellationToken.None);

        _handler.Requests.Should().BeEmpty();
        (await ReminderAsync(DailySection.Cleaning)).LastSentOn.Should().Be(Today);
    }

    [Fact]
    public async Task DispatchDueAsync_ApiRejects_KeepsTheErrorAndRetriesLater()
    {
        Guid householdId = await HouseholdAsync();
        await SeedTodayAsync(householdId);
        await AddReminderAsync(householdId, DailySection.Events, new TimeOnly(8, 0));
        _handler.Status = HttpStatusCode.BadRequest;

        await Dispatcher().DispatchDueAsync(CancellationToken.None);
        await Dispatcher().DispatchDueAsync(CancellationToken.None);

        _handler.Requests.Should().ContainSingle();
        DailyReminder reminder = await ReminderAsync(DailySection.Events);
        reminder.LastSentOn.Should().BeNull();
        reminder.LastError.Should().Contain("400");
    }

    [Fact]
    public async Task FetchGroupsAsync_EvolutionGroups_ReturnsNamedGroupsFirstSortedByName()
    {
        _handler.ResponseBody = """[{"id":"2@g.us","subject":"Limpezas"},{"id":"1@g.us","subject":"Casa"},{"id":"3@g.us","subject":null}]""";

        List<WhatsAppGroupDto> groups = await _sender.FetchGroupsAsync("http://evo.local/", "agender", "secret", CancellationToken.None);

        groups.Should().Equal(new WhatsAppGroupDto("1@g.us", "Casa"), new WhatsAppGroupDto("2@g.us", "Limpezas"), new WhatsAppGroupDto("3@g.us", "3@g.us"));
        _handler.Requests[0].Url.Should().Be("http://evo.local/group/fetchAllGroups/agender?getParticipants=false");
    }

    private sealed class ContextFactory(TestDb db) : IDbContextFactory<AgenderDbContext>
    {
        public AgenderDbContext CreateDbContext() => db.Create(string.Empty);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<(string Url, string Body)> Requests { get; } = [];

        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

        public string ResponseBody { get; set; } = "{}";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add((request.RequestUri!.ToString(), body));

            return new HttpResponseMessage(Status) { Content = new StringContent(ResponseBody, Encoding.UTF8, "application/json") };
        }
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
