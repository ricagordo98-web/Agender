namespace Agender.Web.Tunnel;

public static class TunnelRestrictions
{
    private const string CloudflareHeader = "CF-Connecting-IP";

    private static readonly string[] PublicPrefixes = ["/p/", "/_blazor", "/_framework", "/_content"];
    private static readonly string[] AssetExtensions = [".css", ".js", ".map", ".png", ".ico", ".svg", ".woff", ".woff2", ".webp"];

    public static IApplicationBuilder UseTunnelRestrictions(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        if (context.Request.Headers.ContainsKey(CloudflareHeader) && !IsPublicPath(context.Request.Path))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        await next();
    });

    private static bool IsPublicPath(PathString path)
    {
        string value = path.Value ?? string.Empty;

        return PublicPrefixes.Any(prefix => value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            || AssetExtensions.Contains(Path.GetExtension(value), StringComparer.OrdinalIgnoreCase);
    }
}
