using Agender.Auth;
using Agender.Shared.Contracts;
using Agender.Shared.Domain;
using Agender.Shared.Services;

namespace Agender.Api;

public sealed class ApiDailyReminderService(IHttpClientFactory httpClientFactory, AuthSession session)
    : ApiClientBase(httpClientFactory, session), IDailyReminderService
{
    private const string Root = "/api/casa/daily";

    private static readonly TimeSpan RemoteTimeout = TimeSpan.FromMinutes(1);

    public Task<DailyRemindersDto> GetAsync(CancellationToken cancellationToken = default) =>
        GetAsync<DailyRemindersDto>(Root, cancellationToken);

    public Task<DailyRemindersDto> SaveAsync(List<DailyReminderDto> reminders, CancellationToken cancellationToken = default) =>
        SendAsync<DailyRemindersDto>(HttpMethod.Put, Root, reminders, cancellationToken);

    public Task<List<WhatsAppGroupDto>> GetGroupsAsync(CancellationToken cancellationToken = default) =>
        SendAsync<List<WhatsAppGroupDto>>(HttpMethod.Get, $"{Root}/groups", null, cancellationToken, RemoteTimeout);

    public Task SendNowAsync(DailySection section, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, $"{Root}/send/{(int)section}", null, cancellationToken, RemoteTimeout);

    public Task<DailyDigestDto> GetDigestAsync(CancellationToken cancellationToken = default) =>
        GetAsync<DailyDigestDto>($"{Root}/digest", cancellationToken);
}
