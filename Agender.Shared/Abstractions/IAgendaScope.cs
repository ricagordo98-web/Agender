namespace Agender.Shared.Abstractions;

public interface IAgendaScope
{
    public const string HeaderName = "X-Agender-Scope";
    public const string HouseholdPrefix = "h:";

    string? TargetId { get; }
}
