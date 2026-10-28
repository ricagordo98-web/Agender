namespace Agender.Shared.Domain;

public enum GuestStatus { Pending = 0, Invited = 1, Confirmed = 2, Declined = 3 }

public enum VendorStatus { ToContact = 0, Contacted = 1, Booked = 2, Paid = 3 }

public class WeddingSettings
{
    public const int VenueMaxLength = 120;

    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid HouseholdId { get; set; }

    public DateOnly? Date { get; set; }

    public string? Venue { get; set; }
}

public class WeddingGuest
{
    public const int NameMaxLength = 80;
    public const int TextMaxLength = 120;
    public const int NotesMaxLength = 300;
    public const int MaxPlusOnes = 20;

    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid HouseholdId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Group { get; set; }

    public int PlusOnes { get; set; }

    public string? Contact { get; set; }

    public string? Table { get; set; }

    public string? Diet { get; set; }

    public string? Notes { get; set; }

    public GuestStatus Status { get; set; } = GuestStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int People => 1 + PlusOnes;
}

public class WeddingCategory
{
    public const int NameMaxLength = 60;

    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid HouseholdId { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal Planned { get; set; }

    public decimal Cost { get; set; }

    public int Order { get; set; }

    public List<WeddingPayment> Payments { get; set; } = [];

    public decimal Paid => Payments.Sum(p => p.Amount);

    public decimal Remaining => Math.Max(0, Cost - Paid);
}

public class WeddingPayment
{
    public const int PaidByMaxLength = 60;
    public const int NotesMaxLength = 200;

    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid HouseholdId { get; set; }

    public Guid CategoryId { get; set; }

    public decimal Amount { get; set; }

    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    public string? PaidBy { get; set; }

    public string? Notes { get; set; }
}

public class WeddingVendor
{
    public const int NameMaxLength = 80;
    public const int TextMaxLength = 120;
    public const int NotesMaxLength = 300;

    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid HouseholdId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Kind { get; set; }

    public string? Contact { get; set; }

    public decimal? Quote { get; set; }

    public VendorStatus Status { get; set; } = VendorStatus.ToContact;

    public Guid? CategoryId { get; set; }

    public string? Notes { get; set; }
}

public class WeddingTask
{
    public const int TitleMaxLength = 160;

    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid HouseholdId { get; set; }

    public string Title { get; set; } = string.Empty;

    public DateOnly? DueDate { get; set; }

    public string? AssigneeUserId { get; set; }

    public bool Done { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public static class WeddingCatalog
{
    public static readonly string[] GuestGroups = ["Família da noiva", "Família do noivo", "Amigos", "Trabalho", "Outros"];

    public static readonly string[] VendorKinds =
        ["Espaço", "Catering", "Fotografia", "Vídeo", "Música / DJ", "Flores", "Bolo", "Vestido / Fato", "Cabelo e maquilhagem", "Transporte", "Convites", "Alianças", "Outros"];

    public static readonly string[] SuggestedCategories =
        ["Espaço", "Catering", "Bebidas", "Fotografia e vídeo", "Música", "Flores e decoração", "Vestido e fato", "Cabelo e maquilhagem", "Alianças", "Convites", "Bolo", "Transporte", "Lua de mel", "Outros"];

    public static readonly (int MonthsBefore, string Title)[] SuggestedTasks =
    [
        (12, "Definir orçamento total"),
        (12, "Fazer a lista de convidados"),
        (12, "Escolher e reservar o espaço"),
        (10, "Escolher fotógrafo e vídeo"),
        (10, "Escolher catering"),
        (9, "Escolher música / DJ"),
        (8, "Escolher vestido e fato"),
        (6, "Enviar os save the date"),
        (6, "Reservar a lua de mel"),
        (4, "Provar menu e escolher bolo"),
        (4, "Escolher e encomendar alianças"),
        (3, "Enviar os convites"),
        (3, "Tratar da documentação do casamento"),
        (2, "Marcar prova final do vestido e do fato"),
        (2, "Marcar cabelo e maquilhagem"),
        (1, "Confirmar presenças e restrições alimentares"),
        (1, "Definir a disposição das mesas"),
        (1, "Confirmar todos os fornecedores")
    ];

    public static string Label(GuestStatus status) => status switch
    {
        GuestStatus.Pending => "Pendente",
        GuestStatus.Invited => "Convidado",
        GuestStatus.Confirmed => "Confirmado",
        _ => "Recusou"
    };

    public static string Color(GuestStatus status) => status switch
    {
        GuestStatus.Pending => "#9AA3B8",
        GuestStatus.Invited => "#56CCF2",
        GuestStatus.Confirmed => "#6FCF97",
        _ => "#FF7A6B"
    };

    public static string Label(VendorStatus status) => status switch
    {
        VendorStatus.ToContact => "Por contactar",
        VendorStatus.Contacted => "Contactado",
        VendorStatus.Booked => "Reservado",
        _ => "Pago"
    };

    public static string Color(VendorStatus status) => status switch
    {
        VendorStatus.ToContact => "#9AA3B8",
        VendorStatus.Contacted => "#56CCF2",
        VendorStatus.Booked => "#F2C94C",
        _ => "#6FCF97"
    };
}
