namespace Agender.Shared.State;

public sealed class PaletteState
{
    public event Action? Changed;

    public bool IsOpen { get; private set; }

    public void Open() => Set(true);

    public void Close() => Set(false);

    public void Toggle() => Set(!IsOpen);

    private void Set(bool value)
    {
        if (IsOpen == value)
        {
            return;
        }

        IsOpen = value;
        Changed?.Invoke();
    }
}
