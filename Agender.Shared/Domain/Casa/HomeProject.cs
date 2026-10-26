namespace Agender.Shared.Domain;

public enum HomePriority { Low = 0, Medium = 1, High = 2, Urgent = 3 }

public enum HomeStatus { Idea = 0, Planned = 1, InProgress = 2, Done = 3 }

public enum HomeKind { Purchase = 0, Renovation = 1, Repair = 2, Maintenance = 3, Decoration = 4 }

public class HomeProject
{
    public const int TitleMaxLength = 120;
    public const int DescriptionMaxLength = 2000;
    public const int LinkMaxLength = 500;

    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid HouseholdId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public HomeKind Kind { get; set; } = HomeKind.Purchase;

    public HomePriority Priority { get; set; } = HomePriority.Medium;

    public HomeStatus Status { get; set; } = HomeStatus.Idea;

    public string? Room { get; set; }

    public decimal? EstimatedCost { get; set; }

    public decimal? ActualCost { get; set; }

    public DateOnly? DueDate { get; set; }

    public string? Link { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<HomeTask> Tasks { get; set; } = [];
}

public class HomeTask
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProjectId { get; set; }

    public string Title { get; set; } = string.Empty;

    public bool Done { get; set; }

    public int Order { get; set; }
}
