using Agender.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Agender.Tests.Services;

public sealed class TestDb : IUserDbContextFactory, IDisposable
{
    public const string DefaultUserId = "user-a";

    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly DbContextOptions<AgenderDbContext> _options;
    private readonly string _userId;

    public TestDb(string userId = DefaultUserId)
    {
        _userId = userId;
        _connection.Open();
        _options = new DbContextOptionsBuilder<AgenderDbContext>().UseSqlite(_connection).Options;

        using AgenderDbContext context = Create(string.Empty);
        context.Database.EnsureCreated();
    }

    public Task<AgenderDbContext> CreateAsync(CancellationToken cancellationToken = default) => Task.FromResult(Create(_userId));

    public IUserDbContextFactory ForUser(string userId) => new UserScopedFactory(this, userId);

    public AgenderDbContext Create(string userId) => new(_options) { UserId = userId };

    public void Dispose() => _connection.Dispose();

    private sealed class UserScopedFactory(TestDb db, string userId) : IUserDbContextFactory
    {
        public Task<AgenderDbContext> CreateAsync(CancellationToken cancellationToken = default) => Task.FromResult(db.Create(userId));
    }
}
