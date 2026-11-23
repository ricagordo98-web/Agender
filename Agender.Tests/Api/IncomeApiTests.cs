using System.Net;
using System.Net.Http.Json;
using Agender.Shared.Contracts;
using Agender.Shared.Domain;
using FluentAssertions;

namespace Agender.Tests.Api;

public sealed class IncomeApiTests(AgenderApiFactory factory) : IClassFixture<AgenderApiFactory>
{
    [Fact]
    public async Task Income_CreateSourceAndEntries_OnlyTheOwnerCanReadThem()
    {
        using HttpClient owner = await factory.CreateAuthenticatedClientAsync("income-owner@example.com");
        using HttpClient stranger = await factory.CreateAuthenticatedClientAsync("income-stranger@example.com");

        HttpResponseMessage created = await owner.PostAsJsonAsync("/api/income/sources", new IncomeSourceInput("Surveys", "📝", EntryColor.Teal, false));
        created.StatusCode.Should().Be(HttpStatusCode.OK);
        IncomeSourceDto source = (await created.Content.ReadFromJsonAsync<IncomeSourceDto>())!;

        (await owner.PostAsJsonAsync("/api/income/entries", new IncomeEntryInput(source.Id, new DateOnly(2026, 11, 2), 3.2m, "Prolific", string.Empty))).EnsureSuccessStatusCode();

        IReadOnlyList<IncomeEntryDto> entries = (await owner.GetFromJsonAsync<IReadOnlyList<IncomeEntryDto>>($"/api/income/entries?from=2026-11-01&to=2026-11-30&sourceId={source.Id}"))!;
        entries.Should().ContainSingle().Which.Amount.Should().Be(3.2m);
        (await owner.GetFromJsonAsync<IReadOnlyList<string>>($"/api/income/sources/{source.Id}/platforms"))!.Should().Equal("Prolific");
        (await stranger.GetFromJsonAsync<IReadOnlyList<IncomeSourceDto>>("/api/income/sources"))!.Should().BeEmpty();
        (await stranger.GetFromJsonAsync<IReadOnlyList<IncomeEntryDto>>("/api/income/entries?from=2026-11-01&to=2026-11-30"))!.Should().BeEmpty();
    }

    [Fact]
    public async Task Income_InvalidEntry_ReturnsBadRequest()
    {
        using HttpClient client = await factory.CreateAuthenticatedClientAsync("income-invalid@example.com");

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/income/entries", new IncomeEntryInput(Guid.NewGuid(), new DateOnly(2026, 11, 2), 0, string.Empty, string.Empty));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Income_DeleteUnknownEntry_ReturnsNotFound()
    {
        using HttpClient client = await factory.CreateAuthenticatedClientAsync("income-missing@example.com");

        (await client.DeleteAsync($"/api/income/entries/{Guid.NewGuid()}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
