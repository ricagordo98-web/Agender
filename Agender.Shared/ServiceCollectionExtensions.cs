using Agender.Shared.Abstractions;
using Agender.Shared.State;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Agender.Shared;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAgenderUi(this IServiceCollection services)
    {
        services.AddScoped<IToastService, ToastService>();
        services.TryAddScoped<AgendaScope>();
        services.TryAddScoped<IAgendaScope>(provider => provider.GetRequiredService<AgendaScope>());
        services.AddScoped<AgendaStore>();
        services.AddScoped<PaletteState>();
        services.AddScoped<GoogleCalendarState>();
        services.AddScoped<UserSettingsState>();
        services.TryAddSingleton<IAppActivation, NullAppActivation>();
        services.TryAddSingleton<IChangeFeed, NullChangeFeed>();

        return services;
    }
}
