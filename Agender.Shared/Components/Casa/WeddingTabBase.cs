using Agender.Shared.Contracts;
using Agender.Shared.Services;
using Agender.Shared.State;
using Microsoft.AspNetCore.Components;

namespace Agender.Shared.Components.Casa;

public abstract class WeddingTabBase : ComponentBase
{
    [Inject]
    protected IWeddingService Wedding { get; set; } = default!;

    [Inject]
    protected IToastService Toasts { get; set; } = default!;

    [Parameter, EditorRequired]
    public WeddingOverview Overview { get; set; } = new();

    [Parameter]
    public HouseholdDto? Household { get; set; }

    [Parameter]
    public EventCallback OnChanged { get; set; }

    [Parameter]
    public EventCallback<bool> OnEditingChanged { get; set; }

    protected void BeginEditing() => _ = OnEditingChanged.InvokeAsync(true);

    protected async Task RunAsync(Func<Task> action, string success, Action close)
    {
        try
        {
            await action();
            close();
            Toasts.Show(success, ToastKind.Success);
            await OnEditingChanged.InvokeAsync(false);
            await OnChanged.InvokeAsync();
        }
        catch (Exception ex) when (ex is ArgumentException or NotFoundException or PersistenceException or ServerUnavailableException or UnauthorizedAccessException)
        {
            Toasts.Show("Não foi possível guardar. Confirma os dados.", ToastKind.Error);
        }
    }

    protected string MemberName(string? userId) =>
        Household?.Members.FirstOrDefault(m => m.UserId == userId)?.DisplayName ?? "—";
}
