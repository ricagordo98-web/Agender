using Agender.Infrastructure.Data;
using Agender.Infrastructure.Identity;
using Agender.Infrastructure.Services;
using Agender.Shared.Contracts;
using Agender.Shared.Domain;
using Agender.Shared.Services;
using FluentAssertions;

namespace Agender.Tests.Services;

public sealed class CasaServiceTests : IDisposable
{
    private static readonly TimeProvider Time = new FixedTime(new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero));

    private readonly TestDb _db = new();
    private readonly IUserDbContextFactory _ana;
    private readonly IUserDbContextFactory _rui;
    private readonly IUserDbContextFactory _eva;

    public CasaServiceTests()
    {
        _ana = _db.ForUser("ana");
        _rui = _db.ForUser("rui");
        _eva = _db.ForUser("eva");

        using AgenderDbContext context = _db.Create(string.Empty);
        context.Users.AddRange(User("ana", "ana@casa.pt", "Ana Silva"), User("rui", "rui@casa.pt", "Rui Costa"), User("eva", "eva@fora.pt", "Eva Lopes"));
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

    private HouseholdService Households(IUserDbContextFactory factory) => new(factory, Time);

    private async Task<HouseholdDto> CoupleAsync()
    {
        HouseholdDto household = await Households(_ana).CreateAsync("Casa");
        await Households(_rui).JoinAsync(household.InviteCode);

        return household;
    }

    [Fact]
    public async Task GetAsync_UserWithoutHousehold_ReturnsNull()
    {
        (await Households(_ana).GetAsync()).Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_NewHousehold_MakesTheCreatorTheFirstMember()
    {
        HouseholdDto household = await Households(_ana).CreateAsync("  Casa da Ana  ");

        household.Name.Should().Be("Casa da Ana");
        household.InviteCode.Should().HaveLength(Household.InviteCodeLength);
        household.Members.Should().ContainSingle().Which.DisplayName.Should().Be("Ana Silva");
    }

    [Fact]
    public async Task CreateAsync_BlankName_UsesTheDefaultName()
    {
        (await Households(_ana).CreateAsync(" ")).Name.Should().Be(Household.DefaultName);
    }

    [Fact]
    public async Task CreateAsync_UserAlreadyInAHousehold_Throws()
    {
        await Households(_ana).CreateAsync("Casa");

        Func<Task> act = () => Households(_ana).CreateAsync("Outra");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task JoinAsync_ValidCode_AddsTheMemberWithADifferentColor()
    {
        HouseholdDto household = await CoupleAsync();

        HouseholdDto joined = (await Households(_rui).GetAsync())!;

        joined.Id.Should().Be(household.Id);
        joined.Members.Should().HaveCount(2);
        joined.Members.Select(m => m.Color).Distinct().Should().HaveCount(2);
    }

    [Fact]
    public async Task JoinAsync_LowercaseCode_Works()
    {
        HouseholdDto household = await Households(_ana).CreateAsync("Casa");

        HouseholdDto joined = await Households(_rui).JoinAsync(household.InviteCode.ToLowerInvariant());

        joined.Members.Should().HaveCount(2);
    }

    [Fact]
    public async Task JoinAsync_UnknownCode_ThrowsNotFound()
    {
        Func<Task> act = () => Households(_rui).JoinAsync("ZZZZZZ");

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task NewInviteCodeAsync_InvalidatesThePreviousCode()
    {
        HouseholdDto household = await Households(_ana).CreateAsync("Casa");

        HouseholdDto renewed = await Households(_ana).NewInviteCodeAsync();

        renewed.InviteCode.Should().NotBe(household.InviteCode);
        Func<Task> act = () => Households(_rui).JoinAsync(household.InviteCode);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task SetMyColorAsync_StoresTheColor()
    {
        await Households(_ana).CreateAsync("Casa");

        HouseholdDto updated = await Households(_ana).SetMyColorAsync("#abcdef");

        updated.Members.Single().Color.Should().Be("#ABCDEF");
    }

    [Fact]
    public async Task InviteAsync_RegisteredPersonVisits_JoinsWithoutACode()
    {
        await Households(_ana).CreateAsync("Casa");

        HouseholdDto invited = await Households(_ana).InviteAsync("  EVA@fora.pt ");
        HouseholdDto? joined = await Households(_eva).GetAsync();

        invited.Invites.Should().ContainSingle().Which.Email.Should().Be("eva@fora.pt");
        joined.Should().NotBeNull();
        joined!.Members.Should().HaveCount(2);
        (await Households(_ana).GetAsync())!.Invites.Should().BeEmpty();
    }

    [Fact]
    public async Task InviteAsync_ThePersonThenSeesTheHouseholdData()
    {
        await Households(_ana).CreateAsync("Casa");
        await new ShoppingService(_ana).CreateListAsync(new ShoppingList { Name = "Semana" });
        await Households(_ana).InviteAsync("eva@fora.pt");

        (await Households(_eva).GetAsync()).Should().NotBeNull();

        (await new ShoppingService(_eva).GetListsAsync()).Should().ContainSingle();
    }

    [Fact]
    public async Task InviteAsync_ExpiredInvite_DoesNotAdmitAnyone()
    {
        await Households(_ana).CreateAsync("Casa");
        await Households(_ana).InviteAsync("eva@fora.pt");
        HouseholdService later = new(_eva, new FixedTime(new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero).AddDays(15)));

        (await later.GetAsync()).Should().BeNull();
    }

    [Fact]
    public async Task InviteAsync_CancelledInvite_DoesNotAdmitAnyone()
    {
        HouseholdDto household = await Households(_ana).CreateAsync("Casa");
        HouseholdDto invited = await Households(_ana).InviteAsync("eva@fora.pt");

        HouseholdDto after = await Households(_ana).CancelInviteAsync(invited.Invites!.Single().Id);

        after.Invites.Should().BeEmpty();
        (await Households(_eva).GetAsync()).Should().BeNull();
        household.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task InviteAsync_SameEmailTwice_KeepsASingleInvite()
    {
        await Households(_ana).CreateAsync("Casa");

        await Households(_ana).InviteAsync("eva@fora.pt");
        HouseholdDto again = await Households(_ana).InviteAsync("EVA@fora.pt");

        again.Invites.Should().ContainSingle();
    }

    [Fact]
    public async Task InviteAsync_ExistingMember_Throws()
    {
        await CoupleAsync();

        Func<Task> act = () => Households(_ana).InviteAsync("rui@casa.pt");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task InviteAsync_InvalidEmail_Throws()
    {
        await Households(_ana).CreateAsync("Casa");

        Func<Task> act = () => Households(_ana).InviteAsync("sem-arroba");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task InviteAsync_WithoutAHousehold_Throws()
    {
        Func<Task> act = () => Households(_ana).InviteAsync("eva@fora.pt");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task JoinAsync_WithTheCode_ClearsThePendingInviteForThatEmail()
    {
        HouseholdDto household = await Households(_ana).CreateAsync("Casa");
        await Households(_ana).InviteAsync("rui@casa.pt");

        await Households(_rui).JoinAsync(household.InviteCode);

        (await Households(_ana).GetAsync())!.Invites.Should().BeEmpty();
    }

    [Fact]
    public async Task CancelInviteAsync_InviteOfAnotherHousehold_IsIgnored()
    {
        await Households(_ana).CreateAsync("Casa A");
        await Households(_eva).CreateAsync("Casa E");
        HouseholdDto invited = await Households(_ana).InviteAsync("rui@casa.pt");

        await Households(_eva).CancelInviteAsync(invited.Invites!.Single().Id);

        (await Households(_ana).GetAsync())!.Invites.Should().ContainSingle();
    }

    [Fact]
    public async Task LeaveAsync_LastMember_DeletesTheHouseholdAndItsData()
    {
        await Households(_ana).CreateAsync("Casa");
        await new ShoppingService(_ana).CreateListAsync(new ShoppingList { Name = "Semana" });
        await new HomeProjectService(_ana).CreateAsync(new HomeProject { Title = "Obra" });
        await new FinanceService(_ana).CreateTransactionAsync(new FinanceTransaction { Type = TransactionType.Income, Amount = 10, Date = new DateOnly(2026, 10, 1) });
        await new FamilyEventService(_ana).CreateAsync(new FamilyEvent { Title = "Evento", Start = new DateTime(2026, 10, 5, 9, 0, 0) });

        await Households(_ana).LeaveAsync();

        using AgenderDbContext context = _db.Create(string.Empty);
        context.Households.Should().BeEmpty();
        context.ShoppingLists.Should().BeEmpty();
        context.HomeProjects.Should().BeEmpty();
        context.FinanceTransactions.Should().BeEmpty();
        context.FamilyEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task LeaveAsync_OtherMembersRemain_KeepsTheData()
    {
        await CoupleAsync();
        await new ShoppingService(_ana).CreateListAsync(new ShoppingList { Name = "Semana" });

        await Households(_rui).LeaveAsync();

        (await new ShoppingService(_ana).GetListsAsync()).Should().ContainSingle();
        (await Households(_rui).GetAsync()).Should().BeNull();
    }

    [Fact]
    public async Task Services_UserWithoutHousehold_AreRejected()
    {
        Func<Task> act = () => new ShoppingService(_ana).GetListsAsync();

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CreateAsync_NewHousehold_StartsEmpty()
    {
        await Households(_ana).CreateAsync("Casa");

        (await new ShoppingService(_ana).GetListsAsync()).Should().BeEmpty();
        (await new HomeProjectService(_ana).GetAllAsync()).Should().BeEmpty();
        (await new FinanceService(_ana).GetGoalsAsync()).Should().BeEmpty();
        (await new FinanceService(_ana).GetSummaryAsync(2026, 10)).Income.Should().Be(0);
        (await new FamilyEventService(_ana).GetAsync(new DateTime(2026, 10, 1), new DateTime(2026, 12, 1))).Should().BeEmpty();
    }

    [Fact]
    public async Task Data_OfAnotherHousehold_IsNotVisible()
    {
        await Households(_ana).CreateAsync("Casa A");
        await Households(_eva).CreateAsync("Casa E");
        await new HomeProjectService(_ana).CreateAsync(new HomeProject { Title = "Só da A" });

        (await new HomeProjectService(_eva).GetAllAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task GetDashboardAsync_AggregatesTheHousehold()
    {
        await Households(_ana).CreateAsync("Casa");
        ShoppingList list = await new ShoppingService(_ana).CreateListAsync(new ShoppingList { Name = "Semana" });
        await new ShoppingService(_ana).AddItemAsync(list.Id, new ShoppingItem { Name = "Leite" });
        await new HomeProjectService(_ana).CreateAsync(new HomeProject { Title = "Obra", Status = HomeStatus.Planned });
        await new FinanceService(_ana).CreateGoalAsync(new SavingsGoal { Name = "Férias", Target = 1000 });

        CasaDashboard dashboard = await Households(_ana).GetDashboardAsync();

        dashboard.Lists.Should().ContainSingle();
        dashboard.PendingShoppingItems.Should().Be(1);
        dashboard.OpenProjects.Should().Be(1);
        dashboard.TopProjects.Should().ContainSingle();
        dashboard.Goals.Should().ContainSingle();
    }

    [Fact]
    public async Task Shopping_MemberOfTheHousehold_SeesAndEditsTheList()
    {
        await CoupleAsync();
        ShoppingList list = await new ShoppingService(_ana).CreateListAsync(new ShoppingList { Name = "Semana" });

        ShoppingItem item = await new ShoppingService(_rui).AddItemAsync(list.Id, new ShoppingItem { Name = "Leite" });

        ShoppingList loaded = await new ShoppingService(_ana).GetListAsync(list.Id);
        loaded.Items.Should().ContainSingle().Which.AddedBy.Should().Be("Rui Costa");
        item.Category.Should().Be("Laticínios e Ovos");
    }

    [Fact]
    public async Task Shopping_ListOfAnotherHouseholdWithoutShare_IsNotFound()
    {
        await Households(_ana).CreateAsync("Casa A");
        await Households(_eva).CreateAsync("Casa E");
        ShoppingList list = await new ShoppingService(_ana).CreateListAsync(new ShoppingList { Name = "Privada" });

        Func<Task> act = () => new ShoppingService(_eva).GetListAsync(list.Id);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Shopping_SharedByEmailWithEdit_AppearsAndCanBeEdited()
    {
        await Households(_ana).CreateAsync("Casa A");
        await Households(_eva).CreateAsync("Casa E");
        ShoppingList list = await new ShoppingService(_ana).CreateListAsync(new ShoppingList { Name = "Festa" });
        await new ShoppingService(_ana).ShareAsync(list.Id, " EVA@fora.pt ", canEdit: true);

        IReadOnlyList<ShoppingList> visible = await new ShoppingService(_eva).GetListsAsync();
        ShoppingItem added = await new ShoppingService(_eva).AddItemAsync(list.Id, new ShoppingItem { Name = "Vinho" });

        visible.Should().ContainSingle().Which.IsSharedWithMe.Should().BeTrue();
        visible[0].OwnerHouseholdName.Should().Be("Casa A");
        added.Name.Should().Be("Vinho");
    }

    [Fact]
    public async Task Shopping_SharedReadOnly_CannotEdit()
    {
        await Households(_ana).CreateAsync("Casa A");
        await Households(_eva).CreateAsync("Casa E");
        ShoppingList list = await new ShoppingService(_ana).CreateListAsync(new ShoppingList { Name = "Festa" });
        await new ShoppingService(_ana).ShareAsync(list.Id, "eva@fora.pt", canEdit: false);

        Func<Task> act = () => new ShoppingService(_eva).AddItemAsync(list.Id, new ShoppingItem { Name = "Vinho" });

        await act.Should().ThrowAsync<NotFoundException>();
        (await new ShoppingService(_eva).GetListAsync(list.Id)).CanEdit.Should().BeFalse();
    }

    [Fact]
    public async Task Shopping_GuestLeavesASharedList_LosesAccess()
    {
        await Households(_ana).CreateAsync("Casa A");
        await Households(_eva).CreateAsync("Casa E");
        ShoppingList list = await new ShoppingService(_ana).CreateListAsync(new ShoppingList { Name = "Festa" });
        ListShare share = await new ShoppingService(_ana).ShareAsync(list.Id, "eva@fora.pt", true);

        await new ShoppingService(_eva).UnshareAsync(list.Id, share.Id);

        (await new ShoppingService(_eva).GetListsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Shopping_GuestCannotRemoveSomeoneElsesShare()
    {
        await Households(_ana).CreateAsync("Casa A");
        await Households(_eva).CreateAsync("Casa E");
        ShoppingList list = await new ShoppingService(_ana).CreateListAsync(new ShoppingList { Name = "Festa" });
        await new ShoppingService(_ana).ShareAsync(list.Id, "eva@fora.pt", true);
        ListShare other = await new ShoppingService(_ana).ShareAsync(list.Id, "outro@fora.pt", true);

        Func<Task> act = () => new ShoppingService(_eva).UnshareAsync(list.Id, other.Id);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Shopping_ShareWithInvalidEmail_Throws()
    {
        await Households(_ana).CreateAsync("Casa A");
        ShoppingList list = await new ShoppingService(_ana).CreateListAsync(new ShoppingList { Name = "Festa" });

        Func<Task> act = () => new ShoppingService(_ana).ShareAsync(list.Id, "sem-arroba", true);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Shopping_OnlyTheOwnerHouseholdCanDeleteTheList()
    {
        await Households(_ana).CreateAsync("Casa A");
        await Households(_eva).CreateAsync("Casa E");
        ShoppingList list = await new ShoppingService(_ana).CreateListAsync(new ShoppingList { Name = "Festa" });
        await new ShoppingService(_ana).ShareAsync(list.Id, "eva@fora.pt", true);

        Func<Task> act = () => new ShoppingService(_eva).DeleteListAsync(list.Id);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Shopping_GetLists_CountsPendingAndLimitsThePreview()
    {
        await Households(_ana).CreateAsync("Casa");
        ShoppingService service = new(_ana);
        ShoppingList list = await service.CreateListAsync(new ShoppingList { Name = "Semana" });

        foreach (string name in new[] { "Leite", "Pão", "Ovos", "Arroz", "Sal", "Café" })
        {
            await service.AddItemAsync(list.Id, new ShoppingItem { Name = name });
        }

        ShoppingItem first = (await service.GetListAsync(list.Id)).Items.First();
        first.IsChecked = true;
        await service.UpdateItemAsync(first);

        ShoppingList summary = (await service.GetListsAsync()).Single();

        summary.TotalCount.Should().Be(6);
        summary.PendingCount.Should().Be(5);
        summary.Items.Should().HaveCount(4);
    }

    [Fact]
    public async Task Shopping_ClearChecked_RemovesOnlyTheCheckedItems()
    {
        await Households(_ana).CreateAsync("Casa");
        ShoppingService service = new(_ana);
        ShoppingList list = await service.CreateListAsync(new ShoppingList { Name = "Semana" });
        await service.AddItemAsync(list.Id, new ShoppingItem { Name = "Leite" });
        await service.AddItemAsync(list.Id, new ShoppingItem { Name = "Pão" });
        ShoppingItem leite = (await service.GetListAsync(list.Id)).Items.Single(i => i.Name == "Leite");
        leite.IsChecked = true;
        await service.UpdateItemAsync(leite);

        await service.ClearCheckedAsync(list.Id);

        (await service.GetListAsync(list.Id)).Items.Should().ContainSingle().Which.Name.Should().Be("Pão");
    }

    [Fact]
    public async Task Shopping_AddItem_BlankNameThrows()
    {
        await Households(_ana).CreateAsync("Casa");
        ShoppingList list = await new ShoppingService(_ana).CreateListAsync(new ShoppingList { Name = "Semana" });

        Func<Task> act = () => new ShoppingService(_ana).AddItemAsync(list.Id, new ShoppingItem { Name = "  " });

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Shopping_AddItem_NonPositiveQuantityBecomesOne()
    {
        await Households(_ana).CreateAsync("Casa");
        ShoppingList list = await new ShoppingService(_ana).CreateListAsync(new ShoppingList { Name = "Semana" });

        ShoppingItem item = await new ShoppingService(_ana).AddItemAsync(list.Id, new ShoppingItem { Name = "Sal", Quantity = -3 });

        item.Quantity.Should().Be(1);
    }

    [Fact]
    public async Task HomeProjects_CreateAndUpdate_ReplacesTheTasksAndKeepsOrder()
    {
        await Households(_ana).CreateAsync("Casa");
        HomeProjectService service = new(_ana);
        HomeProject created = await service.CreateAsync(new HomeProject
        {
            Title = "Obra",
            Tasks = [new HomeTask { Title = "A" }, new HomeTask { Title = "B", Done = true }]
        });

        await service.UpdateAsync(new HomeProject
        {
            Id = created.Id,
            Title = "Obra 2",
            Status = HomeStatus.InProgress,
            Tasks = [new HomeTask { Title = "C" }, new HomeTask { Title = "A", Done = true }]
        });

        HomeProject stored = (await service.GetAllAsync()).Single();
        stored.Title.Should().Be("Obra 2");
        stored.Status.Should().Be(HomeStatus.InProgress);
        stored.Tasks.Select(t => t.Title).Should().Equal("C", "A");
        stored.Tasks.Select(t => t.Order).Should().Equal(0, 1);
    }

    [Fact]
    public async Task HomeProjects_GetAll_OrdersByPriorityThenDueDate()
    {
        await Households(_ana).CreateAsync("Casa");
        HomeProjectService service = new(_ana);
        await service.CreateAsync(new HomeProject { Title = "Baixa", Priority = HomePriority.Low });
        await service.CreateAsync(new HomeProject { Title = "Urgente tarde", Priority = HomePriority.Urgent, DueDate = new DateOnly(2026, 12, 1) });
        await service.CreateAsync(new HomeProject { Title = "Urgente cedo", Priority = HomePriority.Urgent, DueDate = new DateOnly(2026, 11, 1) });

        (await service.GetAllAsync()).Select(p => p.Title).Should().Equal("Urgente cedo", "Urgente tarde", "Baixa");
    }

    [Fact]
    public async Task HomeProjects_BlankTitle_Throws()
    {
        await Households(_ana).CreateAsync("Casa");

        Func<Task> act = () => new HomeProjectService(_ana).CreateAsync(new HomeProject { Title = " " });

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task HomeProjects_NegativeCost_Throws()
    {
        await Households(_ana).CreateAsync("Casa");

        Func<Task> act = () => new HomeProjectService(_ana).CreateAsync(new HomeProject { Title = "X", EstimatedCost = -1 });

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task HomeProjects_UpdateOfAnotherHousehold_IsNotFound()
    {
        await Households(_ana).CreateAsync("Casa A");
        await Households(_eva).CreateAsync("Casa E");
        HomeProject project = await new HomeProjectService(_ana).CreateAsync(new HomeProject { Title = "Privado" });

        Func<Task> act = () => new HomeProjectService(_eva).UpdateAsync(new HomeProject { Id = project.Id, Title = "Roubado" });

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task HomeProjects_Delete_RemovesTheProjectAndItsTasks()
    {
        await Households(_ana).CreateAsync("Casa");
        HomeProjectService service = new(_ana);
        HomeProject project = await service.CreateAsync(new HomeProject { Title = "Obra", Tasks = [new HomeTask { Title = "A" }] });

        await service.DeleteAsync(project.Id);

        (await service.GetAllAsync()).Should().BeEmpty();
        using AgenderDbContext context = _db.Create(string.Empty);
        context.HomeTasks.Should().BeEmpty();
    }

    [Fact]
    public async Task Finance_Summary_TotalsTheMonthAndBuildsTheSixMonthTrend()
    {
        await Households(_ana).CreateAsync("Casa");
        FinanceService service = new(_ana);
        await service.CreateTransactionAsync(new FinanceTransaction { Type = TransactionType.Income, Amount = 1000, Category = "Salário", Date = new DateOnly(2026, 10, 1) });
        await service.CreateTransactionAsync(new FinanceTransaction { Type = TransactionType.Expense, Amount = 300, Category = "Habitação", Date = new DateOnly(2026, 10, 3) });
        await service.CreateTransactionAsync(new FinanceTransaction { Type = TransactionType.Expense, Amount = 50, Category = "Lazer", Date = new DateOnly(2026, 10, 5) });
        await service.CreateTransactionAsync(new FinanceTransaction { Type = TransactionType.Saving, Amount = 100, Category = "Poupança", Date = new DateOnly(2026, 10, 6) });
        await service.CreateTransactionAsync(new FinanceTransaction { Type = TransactionType.Expense, Amount = 80, Category = "Lazer", Date = new DateOnly(2026, 9, 6) });

        FinanceSummary summary = await service.GetSummaryAsync(2026, 10);

        summary.Income.Should().Be(1000);
        summary.Expenses.Should().Be(350);
        summary.Savings.Should().Be(100);
        summary.Balance.Should().Be(550);
        summary.ExpensesByCategory.Select(c => c.Category).Should().Equal("Habitação", "Lazer");
        summary.Trend.Should().HaveCount(6);
        summary.Trend[^1].Month.Should().Be(10);
        summary.Trend[^2].Expenses.Should().Be(80);
    }

    [Fact]
    public async Task Finance_GetTransactions_ReturnsOnlyTheRequestedMonthNewestFirst()
    {
        await Households(_ana).CreateAsync("Casa");
        FinanceService service = new(_ana);
        await service.CreateTransactionAsync(new FinanceTransaction { Amount = 10, Date = new DateOnly(2026, 10, 2) });
        await service.CreateTransactionAsync(new FinanceTransaction { Amount = 20, Date = new DateOnly(2026, 10, 9) });
        await service.CreateTransactionAsync(new FinanceTransaction { Amount = 30, Date = new DateOnly(2026, 9, 30) });

        IReadOnlyList<FinanceTransaction> october = await service.GetTransactionsAsync(2026, 10);

        october.Select(t => t.Amount).Should().Equal(20, 10);
    }

    [Fact]
    public async Task Finance_SavingWithGoal_RaisesAndLowersTheGoalAsItChanges()
    {
        await Households(_ana).CreateAsync("Casa");
        FinanceService service = new(_ana);
        SavingsGoal goal = await service.CreateGoalAsync(new SavingsGoal { Name = "Férias", Target = 1000 });
        FinanceTransaction saving = await service.CreateTransactionAsync(new FinanceTransaction { Type = TransactionType.Saving, Amount = 200, GoalId = goal.Id, Date = new DateOnly(2026, 10, 2) });

        (await service.GetGoalsAsync()).Single().Current.Should().Be(200);

        saving.Amount = 350;
        await service.UpdateTransactionAsync(saving);
        (await service.GetGoalsAsync()).Single().Current.Should().Be(350);

        await service.DeleteTransactionAsync(saving.Id);
        (await service.GetGoalsAsync()).Single().Current.Should().Be(0);
    }

    [Fact]
    public async Task Finance_GoalOnANonSavingTransaction_IsIgnored()
    {
        await Households(_ana).CreateAsync("Casa");
        FinanceService service = new(_ana);
        SavingsGoal goal = await service.CreateGoalAsync(new SavingsGoal { Name = "Férias", Target = 1000 });

        FinanceTransaction expense = await service.CreateTransactionAsync(new FinanceTransaction { Type = TransactionType.Expense, Amount = 50, GoalId = goal.Id });

        expense.GoalId.Should().BeNull();
        (await service.GetGoalsAsync()).Single().Current.Should().Be(0);
    }

    [Fact]
    public async Task Finance_DeleteGoal_UnlinksItsTransactions()
    {
        await Households(_ana).CreateAsync("Casa");
        FinanceService service = new(_ana);
        SavingsGoal goal = await service.CreateGoalAsync(new SavingsGoal { Name = "Férias", Target = 1000 });
        await service.CreateTransactionAsync(new FinanceTransaction { Type = TransactionType.Saving, Amount = 50, GoalId = goal.Id, Date = new DateOnly(2026, 10, 2) });

        await service.DeleteGoalAsync(goal.Id);

        (await service.GetTransactionsAsync(2026, 10)).Single().GoalId.Should().BeNull();
    }

    [Fact]
    public async Task Finance_SavingWithAccount_MovesMoneyIntoAndOutOfTheAccount()
    {
        await Households(_ana).CreateAsync("Casa");
        FinanceService service = new(_ana);
        SavingsAccount account = await service.CreateSavingsAccountAsync(new SavingsAccount { Name = "Conta X", Value = 1000 });
        FinanceTransaction deposit = await service.CreateTransactionAsync(new FinanceTransaction { Type = TransactionType.Saving, Amount = 200, SavingsAccountId = account.Id, Date = new DateOnly(2026, 10, 2) });

        (await service.GetSavingsAccountsAsync()).Single().Value.Should().Be(1200);

        deposit.Amount = 150;
        await service.UpdateTransactionAsync(deposit);
        (await service.GetSavingsAccountsAsync()).Single().Value.Should().Be(1150);

        deposit.IsWithdrawal = true;
        await service.UpdateTransactionAsync(deposit);
        (await service.GetSavingsAccountsAsync()).Single().Value.Should().Be(850);

        await service.DeleteTransactionAsync(deposit.Id);
        (await service.GetSavingsAccountsAsync()).Single().Value.Should().Be(1000);
    }

    [Fact]
    public async Task Finance_DeletingTheAccount_KeepsTheTransactionsWithoutIt()
    {
        await Households(_ana).CreateAsync("Casa");
        FinanceService service = new(_ana);
        SavingsAccount account = await service.CreateSavingsAccountAsync(new SavingsAccount { Name = "Conta X", Value = 0 });
        await service.CreateTransactionAsync(new FinanceTransaction { Type = TransactionType.Saving, Amount = 50, SavingsAccountId = account.Id, Date = new DateOnly(2026, 10, 2) });

        await service.DeleteSavingsAccountAsync(account.Id);

        (await service.GetTransactionsAsync(2026, 10)).Single().SavingsAccountId.Should().BeNull();
    }

    [Fact]
    public async Task Finance_AccountOnANonSavingTransaction_IsIgnored()
    {
        await Households(_ana).CreateAsync("Casa");
        FinanceService service = new(_ana);
        SavingsAccount account = await service.CreateSavingsAccountAsync(new SavingsAccount { Name = "Conta X", Value = 100 });

        await service.CreateTransactionAsync(new FinanceTransaction { Type = TransactionType.Expense, Amount = 10, SavingsAccountId = account.Id, Date = new DateOnly(2026, 10, 2) });

        (await service.GetSavingsAccountsAsync()).Single().Value.Should().Be(100);
    }

    [Fact]
    public async Task Finance_TransactionWithAccountOfAnotherHousehold_Throws()
    {
        await Households(_ana).CreateAsync("Casa A");
        await Households(_eva).CreateAsync("Casa E");
        SavingsAccount account = await new FinanceService(_ana).CreateSavingsAccountAsync(new SavingsAccount { Name = "Dela", Value = 5 });

        Func<Task> act = () => new FinanceService(_eva).CreateTransactionAsync(new FinanceTransaction { Type = TransactionType.Saving, Amount = 10, SavingsAccountId = account.Id });

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Finance_TransactionWithGoalOfAnotherHousehold_Throws()
    {
        await Households(_ana).CreateAsync("Casa A");
        await Households(_eva).CreateAsync("Casa E");
        SavingsGoal goal = await new FinanceService(_ana).CreateGoalAsync(new SavingsGoal { Name = "Dela", Target = 100 });

        Func<Task> act = () => new FinanceService(_eva).CreateTransactionAsync(new FinanceTransaction { Type = TransactionType.Saving, Amount = 10, GoalId = goal.Id });

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Finance_People_AreCreatedFromTheHouseholdMembers()
    {
        await CoupleAsync();

        IReadOnlyList<FinancePerson> people = await new FinanceService(_ana).GetPeopleAsync();

        people.Select(p => p.Name).Should().Equal("Ana Silva", "Rui Costa");
        people.Select(p => p.MemberUserId).Should().Equal("ana", "rui");
    }

    [Fact]
    public async Task Finance_Transaction_PersonOfAnotherHousehold_Throws()
    {
        await Households(_ana).CreateAsync("Casa A");
        await Households(_eva).CreateAsync("Casa E");
        FinancePerson other = (await new FinanceService(_eva).GetPeopleAsync()).Single();

        Func<Task> act = () => new FinanceService(_ana).CreateTransactionAsync(new FinanceTransaction { Amount = 12, PersonId = other.Id });

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Finance_Transfer_WithoutAPerson_Throws()
    {
        await CoupleAsync();

        Func<Task> act = () => new FinanceService(_ana).CreateTransactionAsync(new FinanceTransaction { Amount = 10, IsTransfer = true });

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Finance_Transfer_OfASaving_Throws()
    {
        await CoupleAsync();
        FinancePerson ana = (await new FinanceService(_ana).GetPeopleAsync()).First();

        Func<Task> act = () => new FinanceService(_ana).CreateTransactionAsync(new FinanceTransaction { Type = TransactionType.Saving, Amount = 10, PersonId = ana.Id, IsTransfer = true });

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Finance_Summary_IgnoresTransfersAndCountsWithdrawalsAsNegativeSavings()
    {
        await CoupleAsync();
        FinanceService service = new(_ana);
        FinancePerson ana = (await service.GetPeopleAsync()).First();
        await service.CreateTransactionAsync(new FinanceTransaction { Type = TransactionType.Income, Amount = 100, PersonId = ana.Id, Date = new DateOnly(2026, 10, 1) });
        await service.CreateTransactionAsync(new FinanceTransaction { Type = TransactionType.Expense, Amount = 40, PersonId = ana.Id, IsTransfer = true, Date = new DateOnly(2026, 10, 2) });
        await service.CreateTransactionAsync(new FinanceTransaction { Type = TransactionType.Saving, Amount = 30, Date = new DateOnly(2026, 10, 3) });
        await service.CreateTransactionAsync(new FinanceTransaction { Type = TransactionType.Saving, Amount = 10, IsWithdrawal = true, Date = new DateOnly(2026, 10, 4) });

        FinanceSummary summary = await service.GetSummaryAsync(2026, 10);

        summary.Income.Should().Be(100);
        summary.Expenses.Should().Be(0);
        summary.Savings.Should().Be(20);
        summary.Balance.Should().Be(80);
    }

    [Fact]
    public async Task Finance_OwnerFilter_SeparatesPeopleAndShared()
    {
        await CoupleAsync();
        FinanceService service = new(_ana);
        IReadOnlyList<FinancePerson> people = await service.GetPeopleAsync();
        await service.CreateTransactionAsync(new FinanceTransaction { Amount = 1, PersonId = people[0].Id, Date = new DateOnly(2026, 10, 1) });
        await service.CreateTransactionAsync(new FinanceTransaction { Amount = 2, PersonId = people[1].Id, Date = new DateOnly(2026, 10, 1) });
        await service.CreateTransactionAsync(new FinanceTransaction { Amount = 4, Date = new DateOnly(2026, 10, 1) });

        (await service.GetTransactionsAsync(2026, 10, people[1].Id.ToString())).Select(t => t.Amount).Should().Equal(2);
        (await service.GetTransactionsAsync(2026, 10, "shared")).Select(t => t.Amount).Should().Equal(4);
        (await service.GetTransactionsAsync(2026, 10)).Should().HaveCount(3);
        Func<Task> act = () => service.GetTransactionsAsync(2026, 10, "not-a-guid");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Finance_EditingAnImportedTransaction_MakesItManual()
    {
        await Households(_ana).CreateAsync("Casa");
        FinanceService service = new(_ana);
        FinanceTransaction created = await service.CreateTransactionAsync(new FinanceTransaction { Amount = 5, Date = new DateOnly(2026, 10, 1) });

        using (AgenderDbContext context = _db.Create(string.Empty))
        {
            context.FinanceTransactions.Single(t => t.Id == created.Id).IsImported = true;
            context.SaveChanges();
        }

        created.Amount = 6;
        await service.UpdateTransactionAsync(created);

        using AgenderDbContext check = _db.Create(string.Empty);
        check.FinanceTransactions.Single(t => t.Id == created.Id).IsImported.Should().BeFalse();
    }

    [Fact]
    public async Task Finance_Person_CanBeLinkedRenamedAndOnlyDeletedWhenUnused()
    {
        await CoupleAsync();
        FinanceService service = new(_ana);
        IReadOnlyList<FinancePerson> people = await service.GetPeopleAsync();
        FinancePerson extra = await service.CreatePersonAsync(new FinancePerson { Name = "Bruna" });

        await service.UpdatePersonAsync(new FinancePerson { Id = extra.Id, Name = "Bruna Dias", Color = "#EA9999", Order = 5 });
        await service.CreateTransactionAsync(new FinanceTransaction { Amount = 3, PersonId = extra.Id, Date = new DateOnly(2026, 10, 1) });

        Func<Task> used = () => service.DeletePersonAsync(extra.Id);
        Func<Task> duplicate = () => service.CreatePersonAsync(new FinancePerson { Name = " ana silva " });
        Func<Task> linkTaken = () => service.UpdatePersonAsync(new FinancePerson { Id = extra.Id, Name = "Bruna Dias", MemberUserId = "ana" });

        (await service.GetPeopleAsync()).Should().Contain(p => p.Name == "Bruna Dias" && p.Color == "#EA9999");
        await used.Should().ThrowAsync<ArgumentException>();
        await duplicate.Should().ThrowAsync<ArgumentException>();
        await linkTaken.Should().ThrowAsync<ArgumentException>();
        people.Should().HaveCount(2);
    }

    [Fact]
    public async Task Finance_Person_JoiningMemberIsLinkedByFirstName()
    {
        HouseholdDto household = await Households(_ana).CreateAsync("Casa");
        FinanceService service = new(_ana);
        FinancePerson bruna = await service.CreatePersonAsync(new FinancePerson { Name = "Rui" });

        await Households(_rui).JoinAsync(household.InviteCode);

        (await service.GetPeopleAsync()).Single(p => p.Id == bruna.Id).MemberUserId.Should().Be("rui");
    }

    [Fact]
    public async Task Finance_Person_JoiningMemberWithoutAMatch_GetsANewPersonWhenEveryoneIsLinked()
    {
        HouseholdDto household = await Households(_ana).CreateAsync("Casa");
        FinanceService service = new(_ana);
        await service.GetPeopleAsync();

        await Households(_rui).JoinAsync(household.InviteCode);

        (await service.GetPeopleAsync()).Select(p => p.MemberUserId).Should().BeEquivalentTo("ana", "rui");
    }

    [Fact]
    public async Task Balance_FollowsTheSpreadsheetRules()
    {
        await Households(_ana).CreateAsync("Casa");
        FinanceService service = new(_ana, new FixedTime(new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero)));
        FinancePerson bruna = (await service.GetPeopleAsync()).Single();
        await service.UpdatePersonAsync(new FinancePerson { Id = bruna.Id, Name = "Bruna", Order = 0 });
        FinancePerson ricardo = await service.CreatePersonAsync(new FinancePerson { Name = "Ricardo" });
        await service.CreateTransactionAsync(new FinanceTransaction { Type = TransactionType.Income, Amount = 1000, PersonId = bruna.Id, Date = new DateOnly(2025, 3, 1) });
        await service.CreateTransactionAsync(new FinanceTransaction { Type = TransactionType.Income, Amount = 500, PersonId = ricardo.Id, Date = new DateOnly(2025, 3, 1) });
        await service.CreateTransactionAsync(new FinanceTransaction { Type = TransactionType.Expense, Amount = 200, Date = new DateOnly(2025, 4, 1) });
        await service.CreateTransactionAsync(new FinanceTransaction { Type = TransactionType.Expense, Amount = 50, PersonId = bruna.Id, IsTransfer = true, Date = new DateOnly(2026, 2, 1) });

        BalanceSheet sheet = await service.GetBalanceAsync();

        BalanceYear y2025 = sheet.Years.Single(y => y.Year == 2025);
        y2025.Account.Should().Equal(900m, 400m);
        BalanceYear y2026 = sheet.Years.Single(y => y.Year == 2026);
        y2026.Account.Should().Equal(850m, 450m);
        sheet.Current!.Year.Should().Be(2026);
    }

    [Fact]
    public async Task Balance_Savings_UseSnapshotsForPastYearsAndAccountsForTheCurrentYear()
    {
        await Households(_ana).CreateAsync("Casa");
        FinanceService service = new(_ana, new FixedTime(new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero)));
        FinancePerson bruna = (await service.GetPeopleAsync()).Single();
        await service.UpdatePersonAsync(new FinancePerson { Id = bruna.Id, Name = "Bruna", Order = 0 });
        FinancePerson ricardo = await service.CreatePersonAsync(new FinancePerson { Name = "Ricardo" });
        await service.CreateTransactionAsync(new FinanceTransaction { Amount = 1, PersonId = bruna.Id, Date = new DateOnly(2024, 1, 1) });
        await service.SetSavingsSnapshotAsync(2024, bruna.Id, 1000);
        await service.SetSavingsSnapshotAsync(2024, ricardo.Id, 400);
        await service.CreateSavingsAccountAsync(new SavingsAccount { Name = "A", PersonId = bruna.Id, Value = 300 });
        await service.CreateSavingsAccountAsync(new SavingsAccount { Name = "B", PersonId = ricardo.Id, Value = 100 });
        await service.CreateSavingsAccountAsync(new SavingsAccount { Name = "C", PersonId = null, Value = 200 });

        BalanceSheet sheet = await service.GetBalanceAsync();

        sheet.Years.Single(y => y.Year == 2024).Savings.Should().Equal(1000m, 400m);
        sheet.Years.Single(y => y.Year == 2025).Savings.Should().Equal(0m, 0m);
        sheet.Years.Single(y => y.Year == 2026).Savings.Should().Equal(400m, 200m);
    }

    [Fact]
    public async Task Savings_Snapshot_UpsertsOnePerYearAndPerson()
    {
        await Households(_ana).CreateAsync("Casa");
        FinanceService service = new(_ana);
        FinancePerson person = (await service.GetPeopleAsync()).Single();

        await service.SetSavingsSnapshotAsync(2024, person.Id, 10);
        await service.SetSavingsSnapshotAsync(2024, person.Id, 20);

        SavingsSnapshot stored = (await service.GetSavingsSnapshotsAsync()).Single();
        stored.Value.Should().Be(20);

        await service.DeleteSavingsSnapshotAsync(stored.Id);
        (await service.GetSavingsSnapshotsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Savings_Account_ValidatesAndIsolatesByHousehold()
    {
        await Households(_ana).CreateAsync("Casa A");
        await Households(_eva).CreateAsync("Casa E");
        SavingsAccount account = await new FinanceService(_ana).CreateSavingsAccountAsync(new SavingsAccount { Name = "DP", Bank = "BPI", Value = 100 });

        Func<Task> negative = () => new FinanceService(_ana).CreateSavingsAccountAsync(new SavingsAccount { Name = "X", Value = -1 });
        Func<Task> otherHousehold = () => new FinanceService(_eva).UpdateSavingsAccountAsync(new SavingsAccount { Id = account.Id, Name = "Roubada" });

        await negative.Should().ThrowAsync<ArgumentException>();
        await otherHousehold.Should().ThrowAsync<NotFoundException>();
        (await new FinanceService(_eva).GetSavingsAccountsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Finance_NegativeAmount_IsStoredAsPositive()
    {
        await Households(_ana).CreateAsync("Casa");

        FinanceTransaction created = await new FinanceService(_ana).CreateTransactionAsync(new FinanceTransaction { Amount = -25.5m });

        created.Amount.Should().Be(25.5m);
    }

    [Fact]
    public async Task Finance_ZeroAmount_Throws()
    {
        await Households(_ana).CreateAsync("Casa");

        Func<Task> act = () => new FinanceService(_ana).CreateTransactionAsync(new FinanceTransaction { Amount = 0 });

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Finance_Summary_InvalidMonth_Throws()
    {
        await Households(_ana).CreateAsync("Casa");

        Func<Task> act = () => new FinanceService(_ana).GetSummaryAsync(2026, 13);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Finance_Goal_NonPositiveTarget_Throws()
    {
        await Households(_ana).CreateAsync("Casa");

        Func<Task> act = () => new FinanceService(_ana).CreateGoalAsync(new SavingsGoal { Name = "X", Target = 0 });

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Events_Birthday_BecomesAllDayAndYearly()
    {
        await Households(_ana).CreateAsync("Casa");

        FamilyEvent created = await new FamilyEventService(_ana).CreateAsync(new FamilyEvent
        {
            Title = "Avó",
            Kind = FamilyEventKind.Birthday,
            Start = new DateTime(1950, 11, 3, 14, 0, 0),
            Recurrence = EventRecurrence.None
        });

        created.AllDay.Should().BeTrue();
        created.Recurrence.Should().Be(EventRecurrence.Yearly);
        created.Start.Should().Be(new DateTime(1950, 11, 3));
    }

    [Fact]
    public async Task Events_Get_ExpandsRecurrencesInsideTheRange()
    {
        await Households(_ana).CreateAsync("Casa");
        FamilyEventService service = new(_ana);
        await service.CreateAsync(new FamilyEvent { Title = "Natação", Start = new DateTime(2026, 10, 5, 18, 0, 0), End = new DateTime(2026, 10, 5, 19, 0, 0), Recurrence = EventRecurrence.Weekly });
        await service.CreateAsync(new FamilyEvent { Title = "Pontual", Start = new DateTime(2026, 11, 20, 9, 0, 0) });

        IReadOnlyList<EventOccurrence> october = await service.GetAsync(new DateTime(2026, 10, 1), new DateTime(2026, 11, 1));

        october.Count(o => o.Event.Title == "Natação").Should().Be(4);
        october.Should().NotContain(o => o.Event.Title == "Pontual");
    }

    [Fact]
    public async Task Events_EndBeforeStart_Throws()
    {
        await Households(_ana).CreateAsync("Casa");

        Func<Task> act = () => new FamilyEventService(_ana).CreateAsync(new FamilyEvent { Title = "X", Start = new DateTime(2026, 10, 5, 18, 0, 0), End = new DateTime(2026, 10, 5, 17, 0, 0) });

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Events_Get_RangeTooLarge_Throws()
    {
        await Households(_ana).CreateAsync("Casa");

        Func<Task> act = () => new FamilyEventService(_ana).GetAsync(new DateTime(2020, 1, 1), new DateTime(2026, 1, 1));

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Events_Member_IsResolvedFromTheHousehold()
    {
        await CoupleAsync();

        FamilyEvent created = await new FamilyEventService(_ana).CreateAsync(new FamilyEvent { Title = "Dentista", Start = new DateTime(2026, 10, 5, 9, 0, 0), MemberId = "rui" });

        created.MemberName.Should().Be("Rui Costa");
    }

    [Fact]
    public async Task Events_DeleteOfAnotherHousehold_IsNotFound()
    {
        await Households(_ana).CreateAsync("Casa A");
        await Households(_eva).CreateAsync("Casa E");
        FamilyEvent created = await new FamilyEventService(_ana).CreateAsync(new FamilyEvent { Title = "Privado", Start = new DateTime(2026, 10, 5, 9, 0, 0) });

        Func<Task> act = () => new FamilyEventService(_eva).DeleteAsync(created.Id);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
