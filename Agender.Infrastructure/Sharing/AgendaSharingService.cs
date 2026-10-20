using System.Security.Cryptography;
using System.Text;
using Agender.Infrastructure.Data;
using Agender.Infrastructure.Identity;
using Agender.Shared.Contracts;
using Agender.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Agender.Infrastructure.Sharing;

public class AgendaSharingService(IUserDbContextFactory contextFactory, TimeProvider time, IPublicUrlProvider? publicUrl = null) : IAgendaSharingService
{
    public static readonly TimeSpan LinkLifetime = TimeSpan.FromDays(2);

    private const int TokenBytes = 32;
    private const string TunnelFailedNotice = "Não foi possível criar o acesso público. O link só funciona neste computador.";

    public async Task<IReadOnlyList<ShareDto>> GetSharesAsync(CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        string me = context.UserId;

        List<ShareDto> shares = await (
            from share in context.AgendaShares.AsNoTracking()
            join user in context.Users on share.ViewerId equals user.Id
            where share.OwnerId == me
            orderby share.CreatedAt
            select new ShareDto(share.Id, user.Email ?? string.Empty, user.DisplayName)).ToListAsync(cancellationToken);

        return shares;
    }

    public async Task<ShareDto> ShareAsync(string email, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        string me = context.UserId;
        string normalized = email.Trim().ToUpperInvariant();

        AgenderUser viewer = await context.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalized, cancellationToken)
            ?? throw new ArgumentException("There is no user with that email.");

        if (viewer.Id == me)
        {
            throw new ArgumentException("You cannot share your agenda with yourself.");
        }

        AgendaShare? existing = await context.AgendaShares.FirstOrDefaultAsync(s => s.OwnerId == me && s.ViewerId == viewer.Id, cancellationToken);

        if (existing is null)
        {
            existing = new AgendaShare { OwnerId = me, ViewerId = viewer.Id, CreatedAt = time.GetUtcNow().UtcDateTime };
            context.AgendaShares.Add(existing);
            await context.SaveChangesAsync(cancellationToken);
        }

        return new ShareDto(existing.Id, viewer.Email ?? string.Empty, viewer.DisplayName);
    }

    public async Task RevokeAsync(Guid shareId, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        string me = context.UserId;

        await context.AgendaShares.Where(s => s.Id == shareId && s.OwnerId == me).ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SharedWithMeDto>> GetSharedWithMeAsync(CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        string me = context.UserId;

        List<SharedWithMeDto> shares = await (
            from share in context.AgendaShares.AsNoTracking()
            join user in context.Users on share.OwnerId equals user.Id
            where share.ViewerId == me
            orderby user.DisplayName
            select new SharedWithMeDto(share.Id, user.Email ?? string.Empty, user.DisplayName)).ToListAsync(cancellationToken);

        return shares;
    }

    public async Task<SharedAgendaDto> GetSharedAgendaAsync(Guid shareId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        string me = context.UserId;

        AgendaShare share = await context.AgendaShares.AsNoTracking().FirstOrDefaultAsync(s => s.Id == shareId && s.ViewerId == me, cancellationToken)
            ?? throw new NotFoundException($"Share {shareId} was not found.");

        string ownerName = await context.Users.Where(u => u.Id == share.OwnerId).Select(u => u.DisplayName).FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

        return await SharedAgendaReader.ReadAsync(context, share.OwnerId, ownerName, from, to, cancellationToken);
    }

    public async Task<ShareLinkStatusDto> GetLinkAsync(CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        string me = context.UserId;
        DateTime now = time.GetUtcNow().UtcDateTime;

        DateTime? expiresAt = await context.ShareLinks.AsNoTracking()
            .Where(l => l.OwnerId == me && l.ExpiresAt > now)
            .Select(l => (DateTime?)l.ExpiresAt)
            .FirstOrDefaultAsync(cancellationToken);

        return new ShareLinkStatusDto(expiresAt is not null, expiresAt);
    }

    public async Task<NewShareLinkDto> CreateLinkAsync(CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        string me = context.UserId;
        DateTime now = time.GetUtcNow().UtcDateTime;
        string token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(TokenBytes)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        await context.ShareLinks.Where(l => l.OwnerId == me || l.ExpiresAt <= now).ExecuteDeleteAsync(cancellationToken);

        ShareLink link = new() { OwnerId = me, TokenHash = Hash(token), CreatedAt = now, ExpiresAt = now + LinkLifetime };
        context.ShareLinks.Add(link);
        await context.SaveChangesAsync(cancellationToken);

        (string? baseUrl, string? notice) = await AcquirePublicUrlAsync(link.ExpiresAt, cancellationToken);

        return new NewShareLinkDto(token, link.ExpiresAt, baseUrl, notice);
    }

    public async Task RevokeLinkAsync(CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        string me = context.UserId;

        await context.ShareLinks.Where(l => l.OwnerId == me).ExecuteDeleteAsync(cancellationToken);

        DateTime now = time.GetUtcNow().UtcDateTime;

        if (publicUrl is not null && !await context.ShareLinks.AnyAsync(l => l.ExpiresAt > now, cancellationToken))
        {
            await publicUrl.ReleaseAsync();
        }
    }

    private async Task<(string? BaseUrl, string? Notice)> AcquirePublicUrlAsync(DateTime expiresAt, CancellationToken cancellationToken)
    {
        if (publicUrl is null)
        {
            return (null, null);
        }

        try
        {
            return (await publicUrl.AcquireAsync(expiresAt, cancellationToken), null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return (null, TunnelFailedNotice);
        }
    }

    internal static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
