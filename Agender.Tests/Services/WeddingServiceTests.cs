using Agender.Infrastructure.Data;
using Agender.Infrastructure.Identity;
using Agender.Infrastructure.Services;
using Agender.Shared.Contracts;
using Agender.Shared.Domain;
using Agender.Shared.Services;
using FluentAssertions;

namespace Agender.Tests.Services;

public sealed class WeddingServiceTests : IDisposable
{
    private static readonly TimeProvider Time = TimeProvider.System;

    private readonly TestDb _db = new();
    private readonly IUserDbContextFactory _ana;
    private readonly IUserDbContextFactory _rui;
    private readonly IUserDbContextFactory _eva;

    public WeddingServiceTests()
    {
        _ana = _db.ForUser("ana");
        _rui = _db.ForUser("rui");
        _eva = _db.ForUser("eva");

        using AgenderDbContext context = _db.Create(string.Empty);
        context.Users.AddRange(User("ana", "ana@casa.pt", "Ana"), User("rui", "rui@casa.pt", "Rui"), User("eva", "eva@fora.pt", "Eva"));
        context.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    private static AgenderUser User(string id, string email, string name) => new()
    {
        Id = id,
        UserName = email,
        NormalizedUserName = email.ToUpperInvariant(),
        Email = email,
        NormalizedEmail = email.ToUpperInvariant(),
        DisplayName = name
    };

    private async Task<WeddingService> CoupleAsync()
    {
        HouseholdDto household = await new HouseholdService(_ana, Time).CreateAsync("Casa");
        await new HouseholdService(_rui, Time).JoinAsync(household.InviteCode);

        return new WeddingService(_ana);
    }

    [Fact]
    public async Task GetAsync_NewHousehold_StartsEmptyWithoutADate()
    {
        await new HouseholdService(_ana, Time).CreateAsync("Casa");

        WeddingOverview overview = await new WeddingService(_ana).GetAsync();

        overview.Settings.Date.Should().BeNull();
        overview.Guests.Should().BeEmpty();
        overview.Categories.Should().BeEmpty();
        overview.Vendors.Should().BeEmpty();
        overview.Tasks.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAsync_UserWithoutHousehold_Throws()
    {
        Func<Task> act = () => new WeddingService(_ana).GetAsync();

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SaveSettingsAsync_TheDate_IsSeenByTheOtherMember()
    {
        WeddingService ana = await CoupleAsync();

        await ana.SaveSettingsAsync(new WeddingSettings { Date = new DateOnly(2027, 6, 12), Venue = " Quinta do Lago " });
        await ana.SaveSettingsAsync(new WeddingSettings { Date = new DateOnly(2027, 7, 3), Venue = "Quinta do Lago" });

        WeddingOverview seen = await new WeddingService(_rui).GetAsync();
        seen.Settings.Date.Should().Be(new DateOnly(2027, 7, 3));
        seen.Settings.Venue.Should().Be("Quinta do Lago");

        CasaDashboard dashboard = await new HouseholdService(_rui, Time).GetDashboardAsync();
        dashboard.WeddingDate.Should().Be(new DateOnly(2027, 7, 3));
    }

    [Fact]
    public async Task SaveGuestAsync_CreatesThenUpdates_AndCountsThePlusOnes()
    {
        WeddingService service = await CoupleAsync();
        WeddingGuest guest = await service.SaveGuestAsync(new WeddingGuest { Name = "  Marta  ", Group = "Amigos", PlusOnes = 1 });

        guest.Status = GuestStatus.Confirmed;
        guest.Diet = "Vegetariana";
        await new WeddingService(_rui).SaveGuestAsync(guest);

        WeddingGuest stored = (await service.GetAsync()).Guests.Single();
        stored.Name.Should().Be("Marta");
        stored.Status.Should().Be(GuestStatus.Confirmed);
        stored.Diet.Should().Be("Vegetariana");
        stored.People.Should().Be(2);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(21)]
    public async Task SaveGuestAsync_PlusOnesOutOfRange_Throws(int plusOnes)
    {
        WeddingService service = await CoupleAsync();

        Func<Task> act = () => service.SaveGuestAsync(new WeddingGuest { Name = "Marta", PlusOnes = plusOnes });

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SaveGuestAsync_WithoutAName_Throws()
    {
        WeddingService service = await CoupleAsync();

        Func<Task> act = () => service.SaveGuestAsync(new WeddingGuest { Name = "  " });

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task DeleteGuestAsync_Unknown_ThrowsNotFound()
    {
        WeddingService service = await CoupleAsync();

        Func<Task> act = () => service.DeleteGuestAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Budget_CategoryWithPayments_TotalsPaidAndRemaining()
    {
        WeddingService service = await CoupleAsync();
        WeddingCategory catering = await service.SaveCategoryAsync(new WeddingCategory { Name = "Catering", Planned = 5000, Cost = 4800 });

        await service.SavePaymentAsync(new WeddingPayment { CategoryId = catering.Id, Amount = 1000, PaidBy = "Ana", Date = new DateOnly(2026, 11, 1) });
        await service.SavePaymentAsync(new WeddingPayment { CategoryId = catering.Id, Amount = 800, PaidBy = "Rui", Date = new DateOnly(2026, 12, 1) });

        WeddingCategory stored = (await service.GetAsync()).Categories.Single();
        stored.Paid.Should().Be(1800);
        stored.Remaining.Should().Be(3000);
        stored.Payments.Select(p => p.Amount).Should().Equal(800, 1000);
    }

    [Fact]
    public async Task Budget_PaymentWithoutAmountOrOfAnotherHousehold_Throws()
    {
        WeddingService ana = await CoupleAsync();
        WeddingCategory category = await ana.SaveCategoryAsync(new WeddingCategory { Name = "Flores" });
        await new HouseholdService(_eva, Time).CreateAsync("Outra");

        Func<Task> zero = () => ana.SavePaymentAsync(new WeddingPayment { CategoryId = category.Id, Amount = 0 });
        Func<Task> foreign = () => new WeddingService(_eva).SavePaymentAsync(new WeddingPayment { CategoryId = category.Id, Amount = 10 });

        await zero.Should().ThrowAsync<ArgumentException>();
        await foreign.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task DeleteCategoryAsync_RemovesItsPaymentsAndDetachesTheVendors()
    {
        WeddingService service = await CoupleAsync();
        WeddingCategory category = await service.SaveCategoryAsync(new WeddingCategory { Name = "Fotografia", Cost = 900 });
        await service.SavePaymentAsync(new WeddingPayment { CategoryId = category.Id, Amount = 100 });
        await service.SaveVendorAsync(new WeddingVendor { Name = "Estúdio X", CategoryId = category.Id });

        await service.DeleteCategoryAsync(category.Id);

        WeddingOverview overview = await service.GetAsync();
        overview.Categories.Should().BeEmpty();
        overview.Vendors.Single().CategoryId.Should().BeNull();
        using AgenderDbContext context = _db.Create(string.Empty);
        context.WeddingPayments.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveVendorAsync_UnknownCategory_Throws()
    {
        WeddingService service = await CoupleAsync();

        Func<Task> act = () => service.SaveVendorAsync(new WeddingVendor { Name = "Estúdio X", CategoryId = Guid.NewGuid() });

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SaveVendorAsync_Status_IsKept()
    {
        WeddingService service = await CoupleAsync();

        await service.SaveVendorAsync(new WeddingVendor { Name = "DJ Y", Kind = "Música / DJ", Quote = 700, Status = VendorStatus.Booked });

        WeddingVendor stored = (await service.GetAsync()).Vendors.Single();
        stored.Status.Should().Be(VendorStatus.Booked);
        stored.Quote.Should().Be(700);
    }

    [Fact]
    public async Task SaveTaskAsync_AssigneeMustBeAMember()
    {
        WeddingService service = await CoupleAsync();

        WeddingTask mine = await service.SaveTaskAsync(new WeddingTask { Title = "Escolher espaço", AssigneeUserId = "rui" });
        Func<Task> outsider = () => service.SaveTaskAsync(new WeddingTask { Title = "Outra", AssigneeUserId = "eva" });

        mine.AssigneeUserId.Should().Be("rui");
        await outsider.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetAsync_Tasks_ListPendingFirstByDueDate()
    {
        WeddingService service = await CoupleAsync();
        await service.SaveTaskAsync(new WeddingTask { Title = "Feita", Done = true, DueDate = new DateOnly(2026, 1, 1) });
        await service.SaveTaskAsync(new WeddingTask { Title = "Depois", DueDate = new DateOnly(2026, 12, 1) });
        await service.SaveTaskAsync(new WeddingTask { Title = "Sem data" });
        await service.SaveTaskAsync(new WeddingTask { Title = "Antes", DueDate = new DateOnly(2026, 11, 1) });

        (await service.GetAsync()).Tasks.Select(t => t.Title).Should().Equal("Antes", "Depois", "Sem data", "Feita");
    }

    [Fact]
    public async Task AddSuggestedTasksAsync_WithADate_DatesThemAndDoesNotDuplicate()
    {
        WeddingService service = await CoupleAsync();
        await service.SaveSettingsAsync(new WeddingSettings { Date = new DateOnly(2027, 6, 12) });

        int first = await service.AddSuggestedTasksAsync();
        int second = await service.AddSuggestedTasksAsync();

        first.Should().Be(WeddingCatalog.SuggestedTasks.Length);
        second.Should().Be(0);
        WeddingTask spaceTask = (await service.GetAsync()).Tasks.Single(t => t.Title == "Escolher e reservar o espaço");
        spaceTask.DueDate.Should().Be(new DateOnly(2026, 6, 12));
    }

    [Fact]
    public async Task AddSuggestedTasksAsync_WithoutADate_LeavesThemUndated()
    {
        WeddingService service = await CoupleAsync();

        await service.AddSuggestedTasksAsync();

        (await service.GetAsync()).Tasks.Should().OnlyContain(t => t.DueDate == null);
    }

    [Fact]
    public async Task AddSuggestedCategoriesAsync_AddsOnlyTheMissingOnes()
    {
        WeddingService service = await CoupleAsync();
        await service.SaveCategoryAsync(new WeddingCategory { Name = "catering" });

        int added = await service.AddSuggestedCategoriesAsync();

        added.Should().Be(WeddingCatalog.SuggestedCategories.Length - 1);
        (await service.GetAsync()).Categories.Should().HaveCount(WeddingCatalog.SuggestedCategories.Length);
    }

    [Fact]
    public async Task Wedding_DataOfAnotherHousehold_IsNotVisibleOrEditable()
    {
        WeddingService ana = await CoupleAsync();
        await new HouseholdService(_eva, Time).CreateAsync("Outra");
        WeddingGuest guest = await ana.SaveGuestAsync(new WeddingGuest { Name = "Só da casa da Ana" });
        WeddingService eva = new(_eva);

        (await eva.GetAsync()).Guests.Should().BeEmpty();
        Func<Task> delete = () => eva.DeleteGuestAsync(guest.Id);
        await delete.Should().ThrowAsync<NotFoundException>();

        guest.Name = "Alterada pela Eva";
        Func<Task> save = () => eva.SaveGuestAsync(guest);
        await save.Should().ThrowAsync<NotFoundException>();

        (await ana.GetAsync()).Guests.Single().Name.Should().Be("Só da casa da Ana");
    }

    [Fact]
    public async Task LeaveAsync_LastMember_DeletesTheWeddingData()
    {
        await new HouseholdService(_ana, Time).CreateAsync("Casa");
        WeddingService service = new(_ana);
        await service.SaveSettingsAsync(new WeddingSettings { Date = new DateOnly(2027, 6, 12) });
        await service.SaveGuestAsync(new WeddingGuest { Name = "Marta" });
        WeddingCategory category = await service.SaveCategoryAsync(new WeddingCategory { Name = "Espaço" });
        await service.SavePaymentAsync(new WeddingPayment { CategoryId = category.Id, Amount = 50 });
        await service.SaveVendorAsync(new WeddingVendor { Name = "Quinta" });
        await service.SaveTaskAsync(new WeddingTask { Title = "Reservar" });

        await new HouseholdService(_ana, Time).LeaveAsync();

        using AgenderDbContext context = _db.Create(string.Empty);
        context.WeddingDetails.Should().BeEmpty();
        context.WeddingGuests.Should().BeEmpty();
        context.WeddingCategories.Should().BeEmpty();
        context.WeddingPayments.Should().BeEmpty();
        context.WeddingVendors.Should().BeEmpty();
        context.WeddingTasks.Should().BeEmpty();
    }
}
