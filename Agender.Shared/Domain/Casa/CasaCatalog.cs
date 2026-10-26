using System.Globalization;

namespace Agender.Shared.Domain;

public static class CasaCatalog
{
    public const string DefaultShoppingCategory = "Outros";
    public const string DefaultExpenseCategory = "Supermercado";
    public const string DefaultIncomeCategory = "Salário";
    public const string SavingCategory = "Poupança";

    public static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("pt-PT");

    public record Category(string Name, string Emoji, string Color);

    public static readonly Category[] ShoppingCategories =
    [
        new("Frutas e Legumes", "🥬", "#6FCF97"),
        new("Talho e Peixaria", "🥩", "#F2994A"),
        new("Laticínios e Ovos", "🧀", "#F2C94C"),
        new("Padaria", "🥖", "#E0B47C"),
        new("Mercearia", "🥫", "#C89F65"),
        new("Congelados", "🧊", "#56CCF2"),
        new("Bebidas", "🧃", "#BB6BD9"),
        new("Limpeza", "🧽", "#2D9CDB"),
        new("Higiene", "🧴", "#9B8CFF"),
        new("Bebé", "🍼", "#FF9AB5"),
        new("Animais", "🐾", "#A3826B"),
        new("Outros", "📦", "#8A93A6")
    ];

    public static readonly Category[] ExpenseCategories =
    [
        new("Habitação", "🏠", "#D4B483"),
        new("Supermercado", "🛒", "#6FCF97"),
        new("Contas da casa", "💡", "#F2C94C"),
        new("Transportes", "🚗", "#56CCF2"),
        new("Saúde", "🩺", "#FF7A6B"),
        new("Educação", "🎓", "#9B8CFF"),
        new("Restauração", "🍽️", "#F2994A"),
        new("Lazer", "🎬", "#BB6BD9"),
        new("Compras", "🛍️", "#FF9AB5"),
        new("Obras e Casa", "🛠️", "#C89F65"),
        new("Seguros", "🛡️", "#2D9CDB"),
        new("Subscrições", "📺", "#7F8CFF"),
        new("Outros", "📦", "#8A93A6")
    ];

    public static readonly string[] IncomeCategories =
        ["Salário", "Subsídio", "Trabalho extra", "Reembolso", "Rendas", "Investimentos", "Outros"];

    public static readonly string[] Units = ["un", "kg", "g", "L", "ml", "emb", "pack", "dz"];

    public static readonly string[] Rooms =
        ["Sala", "Cozinha", "Quarto", "Quarto das crianças", "Casa de banho", "Escritório", "Hall", "Varanda", "Jardim", "Garagem", "Exterior", "Casa toda"];

    private static readonly string[] CategoryPalette =
        ["#6FCF97", "#F2994A", "#F2C94C", "#56CCF2", "#BB6BD9", "#2D9CDB", "#FF9AB5", "#9B8CFF", "#D4B483", "#FF7A6B", "#7F8CFF", "#14B8A6", "#EA9999", "#3D85C6", "#8B8948"];

    public static readonly string[] MemberColors =
        ["#D4B483", "#8B9CFF", "#6FCF97", "#FF9AB5", "#56CCF2", "#F2994A", "#BB6BD9", "#FF7A6B"];

    public static readonly string[] ListColors =
        ["#D4B483", "#6FCF97", "#8B9CFF", "#FF9AB5", "#56CCF2", "#F2994A", "#BB6BD9", "#FF7A6B"];

    public static readonly string[] ListEmojis =
        ["🛒", "🥦", "🧺", "🏠", "🎉", "🍷", "👶", "🐶", "🧼", "🎁", "🏖️", "📦"];

    public static readonly string[] GoalEmojis =
        ["🎯", "🏖️", "🛟", "🚗", "🏠", "🎓", "💍", "👶", "✈️", "🎄", "💻", "🐷"];

    private static readonly (string Category, string[] Keys)[] GuessMap =
    [
        ("Frutas e Legumes", ["maçã", "maca", "banana", "laranja", "pera", "alface", "tomate", "cebola", "alho", "batata", "cenoura", "fruta", "legume", "limão", "limao", "uva", "morango", "courgette", "brócolo", "brocolo", "espinafre", "pepino", "pimento", "abacate", "kiwi", "melão", "melancia", "salsa", "coentro"]),
        ("Talho e Peixaria", ["frango", "carne", "porco", "vaca", "peru", "bife", "picada", "salsicha", "fiambre", "presunto", "chouriço", "bacalhau", "peixe", "salmão", "salmao", "pescada", "atum fresco", "camarão", "dourada", "robalo", "polvo", "lula"]),
        ("Laticínios e Ovos", ["leite", "iogurte", "queijo", "manteiga", "natas", "ovo", "ovos", "requeijão", "kefir"]),
        ("Padaria", ["pão", "pao", "broa", "croissant", "tosta", "bolo", "baguete", "papo"]),
        ("Congelados", ["congelad", "gelado", "ervilha", "pizza"]),
        ("Bebidas", ["água", "agua", "sumo", "vinho", "cerveja", "refrigerante", "café", "cafe", "chá", "cha ", "coca", "leite vegetal"]),
        ("Limpeza", ["detergente", "lixívia", "lixivia", "esponja", "sacos do lixo", "amaciador", "limpa", "papel de cozinha", "pastilhas"]),
        ("Higiene", ["champô", "champo", "gel de banho", "sabonete", "pasta de dentes", "escova", "desodorizante", "papel higiénico", "papel higienico", "algodão", "cotonetes"]),
        ("Bebé", ["fralda", "toalhitas", "papa", "bebé", "bebe"]),
        ("Animais", ["ração", "racao", "areia gato", "snack cão", "cão", "gato"]),
        ("Mercearia", ["arroz", "massa", "esparguete", "farinha", "açúcar", "acucar", "sal", "azeite", "óleo", "oleo", "feijão", "feijao", "grão", "grao", "atum", "cereais", "bolachas", "molho", "vinagre", "especiarias", "enlatado", "mel", "compota", "chocolate"])
    ];

