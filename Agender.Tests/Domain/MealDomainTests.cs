using Agender.Infrastructure.Services;
using Agender.Shared.Domain;
using FluentAssertions;

namespace Agender.Tests.Domain;

public class MealDomainTests
{
    [Theory]
    [InlineData("500 g de batatas para assar", 500, "g", "Batatas para assar")]
    [InlineData("1 L vinho tinto", 1000, "ml", "Vinho tinto")]
    [InlineData("1,5 dl vinho do Porto", 150, "ml", "Vinho do Porto")]
    [InlineData("2 unid. pau de canela", 2, null, "Pau de canela")]
    [InlineData("3 dentes de alho", 3, "dente", "Alho")]
    [InlineData("2 colheres de sopa de azeite", 2, "c. sopa", "Azeite")]
    [InlineData("1/2 kg de farinha", 500, "g", "Farinha")]
    [InlineData("2 pimentos (vermelho e verde)", 2, null, "Pimentos (vermelho e verde)")]
    [InlineData("1 c.café de sal", 1, "c. café", "Sal")]
    public void Parse_QuantifiedLine_ExtractsQuantityUnitAndName(string line, double quantity, string? unit, string name)
    {
        ParsedIngredient parsed = IngredientParser.Parse(line);

        parsed.Quantity.Should().Be((decimal)quantity);
        parsed.Unit.Should().Be(unit);
        parsed.Name.Should().Be(name);
    }

    [Theory]
    [InlineData("Sal q.b.", "Sal")]
    [InlineData("1 q.b. casca de laranja", "Casca de laranja")]
    [InlineData("Azeite a gosto", "Azeite")]
    public void Parse_ToTasteLine_HasNoQuantity(string line, string name)
    {
        ParsedIngredient parsed = IngredientParser.Parse(line);

        parsed.Quantity.Should().BeNull();
        parsed.ToTaste.Should().BeTrue();
        parsed.Name.Should().Be(name);
    }

    [Fact]
    public void Aggregate_SameIngredientAcrossRecipes_SumsScaledQuantities()
    {
        List<MealIngredientLine> lines = IngredientParser.Aggregate(
            [("2 cebolas", 1m, "Bacalhau"), ("1 cebola picada", 2m, "Arroz"), ("400 g de arroz", 0.5m, "Arroz"), ("Sal q.b.", 1m, "Bacalhau")],
            ["Sal"]);

        lines.Should().ContainSingle(l => l.Key == "cebola").Which.Quantity.Should().Be(4);
        lines.Should().ContainSingle(l => l.Key == "arroz").Which.Quantity.Should().Be(200);
        MealIngredientLine salt = lines.Should().ContainSingle(l => l.Key == "sal").Subject;
        salt.IsPantry.Should().BeTrue();
        salt.Include.Should().BeFalse();
    }

    [Fact]
    public void Aggregate_GramsOverOneKilo_ConvertsToKilograms()
    {
        List<MealIngredientLine> lines = IngredientParser.Aggregate([("800 g de batatas", 1m, "A"), ("700 g batatas", 1m, "B")], []);

        MealIngredientLine potatoes = lines.Should().ContainSingle().Subject;
        potatoes.Quantity.Should().Be(1.5m);
        potatoes.Unit.Should().Be("kg");
        potatoes.Recipes.Should().Equal("A", "B");
    }

    [Fact]
    public void Aggregate_HalfOfThreeCloves_RoundsUpToWholeCloves() =>
        IngredientParser.Aggregate([("3 dentes de alho", 0.5m, "A")], []).Should().ContainSingle().Which.Quantity.Should().Be(2);

    [Fact]
    public void Aggregate_SomeInStock_BuysOnlyTheDifference()
    {
        MealIngredientLine eggs = IngredientParser.Aggregate([("6 ovos", 1m, "Omelete")], [], [new InventoryItem { Name = "Ovos", Quantity = 2 }]).Single();

        eggs.Quantity.Should().Be(4);
        eggs.Include.Should().BeTrue();
        eggs.AtHome.Should().Be("2 un");
    }

