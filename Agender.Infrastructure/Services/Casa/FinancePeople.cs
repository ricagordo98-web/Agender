using System.Globalization;
using System.Text;
using Agender.Infrastructure.Data;
using Agender.Shared.Domain;
using Microsoft.EntityFrameworkCore;

namespace Agender.Infrastructure.Services;

internal static class FinancePeople
{
    public static string Key(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        StringBuilder builder = new();

        foreach (char character in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString();
    }

    public static async Task<List<FinancePerson>> EnsureAsync(AgenderDbContext context, Guid householdId, CancellationToken cancellationToken)
    {
        List<FinancePerson> people = await Query(context, householdId).ToListAsync(cancellationToken);

        if (people.Count > 0)
        {
            return people;
        }

        var members = await (
            from member in context.HouseholdMembers.AsNoTracking()
            join user in context.Users.AsNoTracking() on member.UserId equals user.Id
            where member.HouseholdId == householdId
            orderby member.JoinedAt
            select new { member.UserId, user.DisplayName, member.Color }).ToListAsync(cancellationToken);

        int order = 0;

        foreach (var member in members)
        {
            context.FinancePeople.Add(new FinancePerson
            {
                HouseholdId = householdId,
                Name = string.IsNullOrWhiteSpace(member.DisplayName) ? "Pessoa" : member.DisplayName.Trim(),
                Color = member.Color,
                MemberUserId = member.UserId,
                Order = order++
            });
        }

        await context.SaveChangesAsync(cancellationToken);

        return await Query(context, householdId).ToListAsync(cancellationToken);
    }

    public static async Task OnMemberJoinedAsync(AgenderDbContext context, Guid householdId, string userId, string displayName, string color, CancellationToken cancellationToken)
    {
        List<FinancePerson> people = await context.FinancePeople.Where(p => p.HouseholdId == householdId).ToListAsync(cancellationToken);

        if (people.Count == 0 || people.Any(p => p.MemberUserId == userId))
        {
            return;
        }

        string full = Key(displayName);
        string first = Key(displayName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault());
        FinancePerson? match = people.FirstOrDefault(p => p.MemberUserId is null && (Key(p.Name) == full || Key(p.Name) == first));

        if (match is not null)
        {
            match.MemberUserId = userId;
        }
        else if (people.All(p => p.MemberUserId is not null))
        {
            context.FinancePeople.Add(new FinancePerson
            {
                HouseholdId = householdId,
                Name = string.IsNullOrWhiteSpace(displayName) ? "Pessoa" : displayName.Trim(),
                Color = color,
                MemberUserId = userId,
                Order = people.Max(p => p.Order) + 1
            });
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static IQueryable<FinancePerson> Query(AgenderDbContext context, Guid householdId) =>
        context.FinancePeople.Where(p => p.HouseholdId == householdId).OrderBy(p => p.Order).ThenBy(p => p.CreatedAt);
}
