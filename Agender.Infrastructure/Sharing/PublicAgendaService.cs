using Agender.Infrastructure.Data;
using Agender.Shared.Contracts;
using Agender.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Agender.Infrastructure.Sharing;

public class PublicAgendaService(IDbContextFactory<AgenderDbContext> contextFactory, TimeProvider time) : IPublicAgendaService
{
    public async Task<SharedAgendaDto> GetAsync(string token, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        string hash = AgendaSharingService.Hash(token);
        DateTime now = time.GetUtcNow().UtcDateTime;

        await using AgenderDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        string ownerId = await context.ShareLinks.AsNoTracking()
            .Where(l => l.TokenHash == hash && l.ExpiresAt > now)
            .Select(l => l.OwnerId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("The link is invalid or has expired.");

        string ownerName = await context.Users.Where(u => u.Id == ownerId).Select(u => u.DisplayName).FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

        return await SharedAgendaReader.ReadAsync(context, ownerId, ownerName, from, to, cancellationToken);
    }
}
