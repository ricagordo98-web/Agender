using Agender.Infrastructure.Data;
using Agender.Shared.Contracts;
using Agender.Shared.Domain;
using Agender.Shared.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Agender.Infrastructure.Services;

public class MealSettingsService(IUserDbContextFactory contextFactory, WhatsAppSender whatsApp, IDataProtectionProvider dataProtection) : IMealSettingsService
{
    private const string TestMessage = "✅ Teste do Agender: as listas de compras das refeições vão chegar aqui.";

    public async Task<MealSettingsDto> GetAsync(CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        return await LoadAsync(context, householdId, cancellationToken);
    }

    public async Task<MealSettingsDto> SaveAsync(MealSettingsDto settings, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        if (settings.People < 1 || settings.People > MealSettings.MaxPeople || !Enum.IsDefined(settings.SendDay))
        {
            throw new ArgumentException("The settings are out of range.");
        }

        MealSettings? stored = await context.MealPreferences.FirstOrDefaultAsync(s => s.HouseholdId == householdId, cancellationToken);

        if (stored is null)
        {
            stored = new MealSettings { HouseholdId = householdId };
            context.MealPreferences.Add(stored);
        }

        string pantry = string.Join('\n', Recipe.SplitLines(settings.Pantry));

        if (pantry.Length > MealSettings.PantryMaxLength)
        {
            throw new ArgumentException($"The pantry must be at most {MealSettings.PantryMaxLength} characters.");
        }

        string? apiUrl = HouseholdAccess.OptionalText(settings.WhatsAppApiUrl, "The address", MealSettings.UrlMaxLength);

        if (apiUrl is not null && !RecipeScraper.IsHttpUrl(apiUrl))
        {
            throw new ArgumentException("O endereço da Evolution API tem de começar por http:// ou https://.");
        }

        stored.People = settings.People;
        stored.Pantry = pantry;
        stored.WhatsAppApiUrl = apiUrl?.TrimEnd('/');
        stored.WhatsAppInstance = HouseholdAccess.OptionalText(settings.WhatsAppInstance, "The instance", MealSettings.TextMaxLength);
        stored.WhatsAppDestination = NormalizeDestination(HouseholdAccess.OptionalText(settings.WhatsAppDestination, "The destination", MealSettings.TextMaxLength));
        stored.SendDay = settings.SendDay;
        stored.SendTime = new TimeOnly(settings.SendTime.Hour, settings.SendTime.Minute);

        if (settings.WhatsAppApiKey is not null)
        {
            string key = settings.WhatsAppApiKey.Trim();
            stored.WhatsAppApiKey = key.Length == 0 ? null : dataProtection.CreateProtector(MealMessages.ProtectorPurpose).Protect(key);
        }

        if (settings.WhatsAppEnabled && !MealMessages.IsReady(stored))
        {
            throw new ArgumentException("Para ativar o envio automático preenche o endereço, a instância, a chave e o destino.");
        }

        stored.WhatsAppEnabled = settings.WhatsAppEnabled;
        await context.SaveChangesAsync(cancellationToken);

        return await LoadAsync(context, householdId, cancellationToken);
    }

    public async Task<RecipeSource> SaveSourceAsync(RecipeSource source, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        string sitemap = HouseholdAccess.RequiredText(source.SitemapUrl, "The sitemap", RecipeSource.UrlMaxLength);

        if (!RecipeScraper.TryCreateSafeUri(sitemap, out _))
        {
            throw new ArgumentException("O endereço do sitemap tem de ser um link completo (https://…).");
        }

        RecipeSource? target = await context.RecipeSources.FirstOrDefaultAsync(s => s.Id == source.Id && s.HouseholdId == householdId, cancellationToken);

        if (target is null)
        {
            if (await context.RecipeSources.AnyAsync(s => s.Id == source.Id, cancellationToken))
            {
                throw new NotFoundException($"Source {source.Id} was not found.");
            }

            target = new RecipeSource { Id = source.Id, HouseholdId = householdId };
            context.RecipeSources.Add(target);
        }

        string? filter = HouseholdAccess.OptionalText(source.UrlFilter, "The filter", RecipeSource.FilterMaxLength);

        if (target.SitemapUrl != sitemap || target.UrlFilter != filter || !target.IsEnabled && source.IsEnabled)
        {
            target.LastSyncAt = null;
            target.LastError = null;
        }

        target.Name = HouseholdAccess.RequiredText(source.Name, "The name", RecipeSource.NameMaxLength);
        target.SitemapUrl = sitemap;
        target.UrlFilter = filter;
        target.IsEnabled = source.IsEnabled;
        await context.SaveChangesAsync(cancellationToken);

        return target;
    }

