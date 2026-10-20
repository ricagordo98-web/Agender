using Agender.Infrastructure.Data;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Agender.Tests.Services;

public sealed class AgendaMigrationTests : IDisposable
{
    private const string PreviousMigration = "20261019161501_AddCustomColors";
    private const string Empty = "00000000-0000-0000-0000-000000000000";

    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public AgendaMigrationTests() => _connection.Open();

    public void Dispose() => _connection.Dispose();

    [Fact]
    public void Migrate_ExistingData_GetsOneDefaultAgendaPerUserAndEverythingIsAssigned()
    {
        using AgenderDbContext context = new(new DbContextOptionsBuilder<AgenderDbContext>().UseSqlite(_connection).Options);
        IMigrator migrator = context.GetService<IMigrator>();
        migrator.Migrate(PreviousMigration);

        Exec(context, "INSERT INTO \"Projects\" (\"Id\", \"Name\", \"Color\", \"IsArchived\", \"CreatedAt\", \"UserId\") VALUES ('P1', 'Site A', 'Indigo', 0, '2026-01-01', 'user-a')");
        Exec(context, "INSERT INTO \"Projects\" (\"Id\", \"Name\", \"Color\", \"IsArchived\", \"CreatedAt\", \"UserId\") VALUES ('P2', 'Site B', 'Teal', 0, '2026-01-01', 'user-b')");
        Exec(context, "INSERT INTO \"Tasks\" (\"Id\", \"Title\", \"Color\", \"IsCompleted\", \"CreatedAt\", \"UserId\") VALUES ('T1', 'Tarefa', 'Indigo', 0, '2026-01-01', 'user-a')");
        Exec(context, "INSERT INTO \"TimeEntries\" (\"Id\", \"Date\", \"StartMinute\", \"EndMinute\", \"Description\", \"Color\", \"SyncToGoogle\", \"GoogleSyncFailed\", \"IsCompleted\", \"UserId\") VALUES ('E1', '2026-10-12', 60, 120, 'x', 'Indigo', 0, 0, 0, 'user-a')");
        Exec(context, "INSERT INTO \"TimeEntries\" (\"Id\", \"Date\", \"StartMinute\", \"EndMinute\", \"Description\", \"Color\", \"SyncToGoogle\", \"GoogleSyncFailed\", \"IsCompleted\", \"UserId\") VALUES ('E2', '2026-10-12', 60, 120, 'y', 'Indigo', 0, 0, 0, 'user-b')");

        migrator.Migrate();

        Scalar(context, "SELECT COUNT(*) FROM \"Agendas\"").Should().Be(2);
        Scalar(context, "SELECT COUNT(*) FROM \"Agendas\" WHERE \"IsDefault\" = 1 AND \"IsActive\" = 1 AND \"IsVisible\" = 1 AND \"Name\" = 'Geral'").Should().Be(2);
        foreach (string table in new[] { "Projects", "Tasks", "TimeEntries" })
        {
            Scalar(context, $"SELECT COUNT(*) FROM \"{table}\" WHERE \"AgendaId\" = '{Empty}'").Should().Be(0, table);
            Scalar(context, $"SELECT COUNT(*) FROM \"{table}\" t JOIN \"Agendas\" a ON a.\"Id\" = t.\"AgendaId\" AND a.\"UserId\" = t.\"UserId\"").Should().Be(
                Scalar(context, $"SELECT COUNT(*) FROM \"{table}\""), table);
        }
    }

    private static void Exec(AgenderDbContext context, string sql) => context.Database.ExecuteSqlRaw(sql);

    private static long Scalar(AgenderDbContext context, string sql)
    {
        using System.Data.Common.DbCommand command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;

        return Convert.ToInt64(command.ExecuteScalar());
    }
}
