namespace Agender.Infrastructure.Google;

public class GoogleConnection
{
    public const int CredentialsMaxLength = 10_000;
    public const int EmailMaxLength = 320;
    public const int CalendarIdMaxLength = 300;
    public const int TimeZoneMaxLength = 100;

    public Guid Id { get; set; } = Guid.NewGuid();

    public string ProtectedCredentials { get; set; } = string.Empty;

    public string ServiceAccountEmail { get; set; } = string.Empty;

    public string CalendarId { get; set; } = string.Empty;

    public string WriteCalendarId { get; set; } = string.Empty;

    public string TimeZone { get; set; } = string.Empty;

    public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;
}
