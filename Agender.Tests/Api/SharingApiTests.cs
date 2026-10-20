using System.Net;
using System.Net.Http.Json;
using Agender.Shared.Contracts;
using Agender.Shared.Domain;
using FluentAssertions;

namespace Agender.Tests.Api;

public sealed class SharingApiTests(AgenderApiFactory factory) : IClassFixture<AgenderApiFactory>
{
    private const string Range = "from=2026-10-12&to=2026-10-18";
    private static readonly DateOnly Day = new(2026, 10, 13);

    [Fact]
    public async Task Sharing_WithoutToken_ReturnsUnauthorized()
    {
        using HttpClient client = factory.CreateClient();

        (await client.GetAsync("/api/sharing")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("/api/me")]
    [InlineData("/conta/entrar")]
    [InlineData("/")]
    public async Task Tunnel_RequestsToPrivatePaths_AreNotFound(string path)
    {
        using HttpClient client = factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Get, path);
        request.Headers.Add("CF-Connecting-IP", "203.0.113.7");

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Tunnel_RequestToThePublicAgendaPage_IsServed()
    {
        using HttpClient client = factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Get, "/p/some-token");
        request.Headers.Add("CF-Connecting-IP", "203.0.113.7");

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Share_ViewerSeesTheOwnersAgenda_ButCannotChangeIt()
    {
        using HttpClient owner = await factory.CreateAuthenticatedClientAsync("share-owner@example.com", "Dono");
        using HttpClient viewer = await factory.CreateAuthenticatedClientAsync("share-viewer@example.com", "Visitante");
        await owner.PostAsJsonAsync("/api/entries", new TimeEntryDto(Guid.NewGuid(), Day, 540, 600, "Trabalho", EntryColor.Teal, null, null, []));

        HttpResponseMessage shared = await owner.PostAsJsonAsync("/api/sharing", new ShareRequest("share-viewer@example.com"));
        shared.EnsureSuccessStatusCode();
        SharedWithMeDto[] received = (await viewer.GetFromJsonAsync<SharedWithMeDto[]>("/api/sharing/received"))!;

        SharedAgendaDto agenda = (await viewer.GetFromJsonAsync<SharedAgendaDto>($"/api/sharing/received/{received.Single().ShareId}/agenda?{Range}"))!;

        agenda.Entries.Should().ContainSingle().Which.Description.Should().Be("Trabalho");
        (await viewer.GetFromJsonAsync<TimeEntryDto[]>($"/api/entries?{Range}"))!.Should().BeEmpty();
    }

    [Fact]
    public async Task SharedAgenda_OfAnotherUsersShare_ReturnsNotFound()
    {
        using HttpClient owner = await factory.CreateAuthenticatedClientAsync("share-owner2@example.com");
        using HttpClient viewer = await factory.CreateAuthenticatedClientAsync("share-viewer2@example.com");
        using HttpClient stranger = await factory.CreateAuthenticatedClientAsync("share-stranger2@example.com");
        ShareDto share = (await (await owner.PostAsJsonAsync("/api/sharing", new ShareRequest("share-viewer2@example.com"))).Content.ReadFromJsonAsync<ShareDto>())!;

        HttpResponseMessage response = await stranger.GetAsync($"/api/sharing/received/{share.Id}/agenda?{Range}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Share_UnknownEmail_ReturnsBadRequest()
    {
        using HttpClient owner = await factory.CreateAuthenticatedClientAsync("share-owner3@example.com");

        HttpResponseMessage response = await owner.PostAsJsonAsync("/api/sharing", new ShareRequest("ghost@example.com"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Link_CreateThenRevoke_UpdatesStatus()
    {
        using HttpClient owner = await factory.CreateAuthenticatedClientAsync("share-owner4@example.com");

        NewShareLinkDto created = (await (await owner.PostAsync("/api/sharing/link", null)).Content.ReadFromJsonAsync<NewShareLinkDto>())!;
        ShareLinkStatusDto active = (await owner.GetFromJsonAsync<ShareLinkStatusDto>("/api/sharing/link"))!;
        await owner.DeleteAsync("/api/sharing/link");
        ShareLinkStatusDto revoked = (await owner.GetFromJsonAsync<ShareLinkStatusDto>("/api/sharing/link"))!;

        created.Token.Should().NotBeNullOrWhiteSpace();
        active.IsActive.Should().BeTrue();
        revoked.IsActive.Should().BeFalse();
    }
}
