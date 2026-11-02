using System.Net;
using System.Net.Http.Json;
using Agender.Shared.Contracts;
using FluentAssertions;

namespace Agender.Tests.Api;

public sealed class TraderApiTests(AgenderApiFactory factory) : IClassFixture<AgenderApiFactory>
{
    private const string KeyHeader = "X-Trader-Key";

    private static TraderSyncRequest Snapshot(long id = 1) => new(
    [
        new TraderTradeInput(id, "AAPL", "Apple", true, true, DateTimeOffset.Parse("2026-11-02T09:00:00Z"), 100, 10, 95, 110, DateTimeOffset.Parse("2026-11-02T10:30:00Z"), 102.5, 1, 1000, 50, 25, 0.5, "EUR", "T212", "nota", "setup", "tag", "calmo")
    ]);

    private static async Task<string> CreateKeyAsync(HttpClient client)
    {
        HttpResponseMessage response = await client.PostAsync("/api/trader/integration/key", null);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TraderKeyCreated>())!.Key;
    }

    private HttpClient KeyClient(string key)
    {
        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(KeyHeader, key);
        return client;
    }

    [Fact]
    public async Task Ingest_WithoutKey_ReturnsUnauthorized()
    {
        using HttpClient client = factory.CreateClient();

        (await client.PutAsJsonAsync("/api/integrations/trader/trades", Snapshot())).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Ingest_WithWrongKey_ReturnsUnauthorized()
    {
        using HttpClient client = KeyClient("agt_not-a-real-key");

        (await client.PutAsJsonAsync("/api/integrations/trader/trades", Snapshot())).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Ingest_WithAUserBearerToken_ReturnsUnauthorized()
    {
        using HttpClient client = await factory.CreateAuthenticatedClientAsync("trader-bearer@example.com");

        (await client.PutAsJsonAsync("/api/integrations/trader/trades", Snapshot())).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Ingest_WithTheUsersKey_StoresTradesThatOnlyThatUserCanRead()
    {
        using HttpClient owner = await factory.CreateAuthenticatedClientAsync("trader-owner@example.com");
        using HttpClient stranger = await factory.CreateAuthenticatedClientAsync("trader-stranger@example.com");
        using HttpClient ingest = KeyClient(await CreateKeyAsync(owner));

        HttpResponseMessage synced = await ingest.PutAsJsonAsync("/api/integrations/trader/trades", Snapshot());

        synced.StatusCode.Should().Be(HttpStatusCode.OK);
        (await synced.Content.ReadFromJsonAsync<TraderSyncResult>())!.Created.Should().Be(1);
        IReadOnlyList<TraderTradeDto> mine = (await owner.GetFromJsonAsync<IReadOnlyList<TraderTradeDto>>("/api/trader/trades?from=2026-11-01&to=2026-11-30"))!;
        mine.Should().ContainSingle().Which.NetPnl.Should().Be(25);
        (await stranger.GetFromJsonAsync<IReadOnlyList<TraderTradeDto>>("/api/trader/trades?from=2026-11-01&to=2026-11-30"))!.Should().BeEmpty();
        TraderIntegrationStatus status = (await owner.GetFromJsonAsync<TraderIntegrationStatus>("/api/trader/integration"))!;
        status.Should().Match<TraderIntegrationStatus>(s => s.HasKey && s.TradeCount == 1 && s.LastSyncAt != null);
    }

    [Fact]
    public async Task Ingest_AfterTheKeyIsRevoked_ReturnsUnauthorized()
    {
        using HttpClient owner = await factory.CreateAuthenticatedClientAsync("trader-revoke@example.com");
        using HttpClient ingest = KeyClient(await CreateKeyAsync(owner));

        (await owner.DeleteAsync("/api/trader/integration/key")).EnsureSuccessStatusCode();

        (await ingest.PutAsJsonAsync("/api/integrations/trader/trades", Snapshot())).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateKey_Twice_InvalidatesThePreviousKey()
    {
        using HttpClient owner = await factory.CreateAuthenticatedClientAsync("trader-rotate@example.com");
        string first = await CreateKeyAsync(owner);
        string second = await CreateKeyAsync(owner);

        (await KeyClient(first).PutAsJsonAsync("/api/integrations/trader/trades", Snapshot())).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await KeyClient(second).PutAsJsonAsync("/api/integrations/trader/trades", Snapshot())).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Ingest_DuplicateIds_ReturnsBadRequest()
    {
        using HttpClient owner = await factory.CreateAuthenticatedClientAsync("trader-dup@example.com");
        using HttpClient ingest = KeyClient(await CreateKeyAsync(owner));
        TraderTradeInput trade = Snapshot().Trades[0];

        (await ingest.PutAsJsonAsync("/api/integrations/trader/trades", new TraderSyncRequest([trade, trade]))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