    [Fact]
    public void Aggregate_EnoughInStock_LeavesItOffTheList()
    {
        MealIngredientLine rice = IngredientParser.Aggregate([("400 g de arroz agulha", 1m, "Arroz de pato")], [], [new InventoryItem { Name = "Arroz", Quantity = 2000, Unit = "g" }]).Single();

        rice.IsCovered.Should().BeTrue();
        rice.Include.Should().BeFalse();
        rice.AtHome.Should().Be("2 kg");
    }

    [Fact]
    public void Aggregate_StockInDifferentUnit_KeepsFullQuantityWithNote()
    {
        MealIngredientLine chickpeas = IngredientParser.Aggregate([("200 g de grão", 1m, "Salada")], [], [new InventoryItem { Name = "Grão", Quantity = 1, Unit = "lata" }]).Single();

        chickpeas.Quantity.Should().Be(200);
        chickpeas.Include.Should().BeTrue();
        chickpeas.AtHome.Should().Be("1 lata");
    }

    [Fact]
    public void Aggregate_PantryPrefix_DoesNotMatchDifferentWord()
    {
        List<MealIngredientLine> lines = IngredientParser.Aggregate([("1 molho de salsa", 1m, "A"), ("Pimenta preta q.b.", 1m, "A")], ["Sal", "Pimenta"]);

        lines.Single(l => l.Name.Contains("salsa", StringComparison.OrdinalIgnoreCase)).IsPantry.Should().BeFalse();
        lines.Single(l => l.Key.StartsWith("pimenta", StringComparison.Ordinal)).IsPantry.Should().BeTrue();
    }

    [Fact]
    public void RecipeScraper_Parse_ReadsJsonLdRecipeInsideGraph()
    {
        string html = """
            <html><head>
            <script type="application/ld+json">{"@context":"https://schema.org","@graph":[{"@type":"WebPage"},
              {"@type":"Recipe","name":"Bacalhau na Brasa","image":["/media/bacalhau.jpg"],"prepTime":"PT50M","totalTime":"PT0M",
               "recipeYield":"2 pessoas","recipeCategory":"Peixe","recipeIngredient":["2 lombos de bacalhau","Azeite q.b."],
               "recipeInstructions":[{"@type":"HowToStep","text":"Preparar as brasas."},{"@type":"HowToStep","text":"Grelhar o bacalhau &amp; servir."}]}]}</script>
            </head></html>
            """;

        ScrapedRecipe? recipe = RecipeScraper.Parse(html, new Uri("https://feed.continente.pt/receitas/bacalhau-na-brasa"));

        recipe.Should().NotBeNull();
        recipe!.Title.Should().Be("Bacalhau na Brasa");
        recipe.ImageUrl.Should().Be("https://feed.continente.pt/media/bacalhau.jpg");
        recipe.Minutes.Should().Be(50);
        recipe.Servings.Should().Be(2);
        recipe.Category.Should().Be("Peixe");
        recipe.Ingredients.Should().Equal("2 lombos de bacalhau", "Azeite q.b.");
        recipe.Instructions.Should().Be("Preparar as brasas.\nGrelhar o bacalhau & servir.");
    }

    [Fact]
    public void RecipeScraper_Parse_UnquotedScriptType_ReadsRecipe()
    {
        string html = """<script type=application/ld+json>{"@context":"https:\/\/schema.org\/","@type":"Recipe","name":"Arroz de pato","recipeYield":4,"recipeIngredient":["1 pato"]}</script>""";

        ScrapedRecipe? recipe = RecipeScraper.Parse(html, new Uri("https://www.pingodoce.pt/receitas/arroz-de-pato/"));

        recipe.Should().NotBeNull();
        recipe!.Title.Should().Be("Arroz de pato");
        recipe.Servings.Should().Be(4);
    }

