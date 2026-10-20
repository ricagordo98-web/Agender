namespace Agender.Web.Auth;

public class AgenderOptions
{
    public const string SectionName = "Agender";

    public bool AllowRegistration { get; set; } = true;

    public bool EnableTunnel { get; set; }
}
