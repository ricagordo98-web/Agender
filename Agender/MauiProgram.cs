using Agender.Api;
using Agender.Auth;
using Agender.Shared;
using Agender.Shared.Abstractions;
using Agender.Shared.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;

namespace Agender;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        MauiAppBuilder builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddAgenderUi();
        builder.Services.AddSingleton<IClock, SystemClock>();
        builder.Services.AddSingleton<MauiAppActivation>();
        builder.Services.AddSingleton<IAppActivation>(services => services.GetRequiredService<MauiAppActivation>());
        builder.Services.AddSingleton<IAdditionalRouteAssemblies, MauiRouteAssemblies>();

        builder.Services.AddSingleton<AuthSession>();
        builder.Services.AddScoped<IAuthNavigation, MauiAuthNavigation>();
        builder.Services.AddAuthorizationCore();
        builder.Services.AddScoped<AuthenticationStateProvider, ApiAuthenticationStateProvider>();

        builder.Services.AddTransient<AuthHeaderHandler>();
        builder.Services.AddHttpClient(ApiClientBase.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(15))
            .AddHttpMessageHandler<AuthHeaderHandler>();
        builder.Services.AddScoped<ITimeEntryService, ApiTimeEntryService>();
        builder.Services.AddScoped<ITaskService, ApiTaskService>();
        builder.Services.AddScoped<IDayNoteService, ApiDayNoteService>();
        builder.Services.AddScoped<IProjectService, ApiProjectService>();
        builder.Services.AddScoped<ITagService, ApiTagService>();
        builder.Services.AddScoped<ITimerService, ApiTimerService>();
        builder.Services.AddScoped<IUserSettingsService, ApiUserSettingsService>();
        builder.Services.AddScoped<IAgendaService, ApiAgendaService>();
        builder.Services.AddScoped<IGoogleCalendarService, ApiGoogleCalendarService>();
        builder.Services.AddScoped<ApiAgendaSharingService>();
        builder.Services.AddScoped<IAgendaSharingService>(services => services.GetRequiredService<ApiAgendaSharingService>());
        builder.Services.AddScoped<IShareLinkBuilder>(services => services.GetRequiredService<ApiAgendaSharingService>());

        builder.Services.AddMauiBlazorWebView();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
