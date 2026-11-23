using Agender.Infrastructure.Data;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Agender.Tests.Services;

public sealed class TraderAgendaMigrationTests : IDisposable
{
    private const string PreviousMigration = "20261112145302_AddDailyReminders";

    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public TraderAgendaMigrationTests() => _connection.Open();

    public void Dispose() => _connection.Dispose();

    [Fact]
    public void Migrate_TraderAgenda_IsRemovedWithItsEntriesWhileOtherDataIsKept()
    {
        using AgenderDbContext context = new(new DbContextOptionsBuilder<AgenderDbContext>().UseSqlite(_connection).Options);
        IMigrator migrator = context.GetService<IMigrator>();
        migrator.Migrate(PreviousMigration);

        Agenda(context, "A-DEF", "Geral", isDefault: true, isActive: false);
        Agenda(context, "A-TRD", "Trader", isDefault: false, isActive: true);
        Agenda(context, "A-WRK", "Trabalho", isDefault: false, isActive: false);
        Project(context, "P-WIN", "A-TRD", "Ganho");
        Project(context, "P-MINE", "A-TRD", "Notas");
        Project(context, "P-WORK", "A-WRK", "Ganho");
        Entry(context, "E-TRADE", "A-TRD", "P-WIN");
        Entry(context, "E-MANUAL", "A-TRD", "P-WIN");
        Entry(context, "E-WORK", "A-WRK", "P-WORK");
        Exec(context, "INSERT INTO \"TraderTrades\" (\"Id\", \"ExternalId\", \"Symbol\", \"Name\", \"IsBuy\", \"IsClosed\", \"EntryTime\", \"EntryDate\", \"EntryPrice\", \"Quantity\", \"Fees\", \"Invested\", \"Currency\", \"Platform\", \"Notes\", \"Setup\", \"Tags\", \"Emotion\", \"TimeEntryId\", \"UpdatedAt\", \"UserId\") " +
            "VALUES ('T1', 1, 'AAPL', 'Apple', 1, 0, '2026-11-02 09:00:00', '2026-11-02', 100, 1, 0, 100, 'EUR', '', '', '', '', '', 'E-TRADE', '2026-11-02 09:00:00', 'user-a')");

        migrator.Migrate();

        Scalar(context, "SELECT COUNT(*) FROM \"TraderTrades\"").Should().Be(1);
        Scalar(context, "SELECT COUNT(*) FROM \"Agendas\" WHERE \"Id\" = 'A-TRD'").Should().Be(0);
        Scalar(context, "SELECT COUNT(*) FROM \"TimeEntries\" WHERE \"Id\" = 'E-TRADE'").Should().Be(0);
        Scalar(context, "SELECT COUNT(*) FROM \"Projects\" WHERE \"Id\" = 'P-WIN'").Should().Be(0);
        Scalar(context, "SELECT COUNT(*) FROM \"Projects\" WHERE \"Id\" = 'P-MINE' AND \"AgendaId\" = 'A-DEF'").Should().Be(1);
        Scalar(context, "SELECT COUNT(*) FROM \"TimeEntries\" WHERE \"Id\" = 'E-MANUAL' AND \"AgendaId\" = 'A-DEF' AND \"ProjectId\" IS NULL").Should().Be(1);
        Scalar(context, "SELECT COUNT(*) FROM \"TimeEntries\" WHERE \"Id\" = 'E-WORK' AND \"AgendaId\" = 'A-WRK' AND \"ProjectId\" = 'P-WORK'").Should().Be(1);
        Scalar(context, "SELECT COUNT(*) FROM \"Agendas\" WHERE \"Id\" = 'A-DEF' AND \"IsActive\" = 1").Should().Be(1);
    }

    private static void Agenda(AgenderDbContext context, string id, string name, bool isDefault, bool isActive) =>
        Exec(context, $"INSERT INTO \"Agendas\" (\"Id\", \"Name\", \"Color\", \"IsActive\", \"IsDefault\", \"IsVisible\", \"CreatedAt\", \"UserId\") VALUES ('{id}', '{name}', 'Indigo', {(isActive ? 1 : 0)}, {(isDefault ? 1 : 0)}, 1, '2026-01-01', 'user-a')");

    private static void Project(AgenderDbContext context, string id, string agendaId, string name) =>
        Exec(context, $"INSERT INTO \"Projects\" (\"Id\", \"AgendaId\", \"Name\", \"Color\", \"IsArchived\", \"CreatedAt\", \"UserId\") VALUES ('{id}', '{agendaId}', '{name}', 'Indigo', 0, '2026-01-01', 'user-a')");

    private static void Entry(AgenderDbContext context, string id, string agendaId, string projectId) =>
        Exec(context, $"INSERT INTO \"TimeEntries\" (\"Id\", \"AgendaId\", \"ProjectId\", \"Date\", \"StartMinute\", \"EndMinute\", \"Description\", \"Color\", \"SyncToGoogle\", \"GoogleSyncFailed\", \"IsCompleted\", \"UserId\") VALUES ('{id}', '{agendaId}', '{projectId}', '2026-11-02', 60, 120, 'x', 'Indigo', 0, 0, 0, 'user-a')");

    private static void Exec(AgenderDbContext context, string sql) => context.Database.ExecuteSqlRaw(sql);

    private static long Scalar(AgenderDbContext context, string sql)
    {
        using System.Data.Common.DbCommand command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        return (long)command.ExecuteScalar()!;
    }
}
