using NSubstitute;
using static Dexterity.Generation.Tests.Game;

namespace Dexterity.Generation.Tests;

public sealed partial class AcceptanceTests
{
    // CRT-06..11, MAP-06..08: initial and calculated stats have separate meanings.
    [Theory]
    [InlineData(null, 50, 50)]
    [InlineData(0d, 50, 0)]
    [InlineData(12d, 50, 12)]
    [InlineData(12d, 8, 8)]
    public void InitialCurrents_Should_UseFinalMaximumOrPreserveExplicitValue(double? current, double calculated, double expected)
    {
        var g = new Game();
        g.Template.TypeId = Character;
        g.Template.CategoryId = 1;
        g.Template.Items = new()
        {
            new GenerationValue { Id = "str", Kind = GenerationValueKind.Fixed, Value = "5" },
            new GenerationValue { Id = "hp", Kind = GenerationValueKind.Fixed, Value = "20" },
            new GenerationValue { Id = "current", Kind = GenerationValueKind.Fixed, Value = (current ?? 12).ToString(System.Globalization.CultureInfo.InvariantCulture) },
            new GenerationValue { Id = "stun", Kind = GenerationValueKind.Fixed, Value = "10" }
        };
        g.Template.StatBindings = new()
        {
            new() { StatTypeId = Str, MaxKey = "str" },
            new() { StatTypeId = HP, MaxKey = "hp", CurrentKey = current.HasValue ? "current" : null },
            new() { StatTypeId = Stun, MaxKey = "stun" }
        };
        var calculator = new Calculator { Run = (_, _) => Result.Ok(calculated) };
        g.World.Calculators.Add(calculator);
        var result = g.Create();
        var component = g.Component(result);
        component.Stats[HP].Current.ShouldBe(expected);
        component.Stats[HP].Max.ShouldBe(calculated);
        component.Stats[Str].Current.ShouldBe(5);
        component.Stats[Stun].Current.ShouldBe(0);
        calculator.Calls.ShouldBe(new[] { component.Id });
        result.Generation.Values["hp"].ShouldBe("20");
        component.ShouldBeSameAs(result.Component);
    }

    [Fact]
    public void DerivedHealth_Should_UseGeneratedStrengthAndKeepSourceValues()
    {
        var g = new Game();
        g.Template.TypeId = Character;
        g.Template.Items.Add(new GenerationValue { Id = "str" });
        g.Template.StatBindings.Add(new StatBinding { StatTypeId = Str, MaxKey = "str" });
        g.World.Calculators.Add(new Calculator { Run = (id, world) => Result.Ok(world.Repository.Items[id].Stats[Str].ModifiedMax * 10) });
        var result = g.Create(5);
        result.Generation.Values["str"].ShouldBe("5");
        result.Component.Stats[Str].Max.ShouldBe(5);
        result.Component.Stats[HP].Max.ShouldBe(50);
        g.Component(result).Stats[HP].Current.ShouldBe(50);
    }

    [Theory]
    [InlineData("failure")]
    [InlineData("exception")]
    [InlineData("nan")]
    [InlineData("infinity")]
    public void FailedCalculators_Should_KeepCreatedComponentWithoutReusingItsId(string failure)
    {
        var g = new Game();
        var existing = Ok(g.World.Repository.Create(Character, 1));
        existing.Stats[HP].Current = 7;
        var stat = existing.Stats[HP];
        g.Template.TypeId = Character;
        var calculator = new Calculator
        {
            Run = (_, _) => failure switch
            {
                "failure" => Result.Fail("Calculation rejected.", 422),
                "exception" => throw new InvalidOperationException("calculator failed"),
                "nan" => Result.Ok(double.NaN),
                _ => Result.Ok(double.PositiveInfinity)
            }
        };
        g.World.Calculators.Add(calculator);
        Fail(g.Service.Create(g.Request()));
        g.World.Repository.Items.Count.ShouldBe(2);
        g.World.Repository.Items[2].TypeId.ShouldBe(Character);
        existing.Stats[HP].ShouldBeSameAs(stat);
        stat.Current.ShouldBe(7);
        g.World.Calculators.Clear();
        g.Create().Component.Id.ShouldBe(3);
    }

    // Repository interactions stop at failed guards before registration or recalculation.
    [Theory]
    [InlineData("generation")]
    [InlineData("create")]
    [InlineData("binding")]
    [InlineData("attach")]
    public void CreationFailures_Should_StopAtTheFailingRepositoryGuard(string failure)
    {
        var g = new Game();
        g.SwordTemplate();
        var repository = Substitute.For<IComponentRepository>();
        repository.Items.Returns(new Dictionary<int, Component>());
        repository.Relations.Returns(new List<ComponentRelation>());
        var component = Ok(g.World.Repository.CreateDetached(Item, Sword));
        repository.CreateDetached(Item, Sword).Returns(Result.Ok(component));
        repository.Attach(component).Returns(Result.Fail("Attachment rejected.", 422));
        if (failure == "generation")
            g.Template.Name = "";
        if (failure == "create")
            repository.CreateDetached(Item, Sword).Returns(Result.Fail("Creation rejected.", 409));
        if (failure == "binding")
            g.Template.StatBindings[0].CurrentKey = "tier";
        if (failure == "binding")
            ((GenerationValue)g.Template.Items[2]).Value = "invalid";
        g.World.Repository = repository;
        Fail(g.Service.Create(g.Request()));
        if (failure == "generation")
            repository.DidNotReceive().CreateDetached(Arg.Any<int>(), Arg.Any<int>());
        if (failure != "attach")
            repository.DidNotReceive().Attach(Arg.Any<Component>());
        else
        {
            repository.Received(1).Attach(component);
            component.Stats[Durability].Max.ShouldBe(10);
            component.ModifierData.Single().Data["value"].ShouldBe(15);
        }
        component.Id.ShouldBe(-1);
        component.State.ShouldBe(ComponentState.Detached);
        repository.DidNotReceive().Create(Arg.Any<int>(), Arg.Any<int>());
        repository.DidNotReceive().SetStat(Arg.Any<int>(), Arg.Any<Stat>());
        repository.DidNotReceive().Delete(Arg.Any<int>());
        repository.DidNotReceive().Cleanup();
    }

    // CRT-21, INT-03.
    [Fact]
    public void GeneratedWands_Should_StartChargedAndUseNormalConsumption()
    {
        var g = new Game();
        g.Template.CategoryId = Wand;
        g.Template.Items.Add(new GenerationValue { Id = "charges", Kind = GenerationValueKind.Fixed, Value = "3" });
        g.Template.StatBindings.Add(new StatBinding { StatTypeId = Charges, MaxKey = "charges" });
        var component = g.Component(g.Create());
        var effects = new DefaultEffectService(g.World);
        Ok(effects.CanConsume(component.Id)).ShouldBeTrue();
        Ok(effects.Consume(component.Id));
        component.Stats[Charges].Current.ShouldBe(2);
    }
}