    [Fact]
    public void RecipeScraper_Parse_PageWithoutRecipe_ReturnsNull() =>
        RecipeScraper.Parse("""<script type="application/ld+json">{"@type":"WebPage","name":"Receitas"}</script>""", new Uri("https://x.pt/"))
            .Should().BeNull();

    [Theory]
    [InlineData("http://localhost:5018/api", false)]
    [InlineData("http://192.168.1.10/receita", false)]
    [InlineData("ftp://site.pt/receita", false)]
    [InlineData("https://www.pingodoce.pt/receitas/arroz-de-pato/", true)]
    public void RecipeScraper_TryCreateSafeUri_BlocksLocalAddresses(string url, bool expected) =>
        RecipeScraper.TryCreateSafeUri(url, out _).Should().Be(expected);

    [Theory]
    [InlineData("Bacalhau à Brás", "Peixe", RecipeCourse.Main)]
    [InlineData("Tarte de maçã", "Tartes", RecipeCourse.Dessert)]
    [InlineData("Tarte de legumes", "Tartes", RecipeCourse.Main)]
    [InlineData("Bolo de cenoura", null, RecipeCourse.Dessert)]
    [InlineData("Bolo de carne", "Sem categoria", RecipeCourse.Main)]
    [InlineData("Queques de ovo com legumes", "Ovos", RecipeCourse.Main)]
    [InlineData("Batido de banana e espinafres", "Sem categoria", RecipeCourse.Drink)]
    [InlineData("Gelado de kiwi", "Sem categoria", RecipeCourse.Dessert)]
    [InlineData("Pão de cebola com orégãos", "Sem categoria", RecipeCourse.Other)]
    [InlineData("Hummus de cenoura", "Vegetais", RecipeCourse.Other)]
    [InlineData("Caldo verde", "Sopas", RecipeCourse.Main)]
    [InlineData("Cheesecake de caramelo", "Doces e compotas", RecipeCourse.Dessert)]
    [InlineData("Mojito", "Bebida", RecipeCourse.Drink)]
    [InlineData("Arroz de pato", null, RecipeCourse.Main)]
    [InlineData("Batata-doce assada", null, RecipeCourse.Main)]
    [InlineData("Esparguete com molho de pimentos e burrata", "Massas", RecipeCourse.Main)]
    [InlineData("Koftas com hummus e salada", "Carne", RecipeCourse.Main)]
    public void RecipeClassifier_Course_SeparatesMealsFromDessertsAndDrinks(string title, string? category, RecipeCourse expected) =>
        RecipeClassifier.Course(title, category).Should().Be(expected);

    [Theory]
    [InlineData("Lombos de bacalhau no forno", null, null, MealProtein.Fish)]
    [InlineData("Hambúrguer de beterraba", null, null, MealProtein.Veggie)]
    [InlineData("Arroz de pato", null, null, MealProtein.Meat)]
    [InlineData("Massa à primavera", "Massas", "500 g de massa\n1 curgete", MealProtein.Veggie)]
    [InlineData("Arroz de forno", null, "2 peitos de frango\n300 g de arroz", MealProtein.Meat)]
    [InlineData("Lasanha de vegetais grelhados", null, null, MealProtein.Veggie)]
    [InlineData("Bife de Seitan Grelhado", "Vegetariano", null, MealProtein.Veggie)]
    [InlineData("Lasanha à bolonhesa", null, null, MealProtein.Meat)]
    public void RecipeClassifier_Protein_DetectsMeatFishOrVeggie(string title, string? category, string? ingredients, MealProtein expected) =>
        RecipeClassifier.Protein(title, category, ingredients).Should().Be(expected);

