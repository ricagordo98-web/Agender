namespace Agender.Shared.Domain;

public class Household
{
    public const int NameMaxLength = 80;
    public const int InviteCodeLength = 6;
    public const string DefaultName = "A nossa casa";

    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string InviteCode { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class HouseholdInvite
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(14);
    public const int EmailMaxLength = 256;

    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid HouseholdId { get; set; }

    public string Email { get; set; } = string.Empty;

    public string InvitedBy { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow + Lifetime;
}

public class HouseholdMember
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid HouseholdId { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string Color { get; set; } = CasaCatalog.MemberColors[0];

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}
