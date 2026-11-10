using Agender.Infrastructure.Data;
using Agender.Shared.Domain;
using Microsoft.EntityFrameworkCore;

namespace Agender.Infrastructure.Services;

public static class MealPrices
{
    private const int MaxHistory = 3000;

    public sealed record KnownPrice(string Key, string? PriceUnit, decimal Price);

    public sealed record Estimate(decimal Total, int Priced, int Count);

    internal static async Task<List<KnownPrice>> LoadAsync(AgenderDbContext context, Guid householdId, CancellationToken cancellationToken)
    {
        var history = await context.ShoppingItems.AsNoTracking()
            .Where(i => i.Price != null && context.ShoppingLists.Any(l => l.Id == i.ListId && l.HouseholdId == householdId))
            .OrderByDescending(i => i.CreatedAt)
            .Take(MaxHistory)
            .Select(i => new { i.Name, i.Unit, i.Price })
            .ToListAsync(cancellationToken);

        return [.. history
            .Where(i => i.Price > 0)
            .Select(i => new KnownPrice(IngredientParser.KeyOf(i.Name), CasaCatalog.PriceUnit(i.Unit), i.Price!.Value))
            .Where(p => p.Key.Length > 0)
            .DistinctBy(p => (p.Key, p.PriceUnit))];
    }

    public static decimal? PriceFor(IReadOnlyList<KnownPrice> book, string key, string? unit)
    {
        string? priceUnit = CasaCatalog.PriceUnit(unit);

        return book.FirstOrDefault(p => p.PriceUnit == priceUnit && IngredientParser.SameIngredient(key, p.Key))?.Price;
    }

    public static Estimate EstimateOf(IEnumerable<MealIngredientLine> lines, IReadOnlyList<KnownPrice> book)
    {
        decimal total = 0;
        int priced = 0;
        int count = 0;

        foreach (MealIngredientLine line in lines.Where(l => l.Include))
        {
            count++;

            if (PriceFor(book, line.Key, line.Unit) is decimal price)
            {
                priced++;
                total += CasaCatalog.LineCost(price, line.Quantity ?? 1, line.Unit);
            }
        }

        return new Estimate(Math.Round(total, 2), priced, count);
    }
}
