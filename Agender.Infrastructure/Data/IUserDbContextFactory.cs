namespace Agender.Infrastructure.Data;

public interface IUserDbContextFactory
{
    Task<AgenderDbContext> CreateAsync(CancellationToken cancellationToken = default);
}
