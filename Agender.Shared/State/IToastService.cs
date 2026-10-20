namespace Agender.Shared.State;

public interface IToastService
{
    event Action? Changed;

    IReadOnlyList<ToastMessage> Messages { get; }

    void Show(string text, ToastKind kind = ToastKind.Info, ToastAction? action = null);

    void Dismiss(Guid id);
}
