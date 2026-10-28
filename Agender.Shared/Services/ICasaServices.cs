using Agender.Shared.Contracts;
using Agender.Shared.Domain;

namespace Agender.Shared.Services;

public interface IHouseholdService
{
    Task<HouseholdDto?> GetAsync(CancellationToken cancellationToken = default);

    Task<HouseholdDto> CreateAsync(string name, CancellationToken cancellationToken = default);

    Task<HouseholdDto> JoinAsync(string code, CancellationToken cancellationToken = default);

    Task<HouseholdDto> RenameAsync(string name, CancellationToken cancellationToken = default);

    Task<HouseholdDto> NewInviteCodeAsync(CancellationToken cancellationToken = default);

    Task<HouseholdDto> SetMyColorAsync(string color, CancellationToken cancellationToken = default);

    Task<HouseholdDto> InviteAsync(string email, CancellationToken cancellationToken = default);

    Task<HouseholdDto> CancelInviteAsync(Guid inviteId, CancellationToken cancellationToken = default);

    Task LeaveAsync(CancellationToken cancellationToken = default);

    Task<CasaDashboard> GetDashboardAsync(CancellationToken cancellationToken = default);
}

public interface IShoppingService
{
    Task<IReadOnlyList<ShoppingList>> GetListsAsync(CancellationToken cancellationToken = default);

    Task<ShoppingList> GetListAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ShoppingList> CreateListAsync(ShoppingList list, CancellationToken cancellationToken = default);

    Task UpdateListAsync(ShoppingList list, CancellationToken cancellationToken = default);

    Task DeleteListAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ShoppingItem> AddItemAsync(Guid listId, ShoppingItem item, CancellationToken cancellationToken = default);

    Task UpdateItemAsync(ShoppingItem item, CancellationToken cancellationToken = default);

    Task DeleteItemAsync(Guid id, CancellationToken cancellationToken = default);

    Task ClearCheckedAsync(Guid listId, CancellationToken cancellationToken = default);

    Task<ListShare> ShareAsync(Guid listId, string email, bool canEdit, CancellationToken cancellationToken = default);

    Task UnshareAsync(Guid listId, Guid shareId, CancellationToken cancellationToken = default);
}

public interface IHomeProjectService
{
    Task<IReadOnlyList<HomeProject>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<HomeProject> CreateAsync(HomeProject project, CancellationToken cancellationToken = default);

    Task UpdateAsync(HomeProject project, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IWeddingService
{
    Task<WeddingOverview> GetAsync(CancellationToken cancellationToken = default);

    Task<WeddingSettings> SaveSettingsAsync(WeddingSettings settings, CancellationToken cancellationToken = default);

    Task<WeddingGuest> SaveGuestAsync(WeddingGuest guest, CancellationToken cancellationToken = default);

    Task DeleteGuestAsync(Guid id, CancellationToken cancellationToken = default);

    Task<WeddingCategory> SaveCategoryAsync(WeddingCategory category, CancellationToken cancellationToken = default);

    Task DeleteCategoryAsync(Guid id, CancellationToken cancellationToken = default);

    Task<WeddingPayment> SavePaymentAsync(WeddingPayment payment, CancellationToken cancellationToken = default);

    Task DeletePaymentAsync(Guid id, CancellationToken cancellationToken = default);

    Task<WeddingVendor> SaveVendorAsync(WeddingVendor vendor, CancellationToken cancellationToken = default);

    Task DeleteVendorAsync(Guid id, CancellationToken cancellationToken = default);

    Task<WeddingTask> SaveTaskAsync(WeddingTask task, CancellationToken cancellationToken = default);

    Task DeleteTaskAsync(Guid id, CancellationToken cancellationToken = default);

    Task<int> AddSuggestedCategoriesAsync(CancellationToken cancellationToken = default);

    Task<int> AddSuggestedTasksAsync(CancellationToken cancellationToken = default);
}

public interface IFinanceService
{
    Task<FinanceSummary> GetSummaryAsync(int year, int month, string? owner = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FinanceTransaction>> GetTransactionsAsync(int year, int month, string? owner = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FinancePerson>> GetPeopleAsync(CancellationToken cancellationToken = default);

    Task<FinancePerson> CreatePersonAsync(FinancePerson person, CancellationToken cancellationToken = default);

    Task UpdatePersonAsync(FinancePerson person, CancellationToken cancellationToken = default);

    Task DeletePersonAsync(Guid id, CancellationToken cancellationToken = default);

    Task<BalanceSheet> GetBalanceAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SavingsAccount>> GetSavingsAccountsAsync(CancellationToken cancellationToken = default);

    Task<SavingsAccount> CreateSavingsAccountAsync(SavingsAccount account, CancellationToken cancellationToken = default);

    Task UpdateSavingsAccountAsync(SavingsAccount account, CancellationToken cancellationToken = default);

    Task DeleteSavingsAccountAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SavingsSnapshot>> GetSavingsSnapshotsAsync(CancellationToken cancellationToken = default);

    Task SetSavingsSnapshotAsync(int year, Guid personId, decimal value, CancellationToken cancellationToken = default);

    Task DeleteSavingsSnapshotAsync(Guid id, CancellationToken cancellationToken = default);

    Task<FinanceTransaction> CreateTransactionAsync(FinanceTransaction transaction, CancellationToken cancellationToken = default);

    Task UpdateTransactionAsync(FinanceTransaction transaction, CancellationToken cancellationToken = default);

    Task DeleteTransactionAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SavingsGoal>> GetGoalsAsync(CancellationToken cancellationToken = default);

    Task<SavingsGoal> CreateGoalAsync(SavingsGoal goal, CancellationToken cancellationToken = default);

    Task UpdateGoalAsync(SavingsGoal goal, CancellationToken cancellationToken = default);

    Task DeleteGoalAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IFinanceImportService
{
    Task<ImportReport> ImportAsync(byte[] workbook, bool apply, CancellationToken cancellationToken = default);
}

public interface IFamilyEventService
{
    Task<IReadOnlyList<EventOccurrence>> GetAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);

    Task<FamilyEvent> CreateAsync(FamilyEvent familyEvent, CancellationToken cancellationToken = default);

    Task UpdateAsync(FamilyEvent familyEvent, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
