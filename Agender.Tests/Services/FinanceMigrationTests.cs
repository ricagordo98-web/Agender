using Agender.Infrastructure.Data;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Agender.Tests.Services;

public sealed class FinanceMigrationTests : IDisposable
{
    private const string PreviousMigration = "AddHouseholdInvites";
    private const string Household = "11111111-1111-1111-1111-111111111111";

    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public FinanceMigrationTests() => _connection.Open();

    public void Dispose() => _connection.Dispose();

    [Fact]
    public void Migrate_ExistingTransactions_BecomePeopleAndKeepTheirOwner()
    {
        using AgenderDbContext context = new(new DbContextOptionsBuilder<AgenderDbContext>().UseSqlite(_connection).Options);
        IMigrator migrator = context.GetService<IMigrator>();
        string previous = context.GetService<IMigrationsAssembly>().Migrations.Keys.Single(k => k.EndsWith(PreviousMigration, StringComparison.Ordinal));
        migrator.Migrate(previous);

        Exec(context, "INSERT INTO \"AspNetUsers\" (\"Id\", \"UserName\", \"NormalizedUserName\", \"Email\", \"NormalizedEmail\", \"EmailConfirmed\", \"PasswordHash\", \"SecurityStamp\", \"ConcurrencyStamp\", \"PhoneNumberConfirmed\", \"TwoFactorEnabled\", \"LockoutEnabled\", \"AccessFailedCount\", \"DisplayName\") VALUES ('user-a', 'a', 'A', 'a@x.pt', 'A@X.PT', 1, 'x', 'x', 'x', 0, 0, 0, 0, 'Ana Silva')");
        Exec(context, $"INSERT INTO \"Households\" (\"Id\", \"Name\", \"InviteCode\", \"CreatedAt\") VALUES ('{Household}', 'Casa', 'ABC234', '2026-01-01')");
        Exec(context, $"INSERT INTO \"HouseholdMembers\" (\"Id\", \"HouseholdId\", \"UserId\", \"Color\", \"JoinedAt\") VALUES ('22222222-2222-2222-2222-222222222222', '{Household}', 'user-a', '#8B9CFF', '2026-01-01')");
        Exec(context, $"INSERT INTO \"FinanceTransactions\" (\"Id\", \"HouseholdId\", \"Type\", \"Amount\", \"Category\", \"Date\", \"MemberId\", \"MemberName\", \"CreatedAt\") VALUES ('33333333-3333-3333-3333-333333333333', '{Household}', 'Expense', 10, 'Lazer', '2026-10-01', 'user-a', 'Ana', '2026-10-01')");
        Exec(context, $"INSERT INTO \"FinanceTransactions\" (\"Id\", \"HouseholdId\", \"Type\", \"Amount\", \"Category\", \"Date\", \"MemberId\", \"MemberName\", \"CreatedAt\") VALUES ('44444444-4444-4444-4444-444444444444', '{Household}', 'Income', 20, 'Salário', '2026-10-02', 'user-a', 'Ana', '2026-10-02')");
        Exec(context, $"INSERT INTO \"FinanceTransactions\" (\"Id\", \"HouseholdId\", \"Type\", \"Amount\", \"Category\", \"Date\", \"CreatedAt\") VALUES ('55555555-5555-5555-5555-555555555555', '{Household}', 'Expense', 5, 'Outros', '2026-10-03', '2026-10-03')");

        migrator.Migrate();

        Scalar(context, "SELECT COUNT(*) FROM \"FinancePeople\"").Should().Be(1);
        Scalar(context, "SELECT COUNT(*) FROM \"FinancePeople\" WHERE \"Name\" = 'Ana Silva' AND \"MemberUserId\" = 'user-a' AND \"Color\" = '#8B9CFF'").Should().Be(1);
        Scalar(context, "SELECT COUNT(*) FROM \"FinanceTransactions\" t JOIN \"FinancePeople\" p ON p.\"Id\" = t.\"PersonId\"").Should().Be(2);
        Scalar(context, "SELECT COUNT(*) FROM \"FinanceTransactions\" WHERE \"PersonId\" IS NULL").Should().Be(1);
        Scalar(context, "SELECT COUNT(*) FROM \"FinanceTransactions\"").Should().Be(3);
    }

    private static void Exec(AgenderDbContext context, string sql) => context.Database.ExecuteSqlRaw(sql);

    private static long Scalar(AgenderDbContext context, string sql)
    {
        using System.Data.Common.DbCommand command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;

        return Convert.ToInt64(command.ExecuteScalar());
    }
}
