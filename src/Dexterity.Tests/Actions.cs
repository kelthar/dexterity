using static Dexterity.Tests.Game;

namespace Dexterity.Tests;

public sealed partial class AcceptanceTests
{
    [Fact]
    public void Attack_Should_ApplyPassivesMitigationAndSplash()
    {
        var g = new Game();
        var sword = g.Headline();
        var results = Ok(g.Effects.Attack(g.Hero.Id, sword.Id, g.Orc.Id, new[] { g.Goblin1.Id }));
        Current(g.Orc).ShouldBe(89); // 10 physical - 3 armour + 4 fire
        Current(g.Goblin1).ShouldBe(43); // 50% of 10 physical + 4 fire
        results.Count.ShouldBe(2);
        results[0].Sum(Physical).ShouldBe(-7);
        sword.State.ShouldBe(ComponentState.Active);
    }

    [Fact]
    public void Attack_Should_ApplyLifeStealToItsRecipient()
    {
        var g = new Game();
        g.Hero.Stats[HP].Current = 50;
        var dagger = g.Own("VampDagger", MainHand);
        var results = Ok(g.Effects.Attack(g.Hero.Id, dagger.Id, g.Orc.Id));
        Current(g.Orc).ShouldBe(90);
        Current(g.Hero).ShouldBe(55);
        results.ShouldContain(x => x.TargetId == g.Hero.Id && x.Sum(Physical) == 5);
    }

    [Theory]
    [InlineData("HealingPotion", true, 60d)]
    [InlineData("HealingScroll", false, 55d)]
    public void Use_Should_ApplyHealingAndConsumeExactlyOnce(string name, bool deleted, double expected)
    {
        var g = new Game();
        g.Hero.Stats[HP].Current = 50;
        var item = g.Own(name);
        Ok(g.Effects.Use(g.Hero.Id, item.Id, g.Hero.Id));
        Current(g.Hero).ShouldBe(expected);
        item.State.ShouldBe(deleted ? ComponentState.Deleted : ComponentState.Active);
        if (!deleted)
            item.Stats[Charges].Current.ShouldBe(1);
    }

    [Fact]
    public void Use_Should_ResolveConnectedSkillsWithoutDeletingThem()
    {
        var g = new Game();
        var skill = g.Own("Fireball");
        Ok(g.Effects.Use(g.Hero.Id, skill.Id, g.Orc.Id));
        Current(g.Orc).ShouldBe(94);
        skill.State.ShouldBe(ComponentState.Active);
        g.Relation(skill).Type.ShouldBe(RelationType.Connected);
    }

    [Theory]
    [InlineData(false, 3d)]
    [InlineData(true, 2d)]
    public void Actions_Should_OnlyConsumeOnUseRegardlessOfTargetCount(bool use, double expectedCharges)
    {
        var g = new Game();
        var wand = g.Own("FireWand", MainHand);
        Ok(use ? g.Effects.Use(g.Hero.Id, wand.Id, g.Orc.Id, new[] { g.Goblin1.Id, g.Goblin2.Id })
            : g.Effects.Attack(g.Hero.Id, wand.Id, g.Orc.Id, new[] { g.Goblin1.Id, g.Goblin2.Id }));
        wand.Stats[Charges].Current.ShouldBe(expectedCharges);
        Current(g.Orc).ShouldBe(95);
    }

    [Fact]
    public void Use_Should_ScheduleTimedEffectsAndReturnDetachedReports()
    {
        var g = new Game();
        var bomb = g.Own("FireBomb");
        var report = Ok(g.Effects.Use(g.Hero.Id, bomb.Id, g.Orc.Id)).Single();
        Current(g.Orc).ShouldBe(97);
        bomb.State.ShouldBe(ComponentState.Deleted);
        g.Orc.Effects.Single().Items.Single().Duration.ShouldBe(2);
        report.Items.Clear();
        g.Orc.Effects.Single().Items.ShouldHaveSingleItem();
        Ok(g.Effects.Tick(g.Orc.Id, 1));
        Current(g.Orc).ShouldBe(96);
    }

    [Theory]
    [InlineData("target")]
    [InlineData("ownership")]
    [InlineData("unequipped")]
    [InlineData("charges")]
    public void Actions_Should_RejectInvalidActionsWithoutChangingState(string defect)
    {
        var g = new Game();
        var wand = g.Own("FireWand", MainHand);
        if (defect == "ownership")
            g.Repo.Relations.Clear();
        if (defect == "unequipped")
            g.Relation(wand).Type = RelationType.Inventory;
        if (defect == "charges")
            wand.Stats[Charges].Current = 0;
        var before = g.Snapshot();
        var target = defect == "target" ? 999 : g.Orc.Id;
        Fail(g.Effects.Attack(g.Hero.Id, wand.Id, target));
        g.Snapshot().ShouldBe(before);
        Fail(g.Effects.Use(g.Hero.Id, wand.Id, target));
        g.Snapshot().ShouldBe(before);
    }

    [Theory]
    [InlineData("incoming")]
    [InlineData("apply")]
    [InlineData("consume")]
    [InlineData("exception")]
    public void Use_Should_KeepEarlierTargetsOnPipelineFailure(string failure)
    {
        var g = new Game();
        var sword = g.Own("Longsword", MainHand);
        var service = new FailingAction(g.World, failure, g.Goblin1.Id);
        g.Own("Cleave1");
        var stat = g.Orc.Stats[HP];
        Fail(service.Use(g.Hero.Id, sword.Id, g.Orc.Id, new[] { g.Goblin1.Id }));
        Current(g.Orc).ShouldBe(90);
        Current(g.Goblin1).ShouldBe(failure == "consume" ? 45 : 50);
        g.Orc.Stats[HP].ShouldBeSameAs(stat);
    }

    private sealed class FailingAction(World world, string failure, int second) : DefaultEffectService(world)
    {
        public override Result<List<EffectResult>> Incoming(int targetId, EffectResult result)
        {
            if (failure == "incoming" && targetId == second)
                return Result.Fail("Incoming rejected.", 422);
            return base.Incoming(targetId, result);
        }

        public override Result Apply(int targetId, EffectResult result)
        {
            if (targetId == second && failure == "exception")
                throw new InvalidOperationException("Application failed.");
            if (targetId == second && failure == "apply")
                return Result.Fail("Application rejected.", 422);
            return base.Apply(targetId, result);
        }

        public override Result Consume(int instrumentId)
        {
            if (failure == "consume")
                return Result.Fail("Consumption rejected.", 422);
            return base.Consume(instrumentId);
        }
    }
}
