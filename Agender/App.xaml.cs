using Agender.Auth;

namespace Agender;

public partial class App : Application
{
    private readonly MauiAppActivation _activation;

    public App(MauiAppActivation activation)
    {
        _activation = activation;
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        Window window = new(new MainPage()) { Title = "Agender" };
        window.Activated += (_, _) => _activation.Raise();

        return window;
    }
}
