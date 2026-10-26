using System.Net;
using System.Net.Http.Json;
using Agender.Shared.Contracts;
using Agender.Shared.Domain;
using FluentAssertions;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace Agender.Tests.Api;

public sealed class CasaApiTests(AgenderApiFactory factory) : IClassFixture<AgenderApiFactory>
{
    [Fact]
    public async Task Casa_WithoutToken_ReturnsUnauthorized()
    {
        using HttpClient client = factory.CreateClient();

        (await client.GetAsync("/api/casa/household")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Household_NewUser_HasNoHouseholdUntilOneIsCreated()
    {
        using HttpClient client = await factory.CreateAuthenticatedClientAsync("casa-new@example.com", "Nova");

        (await client.GetFromJsonAsync<HouseholdEnvelope>("/api/casa/household"))!.Household.Should().BeNull();

        HttpResponseMessage created = await client.PostAsJsonAsync("/api/casa/household", new CreateHouseholdRequest("Casa Nova"));
        created.EnsureSuccessStatusCode();

        HouseholdDto household = (await created.Content.ReadFromJsonAsync<HouseholdDto>())!;
        household.Members.Should().ContainSingle().Which.DisplayName.Should().Be("Nova");
        (await client.GetFromJsonAsync<HouseholdEnvelope>("/api/casa/household"))!.Household!.Id.Should().Be(household.Id);
    }

    [Fact]
    public async Task Shopping_WithoutAHousehold_ReturnsBadRequest()
    {
        using HttpClient client = await factory.CreateAuthenticatedClientAsync("casa-none@example.com");

        (await client.GetAsync("/api/casa/shopping/lists")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Shopping_RoundTrip_PersistsItemsAndSharesAcrossTheHousehold()
    {
        using HttpClient ana = await factory.CreateAuthenticatedClientAsync("casa-ana@example.com", "Ana");
        using HttpClient rui = await factory.CreateAuthenticatedClientAsync("casa-rui@example.com", "Rui");
        HouseholdDto household = (await (await ana.PostAsJsonAsync("/api/casa/household", new CreateHouseholdRequest("Casa"))).Content.ReadFromJsonAsync<HouseholdDto>())!;
        (await rui.PostAsJsonAsync("/api/casa/household/join", new JoinHouseholdRequest(household.InviteCode))).EnsureSuccessStatusCode();

        ShoppingList list = (await (await ana.PostAsJsonAsync("/api/casa/shopping/lists", new ShoppingList { Name = "Semana", Emoji = "🛒" })).Content.ReadFromJsonAsync<ShoppingList>())!;
        ShoppingItem item = (await (await rui.PostAsJsonAsync($"/api/casa/shopping/lists/{list.Id}/items", new ShoppingItem { Name = "Leite", Quantity = 2, Unit = "L" })).Content.ReadFromJsonAsync<ShoppingItem>())!;

        item.Category.Should().Be("Laticínios e Ovos");
        ShoppingList loaded = (await ana.GetFromJsonAsync<ShoppingList>($"/api/casa/shopping/lists/{list.Id}"))!;
        loaded.Items.Should().ContainSingle().Which.AddedBy.Should().Be("Rui");
        loaded.Items[0].Quantity.Should().Be(2);

        item.IsChecked = true;
        (await ana.PutAsJsonAsync($"/api/casa/shopping/items/{item.Id}", item)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ana.GetFromJsonAsync<ShoppingList[]>("/api/casa/shopping/lists"))!.Single().PendingCount.Should().Be(0);
    }

    [Fact]
    public async Task Shopping_UpdateWithMismatchedId_ReturnsBadRequest()
    {
        using HttpClient client = await factory.CreateAuthenticatedClientAsync("casa-mismatch@example.com");
        await client.PostAsJsonAsync("/api/casa/household", new CreateHouseholdRequest("Casa"));
        ShoppingList list = (await (await client.PostAsJsonAsync("/api/casa/shopping/lists", new ShoppingList { Name = "A" })).Content.ReadFromJsonAsync<ShoppingList>())!;

        HttpResponseMessage response = await client.PutAsJsonAsync($"/api/casa/shopping/lists/{Guid.NewGuid()}", list);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Shopping_ListOfAnotherHousehold_ReturnsNotFound()
    {
        using HttpClient owner = await factory.CreateAuthenticatedClientAsync("casa-owner@example.com");
        using HttpClient other = await factory.CreateAuthenticatedClientAsync("casa-other@example.com");
        await owner.PostAsJsonAsync("/api/casa/household", new CreateHouseholdRequest("A"));
        await other.PostAsJsonAsync("/api/casa/household", new CreateHouseholdRequest("B"));
        ShoppingList list = (await (await owner.PostAsJsonAsync("/api/casa/shopping/lists", new ShoppingList { Name = "Privada" })).Content.ReadFromJsonAsync<ShoppingList>())!;

        (await other.GetAsync($"/api/casa/shopping/lists/{list.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Projects_RoundTrip_KeepsEnumsDatesAndTasks()
    {
        using HttpClient client = await factory.CreateAuthenticatedClientAsync("casa-projects@example.com");
        await client.PostAsJsonAsync("/api/casa/household", new CreateHouseholdRequest("Casa"));

        HomeProject created = (await (await client.PostAsJsonAsync("/api/casa/projects", new HomeProject
        {
            Title = "Obra",
            Kind = HomeKind.Renovation,
            Priority = HomePriority.Urgent,
            Status = HomeStatus.InProgress,
            DueDate = new DateOnly(2026, 12, 24),
            EstimatedCost = 1234.5m,
            Tasks = [new HomeTask { Title = "A" }, new HomeTask { Title = "B", Done = true }]
        })).Content.ReadFromJsonAsync<HomeProject>())!;

        HomeProject stored = (await client.GetFromJsonAsync<HomeProject[]>("/api/casa/projects"))!.Single();

        stored.Id.Should().Be(created.Id);
        stored.Kind.Should().Be(HomeKind.Renovation);
        stored.Priority.Should().Be(HomePriority.Urgent);
        stored.DueDate.Should().Be(new DateOnly(2026, 12, 24));
        stored.EstimatedCost.Should().Be(1234.5m);
        stored.Tasks.Select(t => t.Done).Should().Equal(false, true);
    }

    [Fact]
    public async Task Finance_RoundTrip_UpdatesTheSummaryAndGoal()
    {
        using HttpClient client = await factory.CreateAuthenticatedClientAsync("casa-finance@example.com");
        await client.PostAsJsonAsync("/api/casa/household", new CreateHouseholdRequest("Casa"));
        SavingsGoal goal = (await (await client.PostAsJsonAsync("/api/casa/finance/goals", new SavingsGoal { Name = "Férias", Target = 1000 })).Content.ReadFromJsonAsync<SavingsGoal>())!;

        await client.PostAsJsonAsync("/api/casa/finance/transactions", new FinanceTransaction { Type = TransactionType.Income, Amount = 2000, Category = "Salário", Date = new DateOnly(2026, 10, 1) });
        await client.PostAsJsonAsync("/api/casa/finance/transactions", new FinanceTransaction { Type = TransactionType.Saving, Amount = 250, GoalId = goal.Id, Date = new DateOnly(2026, 10, 2) });

        FinanceSummary summary = (await client.GetFromJsonAsync<FinanceSummary>("/api/casa/finance/summary?year=2026&month=10"))!;
        SavingsGoal[] goals = (await client.GetFromJsonAsync<SavingsGoal[]>("/api/casa/finance/goals"))!;

        summary.Income.Should().Be(2000);
        summary.Savings.Should().Be(250);
        summary.Balance.Should().Be(1750);
        summary.SavingsTotal.Should().Be(250);
        goals.Single().Current.Should().Be(250);
        goals.Single().Progress.Should().Be(0.25);
    }

    [Fact]
    public async Task Finance_InvalidMonth_ReturnsBadRequest()
    {
        using HttpClient client = await factory.CreateAuthenticatedClientAsync("casa-month@example.com");
        await client.PostAsJsonAsync("/api/casa/household", new CreateHouseholdRequest("Casa"));

        (await client.GetAsync("/api/casa/finance/summary?year=2026&month=14")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Events_RoundTrip_ExpandsRecurringEvents()
    {
        using HttpClient client = await factory.CreateAuthenticatedClientAsync("casa-events@example.com");
        await client.PostAsJsonAsync("/api/casa/household", new CreateHouseholdRequest("Casa"));
        await client.PostAsJsonAsync("/api/casa/events", new FamilyEvent
        {
            Title = "Natação",
            Start = new DateTime(2026, 10, 5, 18, 0, 0),
            End = new DateTime(2026, 10, 5, 19, 0, 0),
            Recurrence = EventRecurrence.Weekly
        });

        EventOccurrence[] october = (await client.GetFromJsonAsync<EventOccurrence[]>("/api/casa/events?from=2026-10-01T00:00:00&to=2026-11-01T00:00:00"))!;

        october.Should().HaveCount(4);
        october.All(o => o.Event.Title == "Natação").Should().BeTrue();
        october[1].Start.Should().Be(new DateTime(2026, 10, 12, 18, 0, 0));
    }

    [Fact]
    public async Task Tunnel_RequestsToCasaApi_AreNotFound()
    {
        using HttpClient client = factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/casa/household");
        request.Headers.Add("CF-Connecting-IP", "203.0.113.7");

        (await client.SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ScopeHeader_ActingAsAHouseholdMember_WritesToThatMembersAgenda()
    {
        using HttpClient ana = await factory.CreateAuthenticatedClientAsync("scope-ana@example.com", "Ana");
        using HttpClient rui = await factory.CreateAuthenticatedClientAsync("scope-rui@example.com", "Rui");
        HouseholdDto household = (await (await ana.PostAsJsonAsync("/api/casa/household", new CreateHouseholdRequest("Casa"))).Content.ReadFromJsonAsync<HouseholdDto>())!;
        HouseholdDto joined = (await (await rui.PostAsJsonAsync("/api/casa/household/join", new JoinHouseholdRequest(household.InviteCode))).Content.ReadFromJsonAsync<HouseholdDto>())!;

        using HttpRequestMessage create = new(HttpMethod.Post, "/api/tasks") { Content = JsonContent.Create(new TaskDto(Guid.NewGuid(), "Tarefa da Ana para o Rui", EntryColor.Teal, false, DateTime.UtcNow, null)) };
        create.Headers.Add("X-Agender-Scope", joined.MyUserId);
        (await ana.SendAsync(create)).EnsureSuccessStatusCode();

        (await rui.GetFromJsonAsync<TaskDto[]>("/api/tasks"))!.Should().ContainSingle().Which.Title.Should().Be("Tarefa da Ana para o Rui");
        (await ana.GetFromJsonAsync<TaskDto[]>("/api/tasks"))!.Should().BeEmpty();
    }

    [Fact]
    public async Task ScopeHeader_TargetOutsideTheHousehold_ReturnsNotFound()
    {
        using HttpClient ana = await factory.CreateAuthenticatedClientAsync("scope-ana2@example.com");
        using HttpClient eva = await factory.CreateAuthenticatedClientAsync("scope-eva2@example.com");
        await ana.PostAsJsonAsync("/api/casa/household", new CreateHouseholdRequest("Casa"));
        HouseholdDto other = (await (await eva.PostAsJsonAsync("/api/casa/household", new CreateHouseholdRequest("Outra"))).Content.ReadFromJsonAsync<HouseholdDto>())!;

        using HttpRequestMessage read = new(HttpMethod.Get, "/api/tasks");
        read.Headers.Add("X-Agender-Scope", other.MyUserId);

        (await ana.SendAsync(read)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ScopeHeader_HouseholdScope_IsSharedByEveryMember()
    {
        using HttpClient ana = await factory.CreateAuthenticatedClientAsync("scope-ana3@example.com", "Ana");
        using HttpClient rui = await factory.CreateAuthenticatedClientAsync("scope-rui3@example.com", "Rui");
        HouseholdDto household = (await (await ana.PostAsJsonAsync("/api/casa/household", new CreateHouseholdRequest("Casa"))).Content.ReadFromJsonAsync<HouseholdDto>())!;
        await rui.PostAsJsonAsync("/api/casa/household/join", new JoinHouseholdRequest(household.InviteCode));
        string scope = $"h:{household.Id}";

        using HttpRequestMessage create = new(HttpMethod.Post, "/api/tasks") { Content = JsonContent.Create(new TaskDto(Guid.NewGuid(), "Comprar lâmpadas", EntryColor.Amber, false, DateTime.UtcNow, null)) };
        create.Headers.Add("X-Agender-Scope", scope);
        (await ana.SendAsync(create)).EnsureSuccessStatusCode();

        using HttpRequestMessage read = new(HttpMethod.Get, "/api/tasks");
        read.Headers.Add("X-Agender-Scope", scope);
        (await (await rui.SendAsync(read)).Content.ReadFromJsonAsync<TaskDto[]>())!.Should().ContainSingle().Which.Title.Should().Be("Comprar lâmpadas");
    }

    [Fact]
    public async Task Invite_ByEmail_LetsThePersonJoinAfterRegistering()
    {
        using HttpClient ana = await factory.CreateAuthenticatedClientAsync("invite-ana@example.com", "Ana");
        await ana.PostAsJsonAsync("/api/casa/household", new CreateHouseholdRequest("Casa"));
        HttpResponseMessage invite = await ana.PostAsJsonAsync("/api/casa/household/invites", new InviteRequest("invite-bruna@example.com"));
        invite.EnsureSuccessStatusCode();
        (await invite.Content.ReadFromJsonAsync<HouseholdDto>())!.Invites.Should().ContainSingle();

        using HttpClient bruna = await factory.CreateAuthenticatedClientAsync("invite-bruna@example.com", "Bruna");
        HouseholdDto joined = (await bruna.GetFromJsonAsync<HouseholdEnvelope>("/api/casa/household"))!.Household!;

        joined.Members.Select(m => m.DisplayName).Should().BeEquivalentTo("Ana", "Bruna");
        joined.Invites.Should().BeEmpty();
    }

    [Fact]
    public async Task FinanceImport_DryRunThenApply_FillsTheBalance()
    {
        using HttpClient client = await factory.CreateAuthenticatedClientAsync("import-user@example.com", "Ricardo Gordo");
        await client.PostAsJsonAsync("/api/casa/household", new CreateHouseholdRequest("Casa"));
        byte[] workbook = Services.FinanceImportTests.BuildWorkbook();

        using ByteArrayContent dryRun = new(workbook);
        ImportReport preview = (await (await client.PostAsync("/api/casa/finance/import?apply=false", dryRun)).Content.ReadFromJsonAsync<ImportReport>())!;
        (await client.GetFromJsonAsync<FinancePerson[]>("/api/casa/finance/people"))!.Should().ContainSingle();

        using ByteArrayContent apply = new(workbook);
        HttpResponseMessage applied = await client.PostAsync("/api/casa/finance/import?apply=true", apply);
        applied.EnsureSuccessStatusCode();
        BalanceSheet sheet = (await client.GetFromJsonAsync<BalanceSheet>("/api/casa/finance/balance"))!;

        preview.Applied.Should().BeFalse();
        preview.RowsImported.Should().Be(13);
        (await applied.Content.ReadFromJsonAsync<ImportReport>())!.Applied.Should().BeTrue();
        sheet.People.Select(p => p.Name).Should().Equal("Bruna", "Ricardo");
        sheet.Years.Select(y => y.Year).Should().Contain(2025);
    }

    [Fact]
    public async Task FinanceImport_NotAWorkbook_ReturnsBadRequest()
    {
        using HttpClient client = await factory.CreateAuthenticatedClientAsync("import-bad@example.com");
        await client.PostAsJsonAsync("/api/casa/household", new CreateHouseholdRequest("Casa"));

        using ByteArrayContent junk = new([1, 2, 3, 4, 5]);
        HttpResponseMessage response = await client.PostAsync("/api/casa/finance/import?apply=false", junk);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task FinanceImport_WithoutAHousehold_ReturnsBadRequest()
    {
        using HttpClient client = await factory.CreateAuthenticatedClientAsync("import-none@example.com");

        using ByteArrayContent workbook = new(Services.FinanceImportTests.BuildWorkbook());
        HttpResponseMessage response = await client.PostAsync("/api/casa/finance/import?apply=false", workbook);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Finance_PeopleAndSavingsEndpoints_RoundTrip()
    {
        using HttpClient client = await factory.CreateAuthenticatedClientAsync("fin-people@example.com", "Ana");
        await client.PostAsJsonAsync("/api/casa/household", new CreateHouseholdRequest("Casa"));
        FinancePerson created = (await (await client.PostAsJsonAsync("/api/casa/finance/people", new FinancePerson { Name = "Bruna", Color = "#EA9999" })).Content.ReadFromJsonAsync<FinancePerson>())!;

        (await client.PutAsJsonAsync("/api/casa/finance/savings/snapshots", new SnapshotRequest(2024, created.Id, 500))).EnsureSuccessStatusCode();
        SavingsAccount account = (await (await client.PostAsJsonAsync("/api/casa/finance/savings/accounts", new SavingsAccount { Name = "DP", PersonId = created.Id, Value = 100 })).Content.ReadFromJsonAsync<SavingsAccount>())!;
        await client.PostAsJsonAsync("/api/casa/finance/transactions", new FinanceTransaction { Type = TransactionType.Income, Amount = 100, PersonId = created.Id, Date = new DateOnly(2024, 5, 1) });

        FinancePerson[] people = (await client.GetFromJsonAsync<FinancePerson[]>("/api/casa/finance/people"))!;
        BalanceSheet sheet = (await client.GetFromJsonAsync<BalanceSheet>("/api/casa/finance/balance"))!;
        (await client.DeleteAsync($"/api/casa/finance/savings/accounts/{account.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.DeleteAsync($"/api/casa/finance/people/{created.Id}")).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        people.Should().HaveCount(2);
        sheet.Years.Single(y => y.Year == 2024).Savings.Sum().Should().Be(500);
    }

    [Fact]
    public async Task Hub_WithoutToken_IsRejected()
    {
        await using HubConnection connection = BuildHub(factory, accessToken: null);

        Func<Task> act = () => connection.StartAsync();

        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task Hub_HouseholdMemberIsNotifiedWhenAPartnerChangesTheirAgenda()
    {
        using HttpClient ana = await factory.CreateAuthenticatedClientAsync("hub-ana@example.com", "Ana");
        using HttpClient rui = await factory.CreateAuthenticatedClientAsync("hub-rui@example.com", "Rui");
        HouseholdDto household = (await (await ana.PostAsJsonAsync("/api/casa/household", new CreateHouseholdRequest("Casa"))).Content.ReadFromJsonAsync<HouseholdDto>())!;
        HouseholdDto joined = (await (await rui.PostAsJsonAsync("/api/casa/household/join", new JoinHouseholdRequest(household.InviteCode))).Content.ReadFromJsonAsync<HouseholdDto>())!;

        TaskCompletionSource<ChangeNotice> received = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await using HubConnection connection = BuildHub(factory, rui.DefaultRequestHeaders.Authorization!.Parameter);
        connection.On<ChangeNotice>("changed", notice => received.TrySetResult(notice));
        await connection.StartAsync();

        using HttpRequestMessage create = new(HttpMethod.Post, "/api/tasks") { Content = JsonContent.Create(new TaskDto(Guid.NewGuid(), "Nova", EntryColor.Teal, false, DateTime.UtcNow, null)) };
        create.Headers.Add("X-Agender-Scope", joined.MyUserId);
        (await ana.SendAsync(create)).EnsureSuccessStatusCode();

        ChangeNotice notice = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        notice.ScopeId.Should().Be(joined.MyUserId);
        notice.ActorId.Should().NotBe(joined.MyUserId);
    }

    private static HubConnection BuildHub(AgenderApiFactory factory, string? accessToken) =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, "/hubs/changes"), options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                options.AccessTokenProvider = () => Task.FromResult(accessToken);
            })
            .Build();
}