    [Fact]
    public void HomeMatcher_Match_CountsNonPantryIngredientsAtHomeAndFlagsExpiring()
    {
        DateOnly today = new(2026, 11, 10);
        List<(string, InventoryItem)> stock = HomeMatcher.Stock(
        [
            new InventoryItem { Name = "Ovos", Quantity = 6 },
            new InventoryItem { Name = "Natas", Quantity = 1, Unit = "emb", ExpiresOn = today.AddDays(2) }
        ]);

        HomeMatch? match = HomeMatcher.Match(HomeMatcher.KeysOf(["4 ovos", "200 ml de natas", "100 g de bacon", "Sal q.b.", "2 c. sopa de azeite"]),
            HomeMatcher.PantryKeys(["Sal", "Azeite"]), stock, today);

        match.Should().NotBeNull();
        match!.Have.Should().Be(2);
        match.Total.Should().Be(3);
        match.Expiring.Should().Equal("Natas");
    }

    [Fact]
    public void MealVariety_Analyze_WarnsAboutStreaksMissingFishAndRepeats()
    {
        Guid repeated = Guid.NewGuid();
        DateOnly monday = new(2026, 11, 16);
        List<MealVariety.PlannedMeal> meals =
        [
            new(monday, MealSlot.Lunch, repeated, "Arroz de pato", MealProtein.Meat, false),
            new(monday, MealSlot.Dinner, Guid.NewGuid(), "Bifanas", MealProtein.Meat, false),
            new(monday.AddDays(1), MealSlot.Lunch, repeated, "Arroz de pato", MealProtein.Meat, true),
            new(monday.AddDays(1), MealSlot.Dinner, Guid.NewGuid(), "Frango assado", MealProtein.Meat, false),
            new(monday.AddDays(2), MealSlot.Lunch, Guid.NewGuid(), "Sopa", MealProtein.Veggie, false),
            new(monday.AddDays(2), MealSlot.Dinner, Guid.NewGuid(), "Omelete", MealProtein.Veggie, false),
            new(monday.AddDays(2), MealSlot.Dessert, Guid.NewGuid(), "Pudim", null, false)
        ];

        VarietySummary summary = MealVariety.Analyze(meals, new HashSet<Guid> { repeated });

        summary.Meat.Should().Be(4);
        summary.Veggie.Should().Be(2);
        summary.Notes.Should().Contain(n => n.Contains("4 refeições seguidas de carne"));
        summary.Notes.Should().Contain("🐟 Ainda não há peixe esta semana");
        summary.Notes.Should().Contain("📅 Arroz de pato já foi na semana passada");
        summary.Notes.Should().NotContain(n => n.Contains("aparece"));
    }

    [Fact]
    public void MealCatalog_NextMeal_DinnerLeftoversGoToNextLunch()
    {
        MealCatalog.NextMeal(new DateOnly(2026, 11, 16), MealSlot.Dinner).Should().Be((new DateOnly(2026, 11, 17), MealSlot.Lunch));
        MealCatalog.NextMeal(new DateOnly(2026, 11, 16), MealSlot.Lunch).Should().Be((new DateOnly(2026, 11, 16), MealSlot.Dinner));
        MealCatalog.NextMeal(new DateOnly(2026, 11, 16), MealSlot.Dessert).Should().BeNull();
    }

    [Theory]
    [InlineData("2026-11-15T18:00", "2026-11-15T18:00", "2026-11-16")]
    [InlineData("2026-11-15T17:59", "2026-11-08T18:00", "2026-11-09")]
    [InlineData("2026-11-17T09:00", "2026-11-15T18:00", "2026-11-16")]
    public void Schedule_SundayAtSix_TargetsFollowingWeek(string now, string expectedMoment, string expectedWeek)
    {
        DateTime moment = MealCatalog.LastScheduled(DateTime.Parse(now), DayOfWeek.Sunday, new TimeOnly(18, 0));

        moment.Should().Be(DateTime.Parse(expectedMoment));
        MealCatalog.TargetWeek(DateOnly.FromDateTime(moment)).Should().Be(DateOnly.Parse(expectedWeek));
    }
}
