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
