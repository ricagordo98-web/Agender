using System.Collections.Concurrent;
using Agender.Infrastructure.Data;
using Agender.Shared.Domain;
using Microsoft.EntityFrameworkCore;

namespace Agender.Infrastructure.Services;

internal static class CatalogIndex
{
    public sealed record Row(Guid Id, string Title, string? Category, CatalogRecipeStatus Status, DateTime? FetchedAt);

    private sealed record Entry(DateTime? FetchedAt, RecipeCourse Course, List<string>? Keys);

    private static readonly ConcurrentDictionary<Guid, Entry> Cache = new();

    public static RecipeCourse CourseOf(Row row)
    {
        if (Cache.TryGetValue(row.Id, out Entry? entry) && entry.FetchedAt == row.FetchedAt)
        {
            return entry.Course;
        }

        RecipeCourse course = RecipeClassifier.Course(row.Title, row.Category);
        Cache[row.Id] = new Entry(row.FetchedAt, course, null);

        return course;
    }

    public static List<string> KeysOf(Row row) =>
        Cache.TryGetValue(row.Id, out Entry? entry) && entry.FetchedAt == row.FetchedAt ? entry.Keys ?? [] : [];

    public static async Task EnsureKeysAsync(AgenderDbContext context, List<Row> rows, CancellationToken cancellationToken)
    {
        List<Guid> missing = [.. rows.Where(r => !Cache.TryGetValue(r.Id, out Entry? entry) || entry.FetchedAt != r.FetchedAt || entry.Keys is null).Select(r => r.Id)];

        if (missing.Count == 0)
        {
            return;
        }

        Dictionary<Guid, Row> byId = rows.ToDictionary(r => r.Id);

        foreach (Guid[] chunk in missing.Chunk(500))
        {
            var loaded = await context.CatalogRecipes.AsNoTracking()
                .Where(c => chunk.Contains(c.Id))
                .Select(c => new { c.Id, c.Ingredients })
                .ToListAsync(cancellationToken);

            foreach (var item in loaded)
            {
                Row row = byId[item.Id];
                Cache[item.Id] = new Entry(row.FetchedAt, RecipeClassifier.Course(row.Title, row.Category), HomeMatcher.KeysOf(Recipe.SplitLines(item.Ingredients)));
            }
        }
    }
}
