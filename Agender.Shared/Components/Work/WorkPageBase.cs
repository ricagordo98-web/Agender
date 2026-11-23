using Agender.Shared.Contracts;
using Agender.Shared.Services;
using Agender.Shared.State;
using Microsoft.AspNetCore.Components;

namespace Agender.Shared.Components.Work;

public abstract class WorkPageBase : ComponentBase
{
    [Inject]
    protected IIncomeService IncomeService { get; set; } = default!;

    [Inject]
    protected IToastService Toasts { get; set; } = default!;

    [Inject]
    protected NavigationManager Navigation { get; set; } = default!;

    [Inject]
    protected AgendaStore Store { get; set; } = default!;

    protected IReadOnlyList<IncomeSourceDto> Sources { get; private set; } = [];

    protected bool EditorOpen { get; private set; }

    protected IncomeSourceDto? EditingSource { get; private set; }

    protected bool ExportOpen { get; private set; }

    protected string? ExportPreselected { get; private set; }

    protected async Task LoadSourcesAsync()
    {
        try
        {
            Sources = await IncomeService.GetSourcesAsync();
        }
        catch (Exception ex) when (ex is NotFoundException or PersistenceException or ServerUnavailableException or UnauthorizedAccessException)
        {
            Toasts.Show("Não foi possível carregar as fontes de rendimento.", ToastKind.Error);
        }
    }

    protected void OpenNewSource()
    {
        EditingSource = null;
        EditorOpen = true;
    }

    protected void OpenEditSource(IncomeSourceDto source)
    {
        EditingSource = source;
        EditorOpen = true;
    }

    protected void CloseEditor() => EditorOpen = false;

    protected void OpenExport(string? preselected = null)
    {
        ExportPreselected = preselected;
        ExportOpen = true;
    }

    protected void CloseExport() => ExportOpen = false;

    protected async Task OnSourceSavedAsync(IncomeSourceDto saved)
    {
        bool isNew = EditingSource is null;
        EditorOpen = false;
        await LoadSourcesAsync();
        await OnSourcesChangedAsync(saved);

        if (isNew)
        {
            Toasts.Show($"Fonte «{saved.Name}» criada.", ToastKind.Success);
            Navigation.NavigateTo($"trabalho/fonte/{saved.Id}");
        }
        else if (saved.IsArchived)
        {
            Toasts.Show($"«{saved.Name}» foi arquivada.", ToastKind.Info);
            Navigation.NavigateTo("trabalho");
        }
    }

    protected async Task OnSourceDeletedAsync(IncomeSourceDto deleted)
    {
        EditorOpen = false;
        await LoadSourcesAsync();
        Toasts.Show($"«{deleted.Name}» foi eliminada.", ToastKind.Info);
        Navigation.NavigateTo("trabalho");
    }

    protected virtual Task OnSourcesChangedAsync(IncomeSourceDto changed) => Task.CompletedTask;
}
