using Agender.Shared.Domain;

namespace Agender.Shared.Contracts;

public record MemberDto(string UserId, string DisplayName, string Email, string Color);

public record InviteDto(Guid Id, string Email, DateTime ExpiresAt);

public record HouseholdDto(Guid Id, string Name, string InviteCode, string MyUserId, IReadOnlyList<MemberDto> Members, IReadOnlyList<InviteDto>? Invites = null);

public record HouseholdEnvelope(HouseholdDto? Household);

public record CreateHouseholdRequest(string Name);

public record JoinHouseholdRequest(string Code);

public record InviteRequest(string Email);

public record SnapshotRequest(int Year, Guid PersonId, decimal Value);

public record RenameHouseholdRequest(string Name);

public record MemberColorRequest(string Color);

public record ShareListRequest(string Email, bool CanEdit);

public record CategoryTotal(string Category, decimal Total);

public record MonthTotal(int Year, int Month, decimal Income, decimal Expenses, decimal Savings);

public class FinanceSummary
{
    public int Year { get; set; }

    public int Month { get; set; }

    public decimal Income { get; set; }

    public decimal Expenses { get; set; }

    public decimal Savings { get; set; }

    public decimal Balance => Income - Expenses - Savings;

    public decimal SavingsTotal { get; set; }

    public List<CategoryTotal> ExpensesByCategory { get; set; } = [];

    public List<MonthTotal> Trend { get; set; } = [];
}

public record BalancePerson(Guid Id, string Name, string Color);

public record BalanceYear(int Year, IReadOnlyList<decimal> Account, IReadOnlyList<decimal> Savings, IReadOnlyList<decimal> Total);

public class BalanceSheet
{
    public List<BalancePerson> People { get; set; } = [];

    public List<BalanceYear> Years { get; set; } = [];

    public BalanceYear? Current { get; set; }
}

public record ImportReport(
    bool Applied,
    int RowsRead,
    int RowsImported,
    int RowsSkipped,
    int TransfersImported,
    int SharedImported,
    int AccountsImported,
    int SnapshotsImported,
    IReadOnlyList<string> People,
    IReadOnlyList<NameChange> Normalized,
    IReadOnlyList<string> Warnings,
    BalanceYear Preview);

public record NameChange(string From, string To, int Count);

public class CasaDashboard
{
    public FinanceSummary Finance { get; set; } = new();

    public List<ShoppingList> Lists { get; set; } = [];

    public int PendingShoppingItems { get; set; }

    public List<HomeProject> TopProjects { get; set; } = [];

    public int OpenProjects { get; set; }

    public List<EventOccurrence> Upcoming { get; set; } = [];

    public List<EventOccurrence> Birthdays { get; set; } = [];

    public List<SavingsGoal> Goals { get; set; } = [];
}

public record ChangeNotice(string ScopeId, string ActorId);
