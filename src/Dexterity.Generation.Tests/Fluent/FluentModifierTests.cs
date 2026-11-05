using static Dexterity.Generation.Tests.Game;
using static Dexterity.Generation.Tests.FluentTestSupport;

namespace Dexterity.Generation.Tests;

public sealed class FluentModifierTests
{
    /// <summary>MOD-01–14, PAR-01: every typed input matches an independently initialized manual template.</summary>
    [Theory]
    [ClassData(typeof(FluentModifierCases))]
    public void Parameters_match_manual_templates(string type, string key, int strategy)
    {
        var fluent = new Game();
        var manual = new Game();
        Fluent(fluent.Template.EffectType(1), type, key, strategy);
        Manual(manual.Template, manual.Template, type, key, strategy);

        var result = Parity(fluent, manual, 40, strategy == 2 ? new[] { 11 } : Array.Empty<int>());
        result.Generation.Values[$"fluent/0/{key}"].ShouldBe("10");
        result.Component.ModifierData.Single().Data[key].ShouldBe(10);
        result.Generation.TotalWorth.ShouldBe(strategy == 0 ? 0 : 40);
        result.Generation.UnusedWorth.ShouldBe(strategy == 0 ? 40 : 0);
        result.Generation.Overspend.ShouldBe(0);
        fluent.Random.Calls.ShouldBe(strategy == 2 ? new[] { (8, 12) } : Array.Empty<(int, int)>());
    }

    /// <summary>DUR-01: duration supports all strategies on every built-in modifier without touching Base.Duration.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Duration_matches_manual_bindings(int strategy)
    {
        foreach (var type in Types)
        {
            var fluent = new Game();
            var manual = new Game();
            Fluent(fluent.Template.EffectType(1), type, strategy: strategy, duration: true);
            Manual(manual.Template, manual.Template, type, strategy: strategy, duration: true);
            var result = Parity(fluent, manual, 10, strategy == 2 ? new[] { 4 } : Array.Empty<int>());
            result.Component.ModifierData.Single().Duration.ShouldBe(strategy == 0 ? 2.5 : strategy == 1 ? 5 : 4);
            fluent.Template.Modifiers.Single().Base.Duration.ShouldBe(0);
        }
    }

    /// <summary>MOD-15/16: mixed bound strategies retain distinct generation-time and runtime randomness.</summary>
    [Fact]
    public void Mixed_bounds_and_splash_are_generated_once()
    {
        var game = new Game();
        game.Template.EffectType(1).RandomDecrease(x => x.Min.Fixed(5).Max.Weight(2));
        var report = game.Create(40);
        report.Component.ModifierData.Single().Data.ShouldBe(new Dictionary<string, double> { ["min"] = 5, ["max"] = 20 });
        report.Generation.TotalWorth.ShouldBe(40);

        game = new Game();
        game.Template.EffectType(1).RandomIncrease(x => x.Min.Fixed(3).Max.Random(1, 6, 12));
        game.Random.Values.Enqueue(9);
        game.Create(20).Component.ModifierData.Single().Data["max"].ShouldBe(9);

        game = new Game();
        game.Template.EffectType(1).Splash(x => x.Range.Fixed(3).Percent.Random(1, 25, 50));
        game.Random.Values.Enqueue(40);
        var splash = game.Create(50);
        splash.Component.ModifierData.Single().Data["percent"].ShouldBe(40);
        splash.Generation.TotalWorth.ShouldBe(40);
        game.Random.Calls.ShouldBe(new[] { (25, 50) });
    }

    /// <summary>API-02: typed accessors expose only the parameters appropriate to their modifier.</summary>
    [Fact]
    public void Typed_surface_has_no_unrelated_parameters()
    {
        typeof(CriticalModifierBuilder).GetProperties().Select(x => x.Name).Order().ShouldBe(new[] { "Chance", "Duration", "Multiplier" });
        typeof(ValueModifierBuilder).GetProperties().Select(x => x.Name).Order().ShouldBe(new[] { "Duration", "Value" });
        typeof(RangeModifierBuilder).GetProperties().Select(x => x.Name).Order().ShouldBe(new[] { "Duration", "Max", "Min" });
        typeof(SplashModifierBuilder).GetProperties().Select(x => x.Name).Order().ShouldBe(new[] { "Duration", "Percent", "Range" });
    }
}
