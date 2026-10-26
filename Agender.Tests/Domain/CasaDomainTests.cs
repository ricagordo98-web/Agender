using Agender.Shared.Domain;
using FluentAssertions;

namespace Agender.Tests.Domain;

public class CasaDomainTests
{
    [Theory]
    [InlineData("2 leite", 2, null, "Leite")]
    [InlineData("1,5 kg maçãs", 1.5, "kg", "Maçãs")]
    [InlineData("pão", 1, null, "Pão")]
    [InlineData("3 un ovos", 3, "un", "Ovos")]
    [InlineData("2", 1, null, "2")]
    [InlineData("  arroz  agulha ", 1, null, "Arroz agulha")]
    public void QuickAddParser_Parse_ExtractsQuantityUnitAndName(string text, double quantity, string? unit, string name)
    {
        QuickAddParser.Parsed parsed = QuickAddParser.Parse(text);

        parsed.Quantity.Should().Be((decimal)quantity);
        parsed.Unit.Should().Be(unit);
        parsed.Name.Should().Be(name);
    }

    [Theory]
    [InlineData("Bananas", "Frutas e Legumes")]
    [InlineData("Peito de frango", "Talho e Peixaria")]
    [InlineData("Leite meio-gordo", "Laticínios e Ovos")]
    [InlineData("Detergente da loiça", "Limpeza")]
    [InlineData("Qualquer coisa estranha", "Outros")]
    public void CasaCatalog_GuessCategory_UsesKeywords(string item, string category)
    {
        CasaCatalog.GuessCategory(item).Should().Be(category);
    }

    [Fact]
    public void CasaCatalog_NewInviteCode_UsesTheSafeAlphabet()
    {
        string code = CasaCatalog.NewInviteCode();

        code.Should().HaveLength(Household.InviteCodeLength).And.MatchRegex("^[A-HJ-NP-Z2-9]+$");
    }

    [Theory]
    [InlineData("Ana Silva", "AS")]
    [InlineData("ana", "A")]
    [InlineData(" ", "?")]
    public void CasaCatalog_Initials_UsesFirstAndLastName(string name, string expected)
    {
        CasaCatalog.Initials(name).Should().Be(expected);
    }

    [Fact]
    public void EventExpander_NoRecurrence_ReturnsOnlyEventsInsideTheRange()
    {
        FamilyEvent inside = new() { Title = "A", Start = new DateTime(2026, 10, 10, 9, 0, 0) };
        FamilyEvent outside = new() { Title = "B", Start = new DateTime(2026, 12, 10, 9, 0, 0) };

        List<EventOccurrence> result = [.. EventExpander.Expand([inside, outside], new DateTime(2026, 10, 1), new DateTime(2026, 11, 1))];

        result.Should().ContainSingle().Which.Event.Should().BeSameAs(inside);
    }

    [Fact]
    public void EventExpander_MultiDayEventStartedBeforeTheRange_IsIncluded()
    {
        FamilyEvent trip = new() { Title = "Viagem", Start = new DateTime(2026, 9, 29), End = new DateTime(2026, 10, 3) };

        EventExpander.Expand([trip], new DateTime(2026, 10, 1), new DateTime(2026, 10, 8)).Should().ContainSingle();
    }

    [Fact]
    public void EventExpander_Weekly_RepeatsEverySevenDays()
    {
        FamilyEvent swim = new() { Title = "Natação", Start = new DateTime(2026, 9, 7, 18, 0, 0), Recurrence = EventRecurrence.Weekly };

        List<EventOccurrence> result = [.. EventExpander.Expand([swim], new DateTime(2026, 10, 1), new DateTime(2026, 10, 15))];

        result.Select(o => o.Start.Date).Should().Equal(new DateTime(2026, 10, 5), new DateTime(2026, 10, 12));
    }

    [Fact]
    public void EventExpander_Monthly_KeepsTheDayOfMonth()
    {
        FamilyEvent rent = new() { Title = "Renda", Start = new DateTime(2026, 1, 31), AllDay = true, Recurrence = EventRecurrence.Monthly };

        List<EventOccurrence> result = [.. EventExpander.Expand([rent], new DateTime(2026, 2, 1), new DateTime(2026, 5, 1))];

        result.Select(o => o.Start.Date).Should().Equal(new DateTime(2026, 2, 28), new DateTime(2026, 3, 31), new DateTime(2026, 4, 30));
    }