    public async Task DeleteSourceAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        RecipeSource source = await context.RecipeSources.FirstOrDefaultAsync(s => s.Id == id && s.HouseholdId == householdId, cancellationToken)
            ?? throw new NotFoundException($"Source {id} was not found.");

        await context.CatalogRecipes.Where(c => c.SourceId == id).ExecuteDeleteAsync(cancellationToken);
        context.RecipeSources.Remove(source);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> AddSuggestedSourcesAsync(CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        List<string> existing = await context.RecipeSources.Where(s => s.HouseholdId == householdId).Select(s => s.SitemapUrl).ToListAsync(cancellationToken);
        int added = 0;

        foreach (MealCatalog.SuggestedSource suggestion in MealCatalog.SuggestedSources.Where(s => !existing.Contains(s.SitemapUrl)))
        {
            context.RecipeSources.Add(new RecipeSource
            {
                HouseholdId = householdId,
                Name = suggestion.Name,
                SitemapUrl = suggestion.SitemapUrl,
                UrlFilter = suggestion.UrlFilter
            });
            added++;
        }

        await context.SaveChangesAsync(cancellationToken);

        return added;
    }

    public async Task RefreshSourcesAsync(CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        await context.RecipeSources.Where(s => s.HouseholdId == householdId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.LastSyncAt, (DateTime?)null), cancellationToken);
        await context.CatalogRecipes.Where(c => c.HouseholdId == householdId && c.Status == CatalogRecipeStatus.Failed)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, CatalogRecipeStatus.Pending), cancellationToken);
    }

    public async Task SendTestAsync(CancellationToken cancellationToken = default)
    {
        await using AgenderDbContext context = await contextFactory.CreateAsync(cancellationToken);
        Guid householdId = await HouseholdAccess.RequireHouseholdIdAsync(context, cancellationToken);

        MealSettings? settings = await context.MealPreferences.AsNoTracking().FirstOrDefaultAsync(s => s.HouseholdId == householdId, cancellationToken);

        if (!MealMessages.IsReady(settings))
        {
            throw new ArgumentException("Preenche e guarda o endereço, a instância, a chave e o destino antes de testar.");
        }

        await MealMessages.SendAsync(whatsApp, dataProtection, settings!, TestMessage, cancellationToken);
    }

    private static async Task<MealSettingsDto> LoadAsync(AgenderDbContext context, Guid householdId, CancellationToken cancellationToken)
    {
        MealSettings settings = await context.MealPreferences.AsNoTracking().FirstOrDefaultAsync(s => s.HouseholdId == householdId, cancellationToken)
            ?? new MealSettings { HouseholdId = householdId };

        List<RecipeSource> sources = await context.RecipeSources.AsNoTracking().Where(s => s.HouseholdId == householdId)
            .OrderBy(s => s.CreatedAt).ToListAsync(cancellationToken);

        var counts = await context.CatalogRecipes.AsNoTracking().Where(c => c.HouseholdId == householdId)
            .GroupBy(c => new { c.SourceId, c.Status })
            .Select(g => new { g.Key.SourceId, g.Key.Status, Count = g.Count() })
            .ToListAsync(cancellationToken);

        foreach (RecipeSource source in sources)
        {
            source.RecipeCount = counts.Where(c => c.SourceId == source.Id && c.Status == CatalogRecipeStatus.Ready).Sum(c => c.Count);
            source.PendingCount = counts.Where(c => c.SourceId == source.Id && c.Status == CatalogRecipeStatus.Pending).Sum(c => c.Count);
        }

        return new MealSettingsDto
        {
            People = settings.People,
            Pantry = settings.Pantry ?? MealCatalog.DefaultPantry,
            WhatsAppEnabled = settings.WhatsAppEnabled,
            WhatsAppApiUrl = settings.WhatsAppApiUrl,
            WhatsAppInstance = settings.WhatsAppInstance,
            HasWhatsAppApiKey = !string.IsNullOrEmpty(settings.WhatsAppApiKey),
            WhatsAppDestination = settings.WhatsAppDestination,
            SendDay = settings.SendDay,
            SendTime = settings.SendTime,
            Sources = sources
        };
    }

    private static string? NormalizeDestination(string? destination)
    {
        if (destination is null || destination.Contains('@'))
        {
            return destination;
        }

        string digits = new([.. destination.Where(char.IsDigit)]);

        return digits.Length == 0 ? null : digits;
    }
}
