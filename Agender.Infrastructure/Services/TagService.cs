using Agender.Infrastructure.Data;
using Agender.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Agender.Infrastructure.Services;

public class TagService(IAgendaDbContextFactory contextFactory) : ITagService
{
    public async Task<IReadOnlyList<string>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);

        return await context.Tags
            .AsNoTracking()
            .OrderBy(t => t.NormalizedName)
            .Select(t => t.Name)
            .ToListAsync(cancellationToken);
    }
}
