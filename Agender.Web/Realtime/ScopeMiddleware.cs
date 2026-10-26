using Agender.Shared.Abstractions;
using Agender.Shared.State;

namespace Agender.Web.Realtime;

public static class ScopeMiddleware
{
    public static IApplicationBuilder UseAgendaScopeHeader(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        if (context.Request.Path.StartsWithSegments("/api")
            && context.Request.Headers.TryGetValue(IAgendaScope.HeaderName, out Microsoft.Extensions.Primitives.StringValues value)
            && value.ToString() is { Length: > 0 and <= 80 } target)
        {
            context.RequestServices.GetRequiredService<AgendaScope>().Select(target);
        }

        await next();
    });

    public static IApplicationBuilder UseHubQueryToken(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        if (context.Request.Path.StartsWithSegments(ChangeHub.Path)
            && !context.Request.Headers.ContainsKey("Authorization")
            && context.Request.Query["access_token"].ToString() is { Length: > 0 } token)
        {
            context.Request.Headers.Authorization = $"Bearer {token}";
        }

        await next();
    });
}
