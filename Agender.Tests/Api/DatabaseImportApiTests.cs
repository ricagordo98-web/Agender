using System.Net;
using FluentAssertions;

namespace Agender.Tests.Api;

public sealed class DatabaseImportApiTests(AgenderApiFactory factory) : IClassFixture<AgenderApiFactory>
{
    [Fact]
    public async Task Import_WithoutToken_ReturnsUnauthorized()
    {
        using HttpClient client = factory.CreateClient();

        (await client.PostAsync("/api/database/import", new ByteArrayContent([1, 2, 3]))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Import_LocalRequestWithInvalidFile_ReturnsBadRequest()
    {
        using HttpClient client = await factory.CreateAuthenticatedClientAsync("import-local@example.com");

        (await client.PostAsync("/api/database/import", new ByteArrayContent("not a database"u8.ToArray()))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("Tailscale-Funnel-Request")]
    [InlineData("X-Forwarded-For")]
    public async Task Import_ThroughAProxy_ReturnsNotFound(string header)
    {
        using HttpClient client = await factory.CreateAuthenticatedClientAsync($"import-{header.ToLowerInvariant()}@example.com");
        using HttpRequestMessage request = new(HttpMethod.Post, "/api/database/import") { Content = new ByteArrayContent("not a database"u8.ToArray()) };
        request.Headers.Add(header, "?1");

        (await client.SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
