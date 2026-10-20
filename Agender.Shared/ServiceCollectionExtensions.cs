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
        services.AddScoped<AgendaStore>();
        services.AddScoped<PaletteState>();
        services.AddScoped<GoogleCalendarState>();
        services.AddScoped<UserSettingsState>();
        services.TryAddSingleton<IAppActivation, NullAppActivation>();

        return services;
    }
}
