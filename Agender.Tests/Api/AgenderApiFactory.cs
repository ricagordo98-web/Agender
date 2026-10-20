using System.Net.Http.Headers;
using System.Net.Http.Json;
using Agender.Infrastructure.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Agender.Tests.Api;

public sealed class AgenderApiFactory : WebApplicationFactory<Program>
{
    public const string Password = "Segredo123";

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"agender-tests-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Agender:DatabasePath", _databasePath);
    }

    public async Task<HttpClient> CreateAuthenticatedClientAsync(string email, string displayName = "Test User")
    {
        using (IServiceScope scope = Services.CreateScope())
        {
            UserManager<AgenderUser> users = scope.ServiceProvider.GetRequiredService<UserManager<AgenderUser>>();
            IdentityResult created = await users.CreateAsync(
                new AgenderUser { UserName = email, Email = email, DisplayName = displayName },
                Password);

            if (!created.Succeeded)
            {
                throw new InvalidOperationException(string.Join(", ", created.Errors.Select(e => e.Description)));
            }
        }

        HttpClient client = CreateClient();
        HttpResponseMessage login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        login.EnsureSuccessStatusCode();

        LoginResponse tokens = (await login.Content.ReadFromJsonAsync<LoginResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            TryDelete(_databasePath);
            TryDelete(_databasePath + "-shm");
            TryDelete(_databasePath + "-wal");
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
    }

    private sealed record LoginResponse(string AccessToken, string RefreshToken);
}
