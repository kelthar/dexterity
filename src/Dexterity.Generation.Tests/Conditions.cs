using System.Globalization;
using static Dexterity.Generation.Tests.Game;

namespace Dexterity.Generation.Tests;

public sealed partial class AcceptanceTests
{
    // CON-01..07, CON-12, NUM-03, NUM-05.
    [Theory]
    [InlineData(ConditionOperator.Equal, "orc", "orc", true)]
    [InlineData(ConditionOperator.Equal, "orc", "Orc", false)]
    [InlineData(ConditionOperator.NotEqual, "orc", "human", true)]
    [InlineData(ConditionOperator.NotEqual, null, "orc", false)]
    [InlineData(ConditionOperator.LessThan, "5", "5", false)]
    [InlineData(ConditionOperator.LessOrEqual, "5", "5", true)]
    [InlineData(ConditionOperator.GreaterThan, "5", "5", false)]
    [InlineData(ConditionOperator.GreaterOrEqual, "5", "5", true)]
    [InlineData(ConditionOperator.GreaterThan, "1.5", "1", true)]
    [InlineData(ConditionOperator.GreaterThan, "1,5", "1", false)]
    [InlineData(ConditionOperator.GreaterThan, "1,000", "1", false)]
    [InlineData(ConditionOperator.GreaterThan, "NaN", "1", false)]
    public void Conditions_Should_UseOrdinalStringsAndValidatedInvariantNumbers(ConditionOperator op, string? context, string operand, bool allowed)
    {
        var g = new Game();
        g.Template.Conditions.Add(new GenerationCondition { Key = "key", Operator = op, Value = operand });
        var request = g.Request();
        if (context != null)
            request.Context.Add("key", context);
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("sv-SE");
            var result = g.Service.Create(request);
            if (allowed)
                Ok(result);
            else
                Fail(result);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
        g.Random.Calls.ShouldBeEmpty();
    }

    // CON-08..11, CHN-03, WGT-14: skipped branches do not consume random draws.
    [Fact]
    public void Conditions_Should_ReadOnlyRequestContextAndSkipWholeBranches()
    {
        var g = new Game();
        g.Template.Items = new()
        {
            new GenerationValue { Id = "level", Kind = GenerationValueKind.Fixed, Value = "5" },
            new GenerationGroup { Id = "conditional", ChanceToGet = 50, Items = new() { new GenerationValue { Id = "hidden", Kind = GenerationValueKind.Random, MaxValue = 10 } }, Conditions = new() { new() { Key = "level", Value = "5" } } },
            new GenerationValue { Id = "visible" }
        };
        var report = g.Create(10).Generation;
        report.Values.ShouldNotContainKey("hidden");
        report.Values["visible"].ShouldBe("10");
        g.Random.Calls.ShouldBeEmpty();
    }

    // NUM-01..04, NUM-06..08, MAP-08..09, FIX-04.
    [Theory]
    [InlineData("1.5", true)]
    [InlineData("1000.5", true)]
    [InlineData("+2.5", true)]
    [InlineData("1.000", true)]
    [InlineData("1,5", false)]
    [InlineData("1,000", false)]
    [InlineData("1 000", false)]
    [InlineData("1'000", false)]
    [InlineData("1\u00a0000", false)]
    [InlineData("1,000.5", false)]
    [InlineData(" 1.5", false)]
    [InlineData("1.5 ", false)]
    [InlineData("1e3", false)]
    [InlineData(".5", false)]
    [InlineData("1.", false)]
    [InlineData("1.2.3", false)]
    [InlineData("NaN", false)]
    [InlineData("Infinity", false)]
    public void NumericBindings_Should_RejectSeparatorsAndMalformedDecimals(string text, bool valid)
    {
        var g = new Game();
        g.Template.Items.Add(new GenerationValue { Id = "number", Kind = GenerationValueKind.Fixed, Value = text });
        g.Template.Modifiers.Add(new ModifierTemplate { Base = new ModifierData { TypeId = "multiplier", EffectTypeId = 1, Scope = Scope.Use }, DataKeys = new() { ["multiplier"] = "number" } });
        var result = g.Service.Create(g.Request());
        if (valid)
            g.Component(Ok(result)).ModifierData.Single().Data["multiplier"].ShouldBe(double.Parse(text, CultureInfo.InvariantCulture));
        else
        {
            Fail(result);
            g.World.Repository.Items.ShouldBeEmpty();
        }
    }

    [Theory]
    [InlineData("1,5")]
    [InlineData("NaN")]
    [InlineData("1e3")]
    public void NumericConditionOperands_Should_FailValidationBeforeRandomness(string operand)
    {
        var g = new Game();
        g.Template.Conditions.Add(new GenerationCondition { Key = "key", Operator = ConditionOperator.GreaterThan, Value = operand });
        Fail(g.Service.Create(g.Request()));
        g.Random.Calls.ShouldBeEmpty();
        g.World.Repository.Items.ShouldBeEmpty();
    }

    // NUM-07, MAP-10.
    [Fact]
    public void StringData_Should_RetainSeparatorsWithoutNumericParsing()
    {
        var g = new Game();
        g.Template.Items.Add(new GenerationValue { Id = "text", Kind = GenerationValueKind.Fixed, Value = "1,000" });
        g.Template.DataBindings.Add(new DataBinding { Key = "text", ValueKey = "text" });
        g.Component(g.Create()).MetaData["text"].ShouldBe("1,000");
    }
}
