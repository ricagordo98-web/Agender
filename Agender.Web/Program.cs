using Agender.Infrastructure;
using Agender.Infrastructure.Data;
using Agender.Infrastructure.Google;
using Agender.Infrastructure.Identity;
using Agender.Infrastructure.Sharing;
using Agender.Shared;
using Agender.Shared.Abstractions;
using Agender.Web.Api;
using Agender.Web.Auth;
using Agender.Web.Components;
using Agender.Web.Tunnel;
using FluentValidation;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.Configure<AgenderOptions>(builder.Configuration.GetSection(AgenderOptions.SectionName));
builder.Services.AddHttpContextAccessor();

string databasePath = builder.Configuration[$"{AgenderOptions.SectionName}:DatabasePath"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "App_Data", "agender.db");

builder.Services.AddAgenderUi();
builder.Services.AddAgenderInfrastructure<WebCurrentUser>(databasePath);

if (builder.Configuration.GetValue<bool>($"{AgenderOptions.SectionName}:DemoGoogle"))
{
    builder.Services.AddSingleton<IGoogleCalendarApi, DemoGoogleCalendarApi>();
}
builder.Services.AddScoped<IClock, BrowserClock>();
builder.Services.AddScoped<IAuthNavigation, WebAuthNavigation>();
builder.Services.AddScoped<IShareLinkBuilder, WebShareLinkBuilder>();
builder.Services.AddSingleton<CloudflareTunnel>();
builder.Services.AddSingleton<IPublicUrlProvider>(services => services.GetRequiredService<CloudflareTunnel>());
builder.Services.AddSingleton<IAdditionalRouteAssemblies, WebRouteAssemblies>();

builder.Services.AddIdentityApiEndpoints<AgenderUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    })
    .AddEntityFrameworkStores<AgenderDbContext>()
    .AddClaimsPrincipalFactory<AgenderClaimsPrincipalFactory>()
    .AddErrorDescriber<PortugueseIdentityErrorDescriber>();

builder.Services.AddSingleton<IEmailSender<AgenderUser>, NoOpEmailSender>();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "agender.auth";
    options.LoginPath = "/conta/entrar";
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.SlidingExpiration = true;
    options.Events.OnRedirectToLogin = context => RedirectOrChallenge(context.HttpContext, () => context.Response.Redirect(context.RedirectUri));
    options.Events.OnRedirectToAccessDenied = context => RedirectOrChallenge(context.HttpContext, () => context.Response.Redirect(context.RedirectUri), StatusCodes.Status403Forbidden);
});

builder.Services.AddAuthentication(options => options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme);
builder.Services.AddAuthorization();
builder.Services.AddValidatorsFromAssemblyContaining<TimeEntryDtoValidator>();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

WebApplication app = builder.Build();

app.Services.MigrateAgenderDatabase();

app.Lifetime.ApplicationStarted.Register(() => _ = app.Services.GetRequiredService<CloudflareTunnel>().PrepareAsync());

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost
});
app.UseTunnelRestrictions();

app.UseWhen(context => context.Request.Path.StartsWithSegments("/api"), api => api.UseExceptionHandler());

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseWhen(
    context => !context.Request.Path.StartsWithSegments("/api"),
    site => site.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true));
app.UseHttpsRedirection();

app.UseAuthApiAllowlist();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapAgenderApi();

app.MapGet("/conta/sair", async (HttpContext context, SignInManager<AgenderUser> signInManager) =>
{
    string site = context.Request.Headers["Sec-Fetch-Site"].ToString();
    if (site is not ("" or "same-origin" or "none"))
    {
        return Results.LocalRedirect("/");
    }

    await signInManager.SignOutAsync();
    return Results.LocalRedirect("/conta/entrar");
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(Agender.Shared._Imports).Assembly);

app.Run();

static Task RedirectOrChallenge(HttpContext context, Action redirect, int apiStatus = StatusCodes.Status401Unauthorized)
{
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        context.Response.StatusCode = apiStatus;
    }
    else
    {
        redirect();
    }

    return Task.CompletedTask;
}

public partial class Program;
