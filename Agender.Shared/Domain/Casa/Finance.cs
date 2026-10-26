namespace Agender.Shared.Domain;

public enum TransactionType { Income = 0, Expense = 1, Saving = 2 }

public class FinanceTransaction
{
    public const int DescriptionMaxLength = 200;

    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid HouseholdId { get; set; }

    public TransactionType Type { get; set; } = TransactionType.Expense;

    public decimal Amount { get; set; }

    public string Category { get; set; } = CasaCatalog.DefaultExpenseCategory;

    public string? Description { get; set; }

    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    public Guid? PersonId { get; set; }

    public bool IsTransfer { get; set; }

    public bool IsWithdrawal { get; set; }

    public bool IsImported { get; set; }

    public Guid? GoalId { get; set; }

    public Guid? SavingsAccountId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public decimal SignedValue => Type switch
    {
        TransactionType.Income => Amount,
        TransactionType.Expense => -Amount,
        _ => IsWithdrawal ? Amount : -Amount
    };
}

public class FinancePerson
{
    public const int NameMaxLength = 60;

    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid HouseholdId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Color { get; set; } = CasaCatalog.MemberColors[0];

    public int Order { get; set; }

    public string? MemberUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class SavingsAccount
{
    public const int NameMaxLength = 80;
    public const int BankMaxLength = 60;

    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid HouseholdId { get; set; }

    public string Name { get; set; } = string.Empty;

    public Guid? PersonId { get; set; }

    public string? Bank { get; set; }

    public decimal Value { get; set; }

    public decimal InitialValue { get; set; }

    public int Order { get; set; }
}

public class SavingsSnapshot
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid HouseholdId { get; set; }

    public int Year { get; set; }

    public Guid PersonId { get; set; }

    public decimal Value { get; set; }
}

public class SavingsGoal
{
    public const int NameMaxLength = 80;

    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid HouseholdId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Emoji { get; set; } = "🎯";

    public decimal Target { get; set; }

    public decimal Current { get; set; }

    public DateOnly? Deadline { get; set; }

    public string Color { get; set; } = CasaCatalog.ListColors[2];

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public double Progress => Target <= 0 ? 0 : Math.Min(1, (double)(Current / Target));
}
