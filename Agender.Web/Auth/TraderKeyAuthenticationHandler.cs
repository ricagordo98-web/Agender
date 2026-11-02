using System.Security.Claims;
using System.Text.Encodings.Web;
using Agender.Infrastructure.Data;
using Agender.Infrastructure.Trader;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Agender.Web.Auth;

public class TraderKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IDbContextFactory<AgenderDbContext> contextFactory) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string Scheme = "TraderKey";
    public const string HeaderName = "X-Trader-Key";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string key = Request.Headers[HeaderName].ToString();

        if (string.IsNullOrWhiteSpace(key))
        {
            return AuthenticateResult.NoResult();
        }

        string hash = TraderKey.Hash(key);
        await using AgenderDbContext context = await contextFactory.CreateDbContextAsync(Context.RequestAborted);
        string? userId = await context.TraderKeys.AsNoTracking().Where(k => k.KeyHash == hash).Select(k => k.UserId).FirstOrDefaultAsync(Context.RequestAborted);

        if (userId is null)
        {
            return AuthenticateResult.Fail("The Trader key is not valid.");
        }

        ClaimsIdentity identity = new([new Claim(ClaimTypes.NameIdentifier, userId)], Scheme);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme));
    }
}
