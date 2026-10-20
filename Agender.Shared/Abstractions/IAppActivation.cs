namespace Agender.Shared.Abstractions;

public interface IAppActivation
{
    event Action? Activated;
}

public class NullAppActivation : IAppActivation
{
    public event Action? Activated
    {
        add { }
        remove { }
    }
}
