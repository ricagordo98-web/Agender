using Agender.Infrastructure.Data;
using Agender.Shared.Domain;
using Microsoft.EntityFrameworkCore;

namespace Agender.Infrastructure.Services;

internal static class HouseholdAccess
{
    public const string RequiredMessage = "Cria uma casa ou junta-te a uma antes de continuares.";

    public static async Task<Guid> RequireHouseholdIdAsync(AgenderDbContext context, CancellationToken cancellationToken)
    {
        string userId = context.UserId;

        return await context.HouseholdMembers
            .AsNoTracking()
            .Where(m => m.UserId == userId)
            .Select(m => (Guid?)m.HouseholdId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new ArgumentException(RequiredMessage);
    }

    public static async Task<(string DisplayName, string Email)> GetCurrentUserAsync(AgenderDbContext context, CancellationToken cancellationToken)
    {
        string userId = context.UserId;

        var user = await context.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.DisplayName, u.Email })
            .FirstOrDefaultAsync(cancellationToken);

        return (user?.DisplayName ?? string.Empty, (user?.Email ?? string.Empty).ToLowerInvariant());
    }

    public static async Task<(string? MemberId, string? MemberName)> ResolveMemberAsync(AgenderDbContext context, Guid householdId, string? memberId, bool fallbackToCurrent, CancellationToken cancellationToken)
    {
        string? candidate = string.IsNullOrWhiteSpace(memberId) ? (fallbackToCurrent ? context.UserId : null) : memberId;

        if (candidate is null)
        {
            return (null, null);
        }

        bool isMember = await context.HouseholdMembers.AnyAsync(m => m.HouseholdId == householdId && m.UserId == candidate, cancellationToken);

        if (!isMember)
        {
            throw new ArgumentException("The member does not belong to the household.");
        }

        string? name = await context.Users.Where(u => u.Id == candidate).Select(u => u.DisplayName).FirstOrDefaultAsync(cancellationToken);

        return (candidate, name);
    }

    public static string RequiredText(string? value, string label, int maxLength)
    {
        string trimmed = (value ?? string.Empty).Trim();

        if (trimmed.Length == 0)
        {
            throw new ArgumentException($"{label} is required.");
        }

        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"{label} must be at most {maxLength} characters.");
        }

        return trimmed;
    }

    public static string? OptionalText(string? value, string label, int maxLength)
    {
        string trimmed = (value ?? string.Empty).Trim();

        if (trimmed.Length == 0)
        {
            return null;
        }

        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"{label} must be at most {maxLength} characters.");
        }

        return trimmed;
    }

    public static decimal? OptionalMoney(decimal? value, string label)
    {
        if (value is null)
        {
            return null;
        }

        if (value < 0 || value > 100_000_000m)
        {
            throw new ArgumentException($"{label} is out of range.");
        }

        return Math.Round(value.Value, 2);
    }

    public static string NormalizeColor(string? value, string fallback) => ColorHex.Normalize(value) ?? fallback;
}