    [Fact]
    public void EventExpander_Yearly_ReportsTheAge()
    {
        FamilyEvent birthday = new() { Title = "Avó", Kind = FamilyEventKind.Birthday, Start = new DateTime(1950, 11, 3), AllDay = true, Recurrence = EventRecurrence.Yearly };

        EventOccurrence occurrence = EventExpander.Expand([birthday], new DateTime(2026, 10, 1), new DateTime(2026, 12, 1)).Single();

        occurrence.Start.Should().Be(new DateTime(2026, 11, 3));
        occurrence.Years.Should().Be(76);
    }

    [Fact]
    public void EventExpander_RecurrenceStartingInTheFuture_DoesNotRepeatBeforeItStarts()
    {
        FamilyEvent future = new() { Title = "Futuro", Start = new DateTime(2027, 3, 1), Recurrence = EventRecurrence.Weekly };

        EventExpander.Expand([future], new DateTime(2026, 10, 1), new DateTime(2026, 11, 1)).Should().BeEmpty();
    }

    [Fact]
    public void SavingsGoal_Progress_IsCappedAtOne()
    {
        new SavingsGoal { Target = 100, Current = 250 }.Progress.Should().Be(1);
        new SavingsGoal { Target = 0, Current = 10 }.Progress.Should().Be(0);
        new SavingsGoal { Target = 200, Current = 50 }.Progress.Should().Be(0.25);
    }

    [Fact]
    public void CasaCatalog_ExpenseCategory_UnknownNamesGetAStableDistinctColor()
    {
        CasaCatalog.Category first = CasaCatalog.ExpenseCategory("Via Verde");
        CasaCatalog.Category again = CasaCatalog.ExpenseCategory("Via Verde");

        again.Should().Be(first);
        first.Name.Should().Be("Via Verde");
        CasaCatalog.ExpenseCategory("Habitação").Color.Should().Be("#D4B483");
        CasaCatalog.ExpenseCategory("Outros").Name.Should().Be("Outros");
        CasaCatalog.ExpenseCategory(null).Name.Should().Be("Outros");
    }

    [Fact]
    public void BalanceCalculator_SharedAndTransfers_FollowTheSpreadsheetRules()
    {
        FinancePerson bruna = new() { Name = "Bruna", Order = 0 };
        FinancePerson ricardo = new() { Name = "Ricardo", Order = 1 };
        List<FinanceTransaction> transactions =
        [
            new() { Type = TransactionType.Income, Amount = 100, PersonId = bruna.Id, Date = new DateOnly(2025, 1, 1) },
            new() { Type = TransactionType.Expense, Amount = 40, Date = new DateOnly(2025, 1, 2) },
            new() { Type = TransactionType.Expense, Amount = 10, PersonId = bruna.Id, IsTransfer = true, Date = new DateOnly(2025, 1, 3) },
            new() { Type = TransactionType.Saving, Amount = 20, PersonId = ricardo.Id, Date = new DateOnly(2025, 1, 4) },
            new() { Type = TransactionType.Saving, Amount = 5, PersonId = ricardo.Id, IsWithdrawal = true, Date = new DateOnly(2025, 1, 5) },
            new() { Type = TransactionType.Income, Amount = 999, PersonId = bruna.Id, Date = new DateOnly(2026, 1, 1) }
        ];

        Agender.Shared.Contracts.BalanceSheet sheet = BalanceCalculator.Compute([ricardo, bruna], transactions, [], [], new DateOnly(2025, 6, 1));

        sheet.People.Select(p => p.Name).Should().Equal("Bruna", "Ricardo");
        Agender.Shared.Contracts.BalanceYear y2025 = sheet.Years.Single(y => y.Year == 2025);
        y2025.Account.Should().Equal(70m, -25m);
        sheet.Years.Single(y => y.Year == 2026).Account[0].Should().Be(1069m);
        sheet.Current!.Year.Should().Be(2025);
    }

    [Fact]
    public void BalanceCalculator_NoPeople_ReturnsAnEmptySheet()
    {
        BalanceCalculator.Compute([], [], [], [], new DateOnly(2026, 1, 1)).Years.Should().BeEmpty();
    }

    [Fact]
    public void FinanceTransaction_SignedValue_DependsOnTypeAndDirection()
    {
        new FinanceTransaction { Type = TransactionType.Income, Amount = 10 }.SignedValue.Should().Be(10);
        new FinanceTransaction { Type = TransactionType.Expense, Amount = 10 }.SignedValue.Should().Be(-10);
        new FinanceTransaction { Type = TransactionType.Saving, Amount = 10 }.SignedValue.Should().Be(-10);
        new FinanceTransaction { Type = TransactionType.Saving, Amount = 10, IsWithdrawal = true }.SignedValue.Should().Be(10);
    }
}
