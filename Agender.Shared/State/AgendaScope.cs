using Agender.Shared.Abstractions;

namespace Agender.Shared.State;

public sealed class AgendaScope : IAgendaScope
{
    public event Action? Changed;

    public string? TargetId { get; private set; }

    public string? TargetLabel { get; private set; }

    public string? MyUserId { get; set; }

    public bool IsForeign => TargetId is not null;

    public string? EffectiveId => TargetId ?? MyUserId;

    public static string HouseholdScopeId(Guid householdId) => IAgendaScope.HouseholdPrefix + householdId;

    public void Select(string? targetId, string? label = null)
    {
        string? normalized = string.IsNullOrEmpty(targetId) || targetId == MyUserId ? null : targetId;

        if (normalized == TargetId)
        {
            return;
        }

        TargetId = normalized;
        TargetLabel = normalized is null ? null : label;
        Changed?.Invoke();
    }
}
