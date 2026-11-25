using Agender.Auth;
using Agender.Shared.Domain;
using Agender.Shared.Services;

namespace Agender.Api;

public sealed class ApiUtilityBillService(IHttpClientFactory httpClientFactory, AuthSession session)
    : ApiClientBase(httpClientFactory, session), IUtilityBillService
{
    private const string Root = "/api/casa/utilities";

    public Task<List<UtilityBill>> GetAsync(int fromYear, int toYear, CancellationToken cancellationToken = default) =>
        GetAsync<List<UtilityBill>>($"{Root}?fromYear={fromYear}&toYear={toYear}", cancellationToken);

    public Task<UtilityBill> CreateAsync(UtilityBill bill, CancellationToken cancellationToken = default) =>
        SendAsync<UtilityBill>(HttpMethod.Post, Root, bill, cancellationToken);

    public Task<UtilityBill> UpdateAsync(UtilityBill bill, CancellationToken cancellationToken = default) =>
        SendAsync<UtilityBill>(HttpMethod.Put, $"{Root}/{bill.Id}", bill, cancellationToken);

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"{Root}/{id}", null, cancellationToken);

    public Task<List<UtilityTariff>> GetTariffsAsync(CancellationToken cancellationToken = default) =>
        GetAsync<List<UtilityTariff>>($"{Root}/tariffs", cancellationToken);

    public Task<UtilityTariff> SaveTariffAsync(UtilityTariff tariff, CancellationToken cancellationToken = default) =>
        SendAsync<UtilityTariff>(HttpMethod.Put, $"{Root}/tariffs", tariff, cancellationToken);
}
