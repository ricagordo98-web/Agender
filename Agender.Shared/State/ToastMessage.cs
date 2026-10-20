namespace Agender.Shared.State;

public enum ToastKind
{
    Info,
    Success,
    Error
}

public record ToastAction(string Label, Func<Task> Callback);

public record ToastMessage(Guid Id, string Text, ToastKind Kind, ToastAction? Action);
