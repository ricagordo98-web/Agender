using Agender.Infrastructure.Identity;
using Agender.Infrastructure.Realtime;
using Microsoft.EntityFrameworkCore;

namespace Agender.Infrastructure.Data;

public class UserDbContextFactory(IDbContextFactory<AgenderDbContext> inner, ICurrentUser currentUser, IChangePublisher? publisher = null) : IUserDbContextFactory
{
    public async Task<AgenderDbContext> CreateAsync(CancellationToken cancellationToken = default)
    {
        string userId = await currentUser.GetUserIdAsync(cancellationToken);
        AgenderDbContext context = await inner.CreateDbContextAsync(cancellationToken);
        context.UserId = userId;
        context.ActorId = userId;
        context.ChangePublisher = publisher;

        return context;
    }
}
