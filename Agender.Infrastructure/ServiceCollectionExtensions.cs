using Agender.Infrastructure.Data;
using Agender.Infrastructure.Google;
using Agender.Infrastructure.Identity;
using Agender.Infrastructure.Realtime;
using Agender.Shared.Abstractions;
using Agender.Infrastructure.Sharing;
using Agender.Infrastructure.Services;
using Agender.Infrastructure.Trader;
using Agender.Shared.Services;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Agender.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAgenderInfrastructure<TCurrentUser>(this IServiceCollection services, string databasePath)
        where TCurrentUser : class, ICurrentUser
    {
        string? directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        services.AddDbContextFactory<AgenderDbContext>(options => options.UseSqlite($"Data Source={databasePath}").AddInterceptors(new WriteTrackingInterceptor()));
        services.AddSingleton(provider => new DatabaseImporter(databasePath, provider.GetRequiredService<IDbContextFactory<AgenderDbContext>>()));
        services.AddScoped<ICurrentUser, TCurrentUser>();
        services.AddScoped<IUserDbContextFactory, UserDbContextFactory>();
        services.AddScoped<IAgendaDbContextFactory, AgendaDbContextFactory>();
        services.AddSingleton<ChangeBus>();
        services.AddSingleton<IChangePublisher>(provider => provider.GetRequiredService<ChangeBus>());
        services.AddScoped<IChangeFeed, InProcessChangeFeed>();
        services.AddScoped<TimeEntryService>();
        services.AddScoped<ITimeEntryService>(provider => new GoogleSyncingTimeEntryService(
            provider.GetRequiredService<TimeEntryService>(),
            provider.GetRequiredService<IGoogleEntrySync>(),
            provider.GetRequiredService<IAgendaScope>()));
        services.AddScoped<ITaskService, TaskService>();
        services.AddScoped<IDayNoteService, DayNoteService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<ITagService, TagService>();
        services.AddScoped<ITimerService, TimerService>();
        services.AddScoped<IUserSettingsService, UserSettingsService>();
        services.AddScoped<IAgendaService, AgendaService>();
        services.AddScoped<IHouseholdService, HouseholdService>();
        services.AddScoped<IShoppingService, ShoppingService>();
        services.AddScoped<IHomeProjectService, HomeProjectService>();
        services.AddScoped<IWeddingService, WeddingService>();
        services.AddScoped<ICleaningService, CleaningService>();
        services.AddScoped<IMealService, MealService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IMealSettingsService, MealSettingsService>();
        services.AddSingleton<RecipeCatalogSync>();
        services.AddSingleton<MealMessageDispatcher>();
        services.AddHttpClient<RecipeScraper>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(20);
                client.MaxResponseContentBufferSize = 16 * 1024 * 1024;
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0 Safari/537.36");
                client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("pt-PT,pt;q=0.9");
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AutomaticDecompression = DecompressionMethods.All });
        services.AddHttpClient<WhatsAppSender>(client => client.Timeout = TimeSpan.FromSeconds(20));
        services.AddScoped<IFinanceService, FinanceService>();
        services.AddScoped<IFinanceImportService, FinanceImportService>();
        services.AddScoped<IFamilyEventService, FamilyEventService>();
        services.AddScoped<ITraderTradeService, TraderTradeService>();
        services.AddScoped<ITraderIntegrationService, TraderIntegrationService>();
        services.AddScoped<ITraderIngestService, TraderIngestService>();

        services.AddDataProtection();
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IGoogleCalendarApi, GoogleCalendarApi>();
        services.AddScoped<GoogleCalendarService>();
        services.AddScoped<IGoogleCalendarService>(provider => provider.GetRequiredService<GoogleCalendarService>());
        services.AddScoped<IGoogleEntrySync>(provider => provider.GetRequiredService<GoogleCalendarService>());
        services.AddScoped<IAgendaSharingService, AgendaSharingService>();
        services.AddScoped<IPublicAgendaService, PublicAgendaService>();
        services.AddScoped<IUnclaimedDataService, UnclaimedDataService>();

        return services;
    }

    public static void MigrateAgenderDatabase(this IServiceProvider services)
    {
        IDbContextFactory<AgenderDbContext> factory = services.GetRequiredService<IDbContextFactory<AgenderDbContext>>();
        using AgenderDbContext context = factory.CreateDbContext();
        context.Database.Migrate();
    }
}
