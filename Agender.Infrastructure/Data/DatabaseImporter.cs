using Agender.Shared.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Agender.Infrastructure.Data;

public sealed class DatabaseImporter(string databasePath, IDbContextFactory<AgenderDbContext> factory)
{
    public const long MaxBytes = 100L * 1024 * 1024;

    private static readonly byte[] SqliteHeader = "SQLite format 3\0"u8.ToArray();
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public async Task ImportAsync(byte[] database, CancellationToken cancellationToken = default)
    {
        if (database.Length < SqliteHeader.Length || !database.AsSpan(0, SqliteHeader.Length).SequenceEqual(SqliteHeader))
        {
            throw new ArgumentException("O ficheiro não é uma base de dados do Agender.");
        }

        if (database.Length > MaxBytes)
        {
            throw new ArgumentException("O ficheiro é demasiado grande.");
        }

        await Gate.WaitAsync(cancellationToken);
        string uploadPath = $"{databasePath}.upload-{Guid.NewGuid():N}";

        try
        {
            await File.WriteAllBytesAsync(uploadPath, database, cancellationToken);
            Validate(uploadPath);
            Replace(uploadPath);

            await using AgenderDbContext context = await factory.CreateDbContextAsync(cancellationToken);
            await context.Database.MigrateAsync(cancellationToken);
        }
        catch (SqliteException ex)
        {
            throw new PersistenceException("The database could not be imported.", ex);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            TryDelete(uploadPath);
            Gate.Release();
        }
    }

    private void Validate(string uploadPath)
    {
        using AgenderDbContext context = factory.CreateDbContext();
        HashSet<string> known = [.. context.Database.GetMigrations()];

        try
        {
            using SqliteConnection connection = Open(uploadPath, SqliteOpenMode.ReadOnly);

            if (Scalar(connection, "PRAGMA integrity_check") is not "ok")
            {
                throw new ArgumentException("O ficheiro está danificado.");
            }

            List<string> applied = [];
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = "SELECT MigrationId FROM __EFMigrationsHistory";
                using SqliteDataReader reader = command.ExecuteReader();

                while (reader.Read())
                {
                    applied.Add(reader.GetString(0));
                }
            }

            if (applied.Count == 0)
            {
                throw new ArgumentException("O ficheiro não é uma base de dados do Agender.");
            }

            if (applied.Any(migration => !known.Contains(migration)))
            {
                throw new ArgumentException("O ficheiro vem de uma versão mais recente do Agender. Atualiza a aplicação primeiro.");
            }
        }
        catch (SqliteException)
        {
            throw new ArgumentException("O ficheiro não é uma base de dados do Agender.");
        }
    }

    private void Replace(string uploadPath)
    {
        string backupPath = Path.Combine(
            Path.GetDirectoryName(databasePath) ?? string.Empty,
            $"{Path.GetFileNameWithoutExtension(databasePath)}.backup-before-load-{DateTime.Now:yyyyMMdd-HHmmss}.db");

        using (SqliteConnection live = Open(databasePath, SqliteOpenMode.ReadOnly))
        using (SqliteConnection backup = Open(backupPath, SqliteOpenMode.ReadWriteCreate))
        {
            live.BackupDatabase(backup);
        }

        using SqliteConnection source = Open(uploadPath, SqliteOpenMode.ReadOnly);
        using SqliteConnection destination = Open(databasePath, SqliteOpenMode.ReadWrite);
        source.BackupDatabase(destination);
    }

    private static SqliteConnection Open(string path, SqliteOpenMode mode)
    {
        SqliteConnection connection = new(new SqliteConnectionStringBuilder { DataSource = path, Mode = mode, Pooling = false }.ToString());
        connection.Open();

        return connection;
    }

    private static string? Scalar(SqliteConnection connection, string sql)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;

        return command.ExecuteScalar()?.ToString();
    }

    private static void TryDelete(string path)
    {
        foreach (string file in new[] { path, $"{path}-wal", $"{path}-shm", $"{path}-journal" })
        {
            try
            {
                File.Delete(file);
            }
            catch (IOException)
            {
            }
        }
    }
}
