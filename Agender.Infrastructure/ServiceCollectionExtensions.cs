using Agender.Infrastructure.Data;
using Agender.Infrastructure.Google;
using Agender.Infrastructure.Identity;
using Agender.Infrastructure.Realtime;
using Agender.Shared.Abstractions;
using Agender.Infrastructure.Sharing;
using Agender.Infrastructure.Services;
using Agender.Shared.Services;
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
        services.AddScoped<IFinanceService, FinanceService>();
        services.AddScoped<IFinanceImportService, FinanceImportService>();
        services.AddScoped<IFamilyEventService, FamilyEventService>();

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
