using Agender.Auth;
using Agender.Shared.Contracts;
using Agender.Shared.Domain;
using Agender.Shared.Services;

namespace Agender.Api;

public sealed class ApiFinanceService(IHttpClientFactory httpClientFactory, AuthSession session)
    : ApiClientBase(httpClientFactory, session), IFinanceService
{
    public Task<FinanceSummary> GetSummaryAsync(int year, int month, string? owner = null, CancellationToken cancellationToken = default) =>
        GetAsync<FinanceSummary>($"/api/casa/finance/summary?year={year}&month={month}{OwnerQuery(owner)}", cancellationToken);

    public async Task<IReadOnlyList<FinanceTransaction>> GetTransactionsAsync(int year, int month, string? owner = null, CancellationToken cancellationToken = default) =>
        await GetAsync<FinanceTransaction[]>($"/api/casa/finance/transactions?year={year}&month={month}{OwnerQuery(owner)}", cancellationToken);

    public async Task<IReadOnlyList<FinancePerson>> GetPeopleAsync(CancellationToken cancellationToken = default) =>
        await GetAsync<FinancePerson[]>("/api/casa/finance/people", cancellationToken);

    public Task<FinancePerson> CreatePersonAsync(FinancePerson person, CancellationToken cancellationToken = default) =>
        SendAsync<FinancePerson>(HttpMethod.Post, "/api/casa/finance/people", person, cancellationToken);

    public Task UpdatePersonAsync(FinancePerson person, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Put, $"/api/casa/finance/people/{person.Id}", person, cancellationToken);

    public Task DeletePersonAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"/api/casa/finance/people/{id}", null, cancellationToken);

    public Task<BalanceSheet> GetBalanceAsync(CancellationToken cancellationToken = default) =>
        GetAsync<BalanceSheet>("/api/casa/finance/balance", cancellationToken);

    public async Task<IReadOnlyList<SavingsAccount>> GetSavingsAccountsAsync(CancellationToken cancellationToken = default) =>
        await GetAsync<SavingsAccount[]>("/api/casa/finance/savings/accounts", cancellationToken);

    public Task<SavingsAccount> CreateSavingsAccountAsync(SavingsAccount account, CancellationToken cancellationToken = default) =>
        SendAsync<SavingsAccount>(HttpMethod.Post, "/api/casa/finance/savings/accounts", account, cancellationToken);

    public Task UpdateSavingsAccountAsync(SavingsAccount account, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Put, $"/api/casa/finance/savings/accounts/{account.Id}", account, cancellationToken);

    public Task DeleteSavingsAccountAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"/api/casa/finance/savings/accounts/{id}", null, cancellationToken);

    public async Task<IReadOnlyList<SavingsSnapshot>> GetSavingsSnapshotsAsync(CancellationToken cancellationToken = default) =>
        await GetAsync<SavingsSnapshot[]>("/api/casa/finance/savings/snapshots", cancellationToken);

    public Task SetSavingsSnapshotAsync(int year, Guid personId, decimal value, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Put, "/api/casa/finance/savings/snapshots", new SnapshotRequest(year, personId, value), cancellationToken);

    public Task DeleteSavingsSnapshotAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"/api/casa/finance/savings/snapshots/{id}", null, cancellationToken);

    public Task<FinanceTransaction> CreateTransactionAsync(FinanceTransaction transaction, CancellationToken cancellationToken = default) =>
        SendAsync<FinanceTransaction>(HttpMethod.Post, "/api/casa/finance/transactions", transaction, cancellationToken);

    public Task UpdateTransactionAsync(FinanceTransaction transaction, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Put, $"/api/casa/finance/transactions/{transaction.Id}", transaction, cancellationToken);

    public Task DeleteTransactionAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"/api/casa/finance/transactions/{id}", null, cancellationToken);

    public async Task<IReadOnlyList<SavingsGoal>> GetGoalsAsync(CancellationToken cancellationToken = default) =>
        await GetAsync<SavingsGoal[]>("/api/casa/finance/goals", cancellationToken);

    public Task<SavingsGoal> CreateGoalAsync(SavingsGoal goal, CancellationToken cancellationToken = default) =>
        SendAsync<SavingsGoal>(HttpMethod.Post, "/api/casa/finance/goals", goal, cancellationToken);

    public Task UpdateGoalAsync(SavingsGoal goal, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Put, $"/api/casa/finance/goals/{goal.Id}", goal, cancellationToken);

    public Task DeleteGoalAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"/api/casa/finance/goals/{id}", null, cancellationToken);

    private static string OwnerQuery(string? owner) => string.IsNullOrEmpty(owner) ? string.Empty : $"&owner={Uri.EscapeDataString(owner)}";
}
