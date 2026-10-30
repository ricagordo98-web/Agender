using Agender.Infrastructure.Data;
using Agender.Infrastructure.Identity;
using Agender.Shared.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Agender.Tests.Services;

public sealed class DatabaseImporterTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"agender-import-{Guid.NewGuid():N}");
    private readonly string _livePath;

    public DatabaseImporterTests()
    {
        Directory.CreateDirectory(_folder);
        _livePath = Path.Combine(_folder, "live.db");
        CreateDatabase(_livePath, "old@casa.pt");
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public async Task ImportAsync_ValidDatabase_ReplacesDataAndKeepsBackup()
    {
        string sourcePath = Path.Combine(_folder, "source.db");
        CreateDatabase(sourcePath, "new@casa.pt");

        await Importer().ImportAsync(await File.ReadAllBytesAsync(sourcePath));

        Emails(_livePath).Should().Equal("new@casa.pt");
        string backup = Directory.GetFiles(_folder, "live.backup-before-load-*.db").Should().ContainSingle().Subject;
        Emails(backup).Should().Equal("old@casa.pt");
    }

    [Fact]
    public async Task ImportAsync_NotADatabase_ThrowsAndKeepsData()
    {
        Func<Task> act = () => Importer().ImportAsync("isto não é uma base de dados, de todo"u8.ToArray());

        await act.Should().ThrowAsync<ArgumentException>();
        Emails(_livePath).Should().Equal("old@casa.pt");
        Directory.GetFiles(_folder, "*backup*").Should().BeEmpty();
    }

    [Fact]
    public async Task ImportAsync_SqliteWithoutAgenderTables_ThrowsAndKeepsData()
    {
        string otherPath = Path.Combine(_folder, "other.db");
        using (SqliteConnection connection = new($"Data Source={otherPath};Pooling=False"))
        {
            connection.Open();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE Notes (Id INTEGER); INSERT INTO Notes VALUES (1);";
            command.ExecuteNonQuery();
        }

        Func<Task> act = async () => await Importer().ImportAsync(await File.ReadAllBytesAsync(otherPath));

        await act.Should().ThrowAsync<ArgumentException>();
        Emails(_livePath).Should().Equal("old@casa.pt");
    }

    [Fact]
    public async Task ImportAsync_NewerMigration_ThrowsAndKeepsData()
    {
        string sourcePath = Path.Combine(_folder, "newer.db");
        CreateDatabase(sourcePath, "new@casa.pt");
        using (SqliteConnection connection = new($"Data Source={sourcePath};Pooling=False"))
        {
            connection.Open();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ('29991231000000_Future', '99.0.0')";
            command.ExecuteNonQuery();
        }

        Func<Task> act = async () => await Importer().ImportAsync(await File.ReadAllBytesAsync(sourcePath));

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*versão mais recente*");
        Emails(_livePath).Should().Equal("old@casa.pt");
    }

    [Fact]
    public async Task ImportAsync_DatabaseStillUsableAfterwards()
    {
        string sourcePath = Path.Combine(_folder, "source.db");
        CreateDatabase(sourcePath, "new@casa.pt");
        DatabaseImporter importer = Importer();

        await importer.ImportAsync(await File.ReadAllBytesAsync(sourcePath));

        await using AgenderDbContext context = FactoryFor(_livePath).CreateDbContext();
        context.Users.Add(NewUser("another@casa.pt"));
        await context.SaveChangesAsync();
        (await context.Users.CountAsync()).Should().Be(2);
    }

    private DatabaseImporter Importer() => new(_livePath, FactoryFor(_livePath));

    private static IDbContextFactory<AgenderDbContext> FactoryFor(string path) => new FileFactory(path);

    private static void CreateDatabase(string path, string email)
    {
        using AgenderDbContext context = FactoryFor(path).CreateDbContext();
        context.Database.Migrate();
        context.Users.Add(NewUser(email));
        context.SaveChanges();
        SqliteConnection.ClearAllPools();
    }

    private static AgenderUser NewUser(string email) => new()
    {
        Id = email,
        UserName = email,
        NormalizedUserName = email.ToUpperInvariant(),
        Email = email,
        NormalizedEmail = email.ToUpperInvariant(),
        DisplayName = email
    };

    private static List<string> Emails(string path)
    {
        using SqliteConnection connection = new($"Data Source={path};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT Email FROM AspNetUsers ORDER BY Email";
        using SqliteDataReader reader = command.ExecuteReader();

        List<string> emails = [];
        while (reader.Read())
        {
            emails.Add(reader.GetString(0));
        }

        return emails;
    }

    private sealed class FileFactory(string path) : IDbContextFactory<AgenderDbContext>
    {
        public AgenderDbContext CreateDbContext() =>
            new(new DbContextOptionsBuilder<AgenderDbContext>().UseSqlite($"Data Source={path}").Options) { UserId = string.Empty };
    }
}
