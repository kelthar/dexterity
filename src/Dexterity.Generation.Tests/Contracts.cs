using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using static Dexterity.Generation.Tests.Game;

namespace Dexterity.Generation.Tests;

public sealed partial class AcceptanceTests
{
    // HLP-01..04: authoring helpers preserve arguments and append, never replace.
    [Fact]
    public void AuthoringHelpers_Should_PreserveParametersAndRejectDuplicateDefinitions()
    {
        var node = new GenerationNode();
        var group = node.AddGroup("group", 3, 4, 25);
        (group.Weight, group.VaryWeightBy, group.ChanceToGet).ShouldBe((3, 4, 25));
        var random = group.AddRandomValue("random", 2, 5, 4, 25, 3, 2);
        (random.Kind, random.MinValue, random.MaxValue, random.WorthPerValue, random.ChanceToGet, random.Weight, random.VaryWeightBy)
            .ShouldBe((GenerationValueKind.Random, 2, 5, 4, 25, 3, 2));
        var weight = group.AddWeightValue("weight", 4, 2, 30, 5);
        (weight.Kind, weight.Weight, weight.WorthPerValue, weight.ChanceToGet, weight.VaryWeightBy).ShouldBe((GenerationValueKind.Weighted, 4, 2, 30, 5));
        var fixedValue = group.AddFixedValue("fixed", "text", 25);
        (fixedValue.Kind, fixedValue.Value, fixedValue.ChanceToGet).ShouldBe((GenerationValueKind.Fixed, "text", 25));
        node.Items.ShouldBe(new[] { group });
        group.Items.ShouldBe(new IGenerationEntry[] { random, weight, fixedValue });
        var g = new Game();
        g.Template.AddWeightValue("same", 1);
        g.Template.AddFixedValue("same", "x");
        g.Template.Items.Count.ShouldBe(2);
        Fail(g.Service.Validate());
    }

    // DET-01..03, DET-09, MAP-19.
    [Theory]
    [InlineData(0)]
    [InlineData(-123)]
    [InlineData(42)]
    public void SeededCreates_Should_ReproduceReportsWithoutConsumingSharedRandomness(int seed)
    {
        var g = new Game();
        g.Template.Items.Add(new GenerationValue { Id = "random", Kind = GenerationValueKind.Random, MinValue = 1, MaxValue = 100, VaryWeightBy = 5 });
        var first = g.Create(100, seed);
        var second = g.Create(100, seed);
        first.Component.Id.ShouldNotBe(second.Component.Id);
        first.Generation.Values.ShouldBe(second.Generation.Values);
        first.Generation.TotalWorth.ShouldBe(second.Generation.TotalWorth);
        g.Random.Calls.ShouldBeEmpty();
        ((IRandomSource)g.World.Random).DidNotReceive().Range(Arg.Any<double>(), Arg.Any<double>());
        ((IRandomSource)g.World.Random).DidNotReceive().Roll();
    }

    // DET-05, DET-07: failure advances only the injected stream, not repository identity.
    [Fact]
    public void FailedMapping_Should_KeepConsumedDrawsButAllocateNoIdentity()
    {
        var g = new Game();
        g.Template.Items.Add(new GenerationValue { Id = "current", Kind = GenerationValueKind.Random, MinValue = 1, MaxValue = 9 });
        g.Template.Items.Add(new GenerationValue { Id = "max", Kind = GenerationValueKind.Fixed, Value = "0" });
        g.Template.StatBindings.Add(new StatBinding { StatTypeId = Durability, MaxKey = "max", CurrentKey = "current" });
        g.Random.Values.Enqueue(7);
        g.Random.Values.Enqueue(3);
        Fail(g.Service.Create(g.Request()));
        g.Random.Calls.Count.ShouldBe(1);
        g.Template.StatBindings.Clear();
        var created = g.Create();
        created.Component.Id.ShouldBe(1);
        created.Generation.Values["current"].ShouldBe("3");
    }

    // DET-10: inclusive full-width integer bounds, no max+1 overflow.
    [Fact]
    public void DefaultRandom_Should_SupportInclusiveIntegerEndpoints()
    {
        var random = new DefaultGenerationRandom(5);
        random.Next(int.MaxValue, int.MaxValue).ShouldBe(int.MaxValue);
        random.Next(int.MinValue, int.MinValue).ShouldBe(int.MinValue);
        var results = Enumerable.Range(0, 100).Select(_ => random.Next(0, 1)).ToArray();
        results.ShouldContain(0);
        results.ShouldContain(1);
        foreach (var value in Enumerable.Range(0, 50).Select(_ => random.Next(int.MinValue, int.MaxValue)))
            value.ShouldBeInRange(int.MinValue, int.MaxValue);
    }

    // DET-11, RES-05.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BrokenRandomProviders_Should_ReturnFailureWithoutCreating(bool throws)
    {
        var g = new Game();
        g.Template.Items.Add(new GenerationValue { Id = "a", ChanceToGet = 50 });
        var random = Substitute.For<IGenerationRandom>();
        random.Next(0, 99).Returns(_ => throws ? throw new InvalidOperationException("random failed") : 100);
        Fail(new GenerationService(g.World, g.Catalog, random).Create(g.Request()));
        g.World.Repository.Items.ShouldBeEmpty();
    }

    // PRJ-03..04: explicit registration is unambiguous with both namespaces imported.
    [Fact]
    public void DependencyInjection_Should_UseConfiguredWorldCatalogAndRandom()
    {
        var g = new Game();
        g.SwordTemplate();
        var services = new ServiceCollection();
        services.AddSingleton(g.World);
        services.AddSingleton(g.Catalog);
        services.AddSingleton<IGenerationRandom>(g.Random);
        Dexterity.ServiceExtensions.Register(services);
        Dexterity.Generation.ServiceExtensions.Register(services);
        using var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IGenerationService>();
        Ok(service.Create(g.Request())).Generation.TotalWorth.ShouldBe(40);
        provider.GetRequiredService<IGenerationRandom>().ShouldBeSameAs(g.Random);
        g.World.Repository.Items.Count.ShouldBe(1);
    }
}