    public static Category ShoppingCategory(string? name) =>
        ShoppingCategories.FirstOrDefault(c => c.Name == name) ?? ShoppingCategories[^1];

    public static Category ExpenseCategory(string? name)
    {
        Category? known = ExpenseCategories.FirstOrDefault(c => c.Name == name);

        if (known is not null || string.IsNullOrWhiteSpace(name) || name == DefaultShoppingCategory)
        {
            return known ?? ExpenseCategories[^1];
        }

        int hash = 17;

        foreach (char character in name)
        {
            hash = unchecked(hash * 31 + char.ToLowerInvariant(character));
        }

        return new Category(name, "🏷️", CategoryPalette[(hash & int.MaxValue) % CategoryPalette.Length]);
    }

    public static string GuessCategory(string item)
    {
        string text = item.ToLowerInvariant();

        foreach ((string category, string[] keys) in GuessMap)
        {
            if (keys.Any(text.Contains))
            {
                return category;
            }
        }

        return DefaultShoppingCategory;
    }

    public static string Label(HomePriority priority) => priority switch
    {
        HomePriority.Low => "Baixa",
        HomePriority.Medium => "Média",
        HomePriority.High => "Alta",
        _ => "Urgente"
    };

    public static string Label(HomeStatus status) => status switch
    {
        HomeStatus.Idea => "Ideias",
        HomeStatus.Planned => "Planeado",
        HomeStatus.InProgress => "Em curso",
        _ => "Concluído"
    };

    public static string Label(HomeKind kind) => kind switch
    {
        HomeKind.Purchase => "Compra",
        HomeKind.Renovation => "Obra",
        HomeKind.Repair => "Reparação",
        HomeKind.Maintenance => "Manutenção",
        _ => "Decoração"
    };

    public static string Emoji(HomeKind kind) => kind switch
    {
        HomeKind.Purchase => "🛍️",
        HomeKind.Renovation => "🧱",
        HomeKind.Repair => "🔧",
        HomeKind.Maintenance => "🧰",
        _ => "🪴"
    };

    public static string Color(HomePriority priority) => priority switch
    {
        HomePriority.Low => "#9AA3B8",
        HomePriority.Medium => "#56CCF2",
        HomePriority.High => "#F2994A",
        _ => "#EF4444"
    };

    public static string Color(HomeStatus status) => status switch
    {
        HomeStatus.Idea => "#9AA3B8",
        HomeStatus.Planned => "#56CCF2",
        HomeStatus.InProgress => "#8B9CFF",
        _ => "#6FCF97"
    };

    public static string Label(TransactionType type) => type switch
    {
        TransactionType.Income => "Entrada",
        TransactionType.Expense => "Saída",
        _ => "Poupança"
    };

    public static string Label(FamilyEventKind kind) => kind switch
    {
        FamilyEventKind.Appointment => "Compromisso",
        FamilyEventKind.Birthday => "Aniversário",
        FamilyEventKind.Family => "Família",
        FamilyEventKind.Health => "Saúde",
        FamilyEventKind.School => "Escola",
        _ => "Lembrete"
    };

    public static string Emoji(FamilyEventKind kind) => kind switch
    {
        FamilyEventKind.Appointment => "📌",
        FamilyEventKind.Birthday => "🎂",
        FamilyEventKind.Family => "👨‍👩‍👧",
        FamilyEventKind.Health => "🩺",
        FamilyEventKind.School => "🎒",
        _ => "🔔"
    };

    public static string Color(FamilyEventKind kind) => kind switch
    {
        FamilyEventKind.Appointment => "#8B9CFF",
        FamilyEventKind.Birthday => "#FF9AB5",
        FamilyEventKind.Family => "#D4B483",
        FamilyEventKind.Health => "#FF7A6B",
        FamilyEventKind.School => "#56CCF2",
        _ => "#6FCF97"
    };

    public static string Label(EventRecurrence recurrence) => recurrence switch
    {
        EventRecurrence.None => "Não repete",
        EventRecurrence.Weekly => "Todas as semanas",
        EventRecurrence.Monthly => "Todos os meses",
        _ => "Todos os anos"
    };

    public static string Money(decimal value) => value.ToString("C", Culture);

    public static string MoneyShort(decimal value) =>
        Math.Abs(value) >= 1000 ? (value / 1000m).ToString("0.#", Culture) + " mil €" : value.ToString("0", Culture) + " €";

    public static string Quantity(decimal quantity) => quantity.ToString("0.##", Culture);

    public static string MonthName(int month) => Capitalize(Culture.DateTimeFormat.GetMonthName(month));

    public static string Capitalize(string text) => string.IsNullOrEmpty(text) ? text : char.ToUpper(text[0], Culture) + text[1..];

    public static string Initials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "?";
        }

        string[] parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return parts.Length == 1 ? parts[0][..1].ToUpperInvariant() : (parts[0][..1] + parts[^1][..1]).ToUpperInvariant();
    }

    public static string NewInviteCode()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        return new string([.. Enumerable.Range(0, Household.InviteCodeLength).Select(_ => alphabet[Random.Shared.Next(alphabet.Length)])]);
    }
}
