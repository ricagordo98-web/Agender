using Agender.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;

namespace Agender.Infrastructure.Data;

public class UserDbContextFactory(IDbContextFactory<AgenderDbContext> inner, ICurrentUser currentUser) : IUserDbContextFactory
{
    public async Task<AgenderDbContext> CreateAsync(CancellationToken cancellationToken = default)
    {
        string userId = await currentUser.GetUserIdAsync(cancellationToken);
        AgenderDbContext context = await inner.CreateDbContextAsync(cancellationToken);
        context.UserId = userId;

        return context;
    }
}
