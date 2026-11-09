using System.Xml;
using Agender.Infrastructure.Data;
using Agender.Shared.Domain;
using Microsoft.EntityFrameworkCore;

namespace Agender.Infrastructure.Services;

public class RecipeCatalogSync(IDbContextFactory<AgenderDbContext> contextFactory, RecipeScraper scraper)
{
    private static readonly TimeSpan SyncInterval = TimeSpan.FromDays(7);

    public async Task SyncDueSourcesAsync(CancellationToken cancellationToken)
    {
        await using AgenderDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);
        DateTime threshold = DateTime.UtcNow - SyncInterval;

        List<RecipeSource> sources = await context.RecipeSources
            .Where(s => s.IsEnabled && (s.LastSyncAt == null || s.LastSyncAt < threshold))
            .ToListAsync(cancellationToken);

        foreach (RecipeSource source in sources)
        {
            try
            {
                List<string> urls = await scraper.ReadSitemapAsync(source.SitemapUrl, source.UrlFilter, cancellationToken);
                HashSet<string> known = [.. await context.CatalogRecipes.Where(c => c.SourceId == source.Id).Select(c => c.Url).ToListAsync(cancellationToken)];

                foreach (string url in urls.Where(u => u.Length <= Recipe.UrlMaxLength && !known.Contains(u)))
                {
                    context.CatalogRecipes.Add(new CatalogRecipe
                    {
                        HouseholdId = source.HouseholdId,
                        SourceId = source.Id,
                        Url = url,
                        Title = MealService.Truncate(RecipeScraper.TitleFromUrl(url), Recipe.TitleMaxLength),
                        Status = CatalogRecipeStatus.Pending
                    });
                }

                source.LastError = urls.Count == 0 ? "Não encontrei páginas de receitas neste sitemap. Confirma o endereço e o filtro." : null;
            }
            catch (Exception ex) when (ex is HttpRequestException or XmlException or InvalidOperationException || ex is TaskCanceledException && !cancellationToken.IsCancellationRequested)
            {
                source.LastError = MealService.Truncate($"Não foi possível ler o sitemap: {ex.Message}", MealWeek.ErrorMaxLength);
            }

            source.LastSyncAt = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<bool> EnrichNextAsync(CancellationToken cancellationToken)
    {
        await using AgenderDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        List<Guid> enabled = await context.RecipeSources.Where(s => s.IsEnabled).Select(s => s.Id).ToListAsync(cancellationToken);

        CatalogRecipe? next = await context.CatalogRecipes
            .Where(c => c.Status == CatalogRecipeStatus.Pending && enabled.Contains(c.SourceId))
            .OrderBy(c => c.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (next is null)
        {
            return false;
        }

        await EnrichAsync(next, scraper, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return true;
    }

    internal static async Task EnrichAsync(CatalogRecipe item, RecipeScraper scraper, CancellationToken cancellationToken)
    {
        try
        {
            ScrapedRecipe? scraped = await scraper.FetchAsync(item.Url, cancellationToken);

            if (scraped is null)
            {
                item.Status = CatalogRecipeStatus.NotRecipe;
            }
            else
            {
                item.Title = MealService.Truncate(scraped.Title, Recipe.TitleMaxLength);
                item.ImageUrl = scraped.ImageUrl is { Length: <= Recipe.UrlMaxLength } ? scraped.ImageUrl : null;
                item.Category = scraped.Category is null ? null : MealService.Truncate(scraped.Category, Recipe.CategoryMaxLength);
                item.Minutes = scraped.Minutes is > 0 and <= Recipe.MaxMinutes ? scraped.Minutes : null;
                item.Servings = scraped.Servings;
                item.Ingredients = MealService.Truncate(string.Join('\n', scraped.Ingredients), Recipe.IngredientsMaxLength);
                item.Instructions = scraped.Instructions is null ? null : MealService.Truncate(scraped.Instructions, Recipe.InstructionsMaxLength);
                item.Status = scraped.Ingredients.Count == 0 ? CatalogRecipeStatus.NotRecipe : CatalogRecipeStatus.Ready;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            item.Status = CatalogRecipeStatus.Failed;
        }

        item.FetchedAt = DateTime.UtcNow;
    }
}
