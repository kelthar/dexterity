using NSubstitute;
using static Dexterity.Tests.Game;

namespace Dexterity.Tests;

public sealed partial class AcceptanceTests
{
    // RES-01: Given `Physical −10, Fire −4, Physical +3 (prevented)`, then `Sum(HP) = −11`.
    [Fact]
    public void EffectResult_Should_SumImmediateItemsByStat()
    {
        var g = new Game();
        var hit = g.Hit();
        hit.Items.Add(g.Hit(Fire, -4).Items[0]);
        hit.Items.Add(g.Hit(Physical, 3).Items[0]);
        hit.SumStat(HP, g.World).ShouldBe(-11);
        hit.Sum(Fire).ShouldBe(-4);
        hit.Sum(Physical).ShouldBe(-7);
    }

    // RES-02: Given the same items, then `Sum(Fire) = −4` and `Sum(Physical) = −7`.
    [Fact]
    public void EffectResult_Should_SumImmediateItemsByEffectType()
    {
        var g = new Game();
        var hit = g.Hit();
        hit.Items.Add(g.Hit(Fire, -4).Items[0]);
        hit.Items.Add(g.Hit(Physical, 3).Items[0]);
        hit.SumStat(HP, g.World).ShouldBe(-11);
        hit.Sum(Fire).ShouldBe(-4);
        hit.Sum(Physical).ShouldBe(-7);
    }

    // RES-03: `Sum` for a stat or effect type that is not in the result is `0`.
    [Fact]
    public void MissingEffectAndStatSums_Should_BeZero()
    {
        var g = new Game();
        var hit = g.Hit();
        hit.Sum(999).ShouldBe(0);
        hit.SumStat(Mana, g.World).ShouldBe(0);
    }

    // RES-04: Given `Poison −2 × 3` with no immediate items, then `Sum(HP) = 0`, and the item is still listed (A16).
    [Fact]
    public void TimedItems_Should_NotContributeToImmediateSums()
    {
        var g = new Game();
        var hit = g.Hit(Poison, -2, 3);
        hit.SumStat(HP, g.World).ShouldBe(0);
        hit.Sum(Poison).ShouldBe(0);
        hit.Items.Count.ShouldBe(1);
    }

    // RES-05: Every item has an integer SourceId and InstrumentId identifying the modifier-owning actor and modifier-holding component. Neither field contains a Component object (D33, D38).
    [Fact]
    public void EffectItems_Should_ContainScalarProvenanceIds()
    {
        foreach (var name in new[]
        {
            nameof(EffectResultItem.SourceId),
            nameof(EffectResultItem.InstrumentId)
        }

        )
            typeof(EffectResultItem).GetProperty(name)!.PropertyType.ShouldBe(typeof(int));
    }

    // RES-06: Every result returned by Outgoing has a populated TargetId (primary or secondary), integer SourceId (the acting actor, Hero) and integer InstrumentId (the weapon, required by D5). No Component objects are embedded (D38).
    [Fact]
    public void OutgoingResults_Should_IdentifyTargetSourceAndInstrument()
    {
        var g = new Game();
        var sword = g.Headline();
        var results = g.Out(sword, g.Orc, g.Goblin1, g.Goblin2);
        results.ShouldAllBe(x => x.TargetId.HasValue && x.SourceId == g.Hero.Id && x.InstrumentId == sword.Id);
    }

    // RES-07: A modifier never changes an existing item. Items added earlier are unchanged after later modifiers run (A14).
    [Fact]
    public void Modifiers_Should_NotMutateExistingContributionItems()
    {
        var g = new Game();
        var item = g.Hit().Items[0];
        var hit = g.Hit();
        hit.Items = new()
        {
            item
        };
        hit = new Multiplier().Calculate(hit, Mod("multiplier", Physical, 2, "multiplier"), new EffectContext { OwnerId = g.Hero.Id, InstrumentId = g.Hero.Id }, g.Hero.Id);
        hit.Items[0].ShouldBeSameAs(item);
        item.Amount.ShouldBe(-10);
        hit.Items.Count.ShouldBe(2);
    }

    // RES-08: Given a melee attack with Cleave1, then Goblin1's splash result has `SourceId = Hero` and `InstrumentId = Longsword`, while its items have `SourceId = Hero`, `InstrumentId = Cleave1` (SPL-21).
    [Fact]
    public void SplashResults_Should_DistinguishActionInstrumentFromHolder()
    {
        var g = new Game();
        var sword = g.Own("Longsword", MainHand);
        var cleave = g.Own("Cleave1");
        var result = g.Out(sword, g.Orc, g.Goblin1)[1];
        result.SourceId.ShouldBe(g.Hero.Id);
        result.InstrumentId.ShouldBe(sword.Id);
        result.Items.ShouldAllBe(x => x.SourceId == g.Hero.Id && x.InstrumentId == cleave.Id);
    }

    // RES-10: Given the final tick resolves a consumed FireBomb's last pending burn and cleanup physically removes the bomb, then the returned result and its items still contain FireBomb's original integer InstrumentId and their resolved amounts. They neither embed the removed component nor prevent cleanup; a later required lookup of that Id fails (D38, D39).
    [Fact]
    public void ResolvedReports_Should_PreserveIdsAfterOriginCleanup()
    {
        var g = new Game();
        var bomb = g.Own("FireBomb");
        g.Store(g.Hit(Fire, -1, 1, instrument: bomb));
        Ok(g.Effects.Consume(bomb.Id));
        var result = g.Tick().Single();
        result.InstrumentId.ShouldBe(bomb.Id);
        result.Items.Single().InstrumentId.ShouldBe(bomb.Id);
        result.Sum(Fire).ShouldBe(-1);
        Fail(g.Repo.Get(bomb.Id));
    }

    // RES-11: Given an EffectContext for an action, then OwnerId is an integer and SourceId, TargetId and InstrumentId are nullable integers populated where the phase requires them. StatsRecalc has no InstrumentId. The context contains no Component-valued identity fields (D38, A20).
    [Fact]
    public void EffectContext_Should_UseScalarNullableActionIds()
    {
        typeof(EffectContext).GetProperty(nameof(EffectContext.OwnerId))!.PropertyType.ShouldBe(typeof(int));
        foreach (var name in new[]
        {
            nameof(EffectContext.SourceId),
            nameof(EffectContext.TargetId),
            nameof(EffectContext.InstrumentId)
        }

        )
            typeof(EffectContext).GetProperty(name)!.PropertyType.ShouldBe(typeof(int? ));
        new EffectContext
        {
            Phase = Phase.StatsRecalc,
            Trigger = Trigger.Recalc,
            OwnerId = 1
        }.InstrumentId.ShouldBeNull();
    }

    // DET-01: Given the same seed for the random source, then two runs of the same attack with the RustySword and a Precision variant (chance 50) give identical results.
    [Fact]
    public void SeededRandomSource_Should_ReproduceActionResults()
    {
        string Run()
        {
            var g = new Game();
            var random = new DefaultRandomSource(456);
            g.World.Random = random;
            g.World.Modifiers.RemoveAll(x => x.Id is "randomdecrease" or "critical");
            g.World.Modifiers.Add(new RandomDecrease(random));
            g.World.Modifiers.Add(new Critical(random));
            var sword = g.Own("RustySword", MainHand);
            g.Own("Precision").ModifierData.ForEach(x => x.Data["chance"] = 50);
            return System.Text.Json.JsonSerializer.Serialize(Enumerable.Range(0, 20).Select(_ => g.Out(sword)).ToArray());
        }

        Run().ShouldBe(Run());
    }

    // DET-03: Given a custom modifier type registered with Order 150, then it runs after Decrease (100) and before Multiplier (200), and it sees the items created by Decrease.
    [Fact]
    public void CustomModifiers_Should_RunBetweenConfiguredOrders()
    {
        var g = new Game();
        var spy = new SpyModifier
        {
            Run = (result, _, _) =>
            {
                result.Sum(Physical).ShouldBe(-10);
                return result;
            }
        };
        g.World.Modifiers.Add(spy);
        var sword = g.Own("Longsword", MainHand);
        sword.ModifierData.Add(Mod("spy", Physical, 1));
        sword.ModifierData.Add(Mod("multiplier", Physical, 2, "multiplier"));
        g.Out(sword)[0].Sum(Physical).ShouldBe(-20);
        spy.Calls.Count.ShouldBe(1);
    }

    // DET-04: Given a custom `IWorldLogic`, then it decides relations and distances (it swaps Ally to Enemy, and Ally then gets splash).
    [Fact]
    public void CustomWorldLogic_Should_ControlRelationsAndDistance()
    {
        var g = new Game();
        var sword = g.Own("Longsword", MainHand);
        g.Own("Cleave1");
        g.Logic.GetRelationKind(g.Hero.Id, g.Ally.Id).Returns(RelationKind.Enemies);
        g.Out(sword, g.Orc, g.Ally)[1].Sum(Physical).ShouldBe(-5);
    }

    // DET-05: In Outgoing, a modifier's context has `Phase = Outgoing`, `OwnerId = SourceId = Hero`, `TargetId` = the result's target Id, `InstrumentId` = the weapon's Id, and `Distance` set for secondaries.
    [Fact]
    public void OutgoingContext_Should_DescribeEachTargetAndAction()
    {
        var g = new Game();
        var spy = new SpyModifier();
        g.World.Modifiers.Add(spy);
        var sword = g.Own("Longsword", MainHand);
        sword.ModifierData.Add(Mod("spy", Physical, 1));
        g.Out(sword, g.Orc, g.Goblin1);
        spy.Calls.ShouldNotBeEmpty();
        spy.Calls.ShouldAllBe(x => x.Phase == Phase.Outgoing && x.Trigger == Trigger.Use && x.OwnerId == g.Hero.Id && x.SourceId == g.Hero.Id && x.InstrumentId == sword.Id);
        spy.Calls.ShouldContain(x => x.TargetId == g.Orc.Id);
        // Splash context must use the primary-to-secondary distance.
        var splash = new SpyModifier
        {
            Id = "splash",
            Order = 400
        };
        g.World.Modifiers.RemoveAll(x => x.Id == "splash");
        g.World.Modifiers.Add(splash);
        g.Own("Cleave1");
        g.Out(sword, g.Orc, g.Goblin1);
        splash.Calls.ShouldContain(x => x.TargetId == g.Goblin1.Id && x.Distance == 2);
    }

    // DET-06: In Incoming, a modifier's context has `Phase = Incoming`, `OwnerId = TargetId = Orc`, `SourceId = result.SourceId` (Hero), `InstrumentId = result.InstrumentId` (Hero's weapon), and `Relation` = Hero relative to the Orc (D8). Required origin data is resolved through ComponentRepository, including retained Deleted records for pending effects (D39).
    [Fact]
    public void IncomingContext_Should_DescribeDefenderAndOriginalAction()
    {
        var g = new Game();
        var spy = new SpyModifier();
        g.World.Modifiers.Add(spy);
        var ward = g.Own("WardRing", RingLeft, g.Orc);
        ward.ModifierData = new()
        {
            Mod("spy", Fire, 1, scope: Scope.Passive, phase: Phase.Incoming)
        };
        var bomb = g.Make("FireBomb");
        bomb.State = ComponentState.Deleted;
        g.In(g.Hit(Fire, -1, instrument: bomb));
        var context = spy.Calls.Single();
        context.Phase.ShouldBe(Phase.Incoming);
        context.OwnerId.ShouldBe(g.Orc.Id);
        context.TargetId.ShouldBe(g.Orc.Id);
        context.SourceId.ShouldBe(g.Hero.Id);
        context.InstrumentId.ShouldBe(bomb.Id);
        context.Relation.ShouldBe(RelationKind.Enemies);
    }

    // DET-07: Given an Incoming spy modifier on the target and an ordinary Outgoing spy modifier on the source, when Tick processes active stored results, then the Incoming spy runs once per stored result per step with `Phase = Incoming` and `Trigger = Tick`. The source's ordinary Outgoing spy is not called, and StatsRecalc is not run. Steal is resolved after Incoming as specified by D14 (D16).
    [Fact]
    public void TickContext_Should_MarkIncomingInvocationsAsTicks()
    {
        var g = new Game();
        var incoming = new SpyModifier();
        var outgoing = new SpyModifier
        {
            Id = "outgoing-spy"
        };
        var recalc = new Calculator();
        g.World.Modifiers.AddRange(new[] { incoming, outgoing });
        g.World.Calculators.Add(recalc);
        var ward = g.Own("WardRing", RingLeft, g.Orc);
        ward.ModifierData = new()
        {
            Mod("spy", Poison, 1, scope: Scope.Passive, phase: Phase.Incoming)
        };
        var ring = g.Own("RingOfFire", RingLeft);
        ring.ModifierData = new()
        {
            Mod("outgoing-spy", Poison, 1, scope: Scope.Passive)
        };
        g.Store(g.Hit(Poison, -2, 3));
        g.Store(g.Hit(Poison, -2, 3));
        g.Tick(elapsed: 2);
        incoming.Calls.Count.ShouldBe(4);
        incoming.Calls.ShouldAllBe(x => x.Phase == Phase.Incoming && x.Trigger == Trigger.Tick);
        outgoing.Calls.ShouldBeEmpty();
        recalc.Calls.ShouldBeEmpty();
    }

    // RSL-06: Given wrong-target use, no matching Use modifiers, or a Charges item at 0 charges (separate cases), when Outgoing is called with otherwise valid inputs, then it returns a successful `Result<List<EffectResult>>` with non-null Data and empty EffectResults according to A17. Passives are skipped, state is unchanged, and the caller does not call Consume (D30, D37).
    [Fact]
    public void WrongTargetUse_Should_ReturnNonNullEmptyResults()
    {
        foreach (var reason in new[]
        {
            "target",
            "modifiers",
            "charges"
        }

        )
        {
            var g = new Game();
            var item = g.Own(reason == "charges" ? "FireWand" : "HealingPotion", reason == "charges" ? MainHand : 0);
            g.Own("RingOfFire", RingLeft);
            if (reason == "modifiers")
                item.ModifierData.Clear();
            if (reason == "charges")
                item.Stats[Charges].Current = 0;
            var before = g.Snapshot();
            var results = g.Out(item, g.Orc, g.Goblin1);
            results.Count.ShouldBe(2);
            results.ShouldAllBe(x => x.Items.Count == 0);
            Ok(Pipeline.Run(g.Effects, g.Hero.Id, item.Id, g.Orc.Id, g.Goblin1.Id));
            g.Snapshot().ShouldBe(before);
        }
    }

    // RSL-07: Given Apply receives a valid empty EffectResult, Recalculate is called on the unmodified Longsword with no calculators, or Consume is called on the Eternal Longsword (separate cases), then the operation returns a successful Result and makes no state changes (APP-08, REC-15, A27).
    [Fact]
    public void SuccessfulNoOps_Should_PreserveState()
    {
        var g = new Game();
        var sword = g.Own("Longsword", MainHand);
        var hit = g.Hit();
        hit.Items.Clear();
        var before = g.Snapshot();
        Ok(g.Effects.Apply(g.Orc.Id, hit));
        Ok(g.Effects.Recalculate(sword.Id));
        Ok(g.Effects.Consume(sword.Id));
        g.Snapshot().ShouldBe(before);
    }

    // RSL-10: Given Outgoing returns a failed Result, when the caller follows the full pipeline, then its `.Failed(...)` guard returns early with a failed outcome. Incoming, Apply and Consume are not called, and the failed Result is not treated as a successful empty effect payload (§1.4).
    [Fact]
    public void ActionPipeline_Should_StopAtFailedGuards()
    {
        foreach (var failAt in new[]
        {
            "none",
            "check",
            "outgoing",
            "incoming",
            "apply",
            "zero",
            "empty"
        }

        )
        {
            var service = Substitute.For<IEffectService>();
            var calls = new List<string>();
            var target = new EffectResult
            {
                TargetId = 3,
                SourceId = 1,
                InstrumentId = 2,
                Items = new()
                {
                    new()
                    {
                        EffectTypeId = Physical,
                        Amount = -1
                    }
                }
            };
            var secondary = new EffectResult
            {
                TargetId = 4,
                SourceId = 1,
                InstrumentId = 2,
                Items = new()
                {
                    new()
                    {
                        EffectTypeId = Poison,
                        Amount = -1,
                        Duration = 2
                    }
                }
            };
            var steal = new EffectResult
            {
                TargetId = 1,
                SourceId = 1,
                InstrumentId = 2,
                Items = new()
                {
                    new()
                    {
                        EffectTypeId = Physical,
                        Amount = .5
                    }
                }
            };
            service.CanConsume(2).Returns(_ =>
            {
                calls.Add("check");
                return failAt == "check" ? Result.Fail("check", 1) : Result.Ok(failAt != "zero");
            });
            if (failAt == "empty")
            {
                target.Items.Clear();
                secondary.Items.Clear();
            }

            service.Outgoing(1, 2, 3, Arg.Any<int[]>()).Returns(_ =>
            {
                calls.Add("outgoing");
                return failAt == "outgoing" ? Result.Fail("outgoing", 1) : Result.Ok(new List<EffectResult> { target, secondary });
            });
            service.Incoming(Arg.Any<int>(), Arg.Any<EffectResult>()).Returns(call =>
            {
                calls.Add("incoming");
                if (failAt == "incoming")
                    return Result.Fail("incoming", 1);
                return Result.Ok(call.ArgAt<int>(0) == 3 ? new List<EffectResult> { target, steal } : new List<EffectResult> { secondary });
            });
            service.Apply(Arg.Any<int>(), Arg.Any<EffectResult>()).Returns(_ =>
            {
                calls.Add("apply");
                return failAt == "apply" ? Result.Fail("apply", 1) : Result.Ok();
            });
            service.Consume(2).Returns(_ =>
            {
                calls.Add("consume");
                return Result.Ok();
            });
            var result = Pipeline.Run(service, 1, 2, 3, 4);
            if (failAt == "none")
            {
                Ok(result);
                calls.ShouldBe(new[] { "check", "outgoing", "incoming", "apply", "apply", "incoming", "apply", "consume" });
                service.Received(1).Apply(1, steal);
            }
            else if (failAt == "zero")
            {
                Ok(result).ShouldBeEmpty();
                calls.ShouldBe(new[] { "check" });
            }
            else if (failAt == "empty")
            {
                Ok(result);
                service.DidNotReceive().Consume(Arg.Any<int>());
            }
            else
            {
                result.Failed(out var failure).ShouldBeTrue();
                calls.Last().ShouldBe(failAt);
                service.DidNotReceive().Consume(Arg.Any<int>());
                if (failAt == "check")
                    service.DidNotReceive().Outgoing(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int[]>());
                if (failAt is "check" or "outgoing")
                    service.DidNotReceive().Incoming(Arg.Any<int>(), Arg.Any<EffectResult>());
                if (failAt is "check" or "outgoing" or "incoming")
                    service.DidNotReceive().Apply(Arg.Any<int>(), Arg.Any<EffectResult>());
            }
        }
    }

    // RSL-12: Given CanConsume returns a failed Result, when the caller follows the full pipeline, then its `.Failed(...)` guard returns early. Outgoing, Incoming, Apply and Consume are not called, and the failure is not confused with the successful false payload of a zero-charge check (D29, §1.4).
    [Fact]
    public void ActionPipeline_Should_StopBeforeLaterFailedStages()
    {
        foreach (var failAt in new[]
        {
            "none",
            "check",
            "outgoing",
            "incoming",
            "apply",
            "zero",
            "empty"
        }

        )
        {
            var service = Substitute.For<IEffectService>();
            var calls = new List<string>();
            var target = new EffectResult
            {
                TargetId = 3,
                SourceId = 1,
                InstrumentId = 2,
                Items = new()
                {
                    new()
                    {
                        EffectTypeId = Physical,
                        Amount = -1
                    }
                }
            };
            var secondary = new EffectResult
            {
                TargetId = 4,
                SourceId = 1,
                InstrumentId = 2,
                Items = new()
                {
                    new()
                    {
                        EffectTypeId = Poison,
                        Amount = -1,
                        Duration = 2
                    }
                }
            };
            var steal = new EffectResult
            {
                TargetId = 1,
                SourceId = 1,
                InstrumentId = 2,
                Items = new()
                {
                    new()
                    {
                        EffectTypeId = Physical,
                        Amount = .5
                    }
                }
            };
            service.CanConsume(2).Returns(_ =>
            {
                calls.Add("check");
                return failAt == "check" ? Result.Fail("check", 1) : Result.Ok(failAt != "zero");
            });
            if (failAt == "empty")
            {
                target.Items.Clear();
                secondary.Items.Clear();
            }

            service.Outgoing(1, 2, 3, Arg.Any<int[]>()).Returns(_ =>
            {
                calls.Add("outgoing");
                return failAt == "outgoing" ? Result.Fail("outgoing", 1) : Result.Ok(new List<EffectResult> { target, secondary });
            });
            service.Incoming(Arg.Any<int>(), Arg.Any<EffectResult>()).Returns(call =>
            {
                calls.Add("incoming");
                if (failAt == "incoming")
                    return Result.Fail("incoming", 1);
                return Result.Ok(call.ArgAt<int>(0) == 3 ? new List<EffectResult> { target, steal } : new List<EffectResult> { secondary });
            });
            service.Apply(Arg.Any<int>(), Arg.Any<EffectResult>()).Returns(_ =>
            {
                calls.Add("apply");
                return failAt == "apply" ? Result.Fail("apply", 1) : Result.Ok();
            });
            service.Consume(2).Returns(_ =>
            {
                calls.Add("consume");
                return Result.Ok();
            });
            var result = Pipeline.Run(service, 1, 2, 3, 4);
            if (failAt == "none")
            {
                Ok(result);
                calls.ShouldBe(new[] { "check", "outgoing", "incoming", "apply", "apply", "incoming", "apply", "consume" });
                service.Received(1).Apply(1, steal);
            }
            else if (failAt == "zero")
            {
                Ok(result).ShouldBeEmpty();
                calls.ShouldBe(new[] { "check" });
            }
            else if (failAt == "empty")
            {
                Ok(result);
                service.DidNotReceive().Consume(Arg.Any<int>());
            }
            else
            {
                result.Failed(out var failure).ShouldBeTrue();
                calls.Last().ShouldBe(failAt);
                service.DidNotReceive().Consume(Arg.Any<int>());
                if (failAt == "check")
                    service.DidNotReceive().Outgoing(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int[]>());
                if (failAt is "check" or "outgoing")
                    service.DidNotReceive().Incoming(Arg.Any<int>(), Arg.Any<EffectResult>());
                if (failAt is "check" or "outgoing" or "incoming")
                    service.DidNotReceive().Apply(Arg.Any<int>(), Arg.Any<EffectResult>());
            }
        }
    }

    // RSL-13: Given CanConsume and Outgoing succeed but an Incoming or Apply call for the action fails, when the caller handles the failed Result, then it returns early without calling Consume. The instrument's charges, State and relations are unchanged by payment; earlier separately completed operations follow §1.4 and are not implicitly rolled back (D29).
    [Fact]
    public void ActionPipeline_Should_NotConsumeAfterFailure()
    {
        foreach (var failAt in new[]
        {
            "none",
            "check",
            "outgoing",
            "incoming",
            "apply",
            "zero",
            "empty"
        }

        )
        {
            var service = Substitute.For<IEffectService>();
            var calls = new List<string>();
            var target = new EffectResult
            {
                TargetId = 3,
                SourceId = 1,
                InstrumentId = 2,
                Items = new()
                {
                    new()
                    {
                        EffectTypeId = Physical,
                        Amount = -1
                    }
                }
            };
            var secondary = new EffectResult
            {
                TargetId = 4,
                SourceId = 1,
                InstrumentId = 2,
                Items = new()
                {
                    new()
                    {
                        EffectTypeId = Poison,
                        Amount = -1,
                        Duration = 2
                    }
                }
            };
            var steal = new EffectResult
            {
                TargetId = 1,
                SourceId = 1,
                InstrumentId = 2,
                Items = new()
                {
                    new()
                    {
                        EffectTypeId = Physical,
                        Amount = .5
                    }
                }
            };
            service.CanConsume(2).Returns(_ =>
            {
                calls.Add("check");
                return failAt == "check" ? Result.Fail("check", 1) : Result.Ok(failAt != "zero");
            });
            if (failAt == "empty")
            {
                target.Items.Clear();
                secondary.Items.Clear();
            }

            service.Outgoing(1, 2, 3, Arg.Any<int[]>()).Returns(_ =>
            {
                calls.Add("outgoing");
                return failAt == "outgoing" ? Result.Fail("outgoing", 1) : Result.Ok(new List<EffectResult> { target, secondary });
            });
            service.Incoming(Arg.Any<int>(), Arg.Any<EffectResult>()).Returns(call =>
            {
                calls.Add("incoming");
                if (failAt == "incoming")
                    return Result.Fail("incoming", 1);
                return Result.Ok(call.ArgAt<int>(0) == 3 ? new List<EffectResult> { target, steal } : new List<EffectResult> { secondary });
            });
            service.Apply(Arg.Any<int>(), Arg.Any<EffectResult>()).Returns(_ =>
            {
                calls.Add("apply");
                return failAt == "apply" ? Result.Fail("apply", 1) : Result.Ok();
            });
            service.Consume(2).Returns(_ =>
            {
                calls.Add("consume");
                return Result.Ok();
            });
            var result = Pipeline.Run(service, 1, 2, 3, 4);
            if (failAt == "none")
            {
                Ok(result);
                calls.ShouldBe(new[] { "check", "outgoing", "incoming", "apply", "apply", "incoming", "apply", "consume" });
                service.Received(1).Apply(1, steal);
            }
            else if (failAt == "zero")
            {
                Ok(result).ShouldBeEmpty();
                calls.ShouldBe(new[] { "check" });
            }
            else if (failAt == "empty")
            {
                Ok(result);
                service.DidNotReceive().Consume(Arg.Any<int>());
            }
            else
            {
                result.Failed(out var failure).ShouldBeTrue();
                calls.Last().ShouldBe(failAt);
                service.DidNotReceive().Consume(Arg.Any<int>());
                if (failAt == "check")
                    service.DidNotReceive().Outgoing(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int[]>());
                if (failAt is "check" or "outgoing")
                    service.DidNotReceive().Incoming(Arg.Any<int>(), Arg.Any<EffectResult>());
                if (failAt is "check" or "outgoing" or "incoming")
                    service.DidNotReceive().Apply(Arg.Any<int>(), Arg.Any<EffectResult>());
            }
        }
    }

    // USE-42: Given an action with primary, secondary and Steal results, when the caller executes the full pipeline with call-order spies, then CanConsume precedes Outgoing; all Incoming and Apply calls, including Steal and scheduling all timed items, precede the single Consume call. The instrument's State, charges and gameplay relations are unchanged until Consume (D29).
    [Fact]
    public void ActionPipeline_Should_CheckEligibilityBeforeCalculation()
    {
        foreach (var failAt in new[]
        {
            "none",
            "check",
            "outgoing",
            "incoming",
            "apply",
            "zero",
            "empty"
        }

        )
        {
            var service = Substitute.For<IEffectService>();
            var calls = new List<string>();
            var target = new EffectResult
            {
                TargetId = 3,
                SourceId = 1,
                InstrumentId = 2,
                Items = new()
                {
                    new()
                    {
                        EffectTypeId = Physical,
                        Amount = -1
                    }
                }
            };
            var secondary = new EffectResult
            {
                TargetId = 4,
                SourceId = 1,
                InstrumentId = 2,
                Items = new()
                {
                    new()
                    {
                        EffectTypeId = Poison,
                        Amount = -1,
                        Duration = 2
                    }
                }
            };
            var steal = new EffectResult
            {
                TargetId = 1,
                SourceId = 1,
                InstrumentId = 2,
                Items = new()
                {
                    new()
                    {
                        EffectTypeId = Physical,
                        Amount = .5
                    }
                }
            };
            service.CanConsume(2).Returns(_ =>
            {
                calls.Add("check");
                return failAt == "check" ? Result.Fail("check", 1) : Result.Ok(failAt != "zero");
            });
            if (failAt == "empty")
            {
                target.Items.Clear();
                secondary.Items.Clear();
            }

            service.Outgoing(1, 2, 3, Arg.Any<int[]>()).Returns(_ =>
            {
                calls.Add("outgoing");
                return failAt == "outgoing" ? Result.Fail("outgoing", 1) : Result.Ok(new List<EffectResult> { target, secondary });
            });
            service.Incoming(Arg.Any<int>(), Arg.Any<EffectResult>()).Returns(call =>
            {
                calls.Add("incoming");
                if (failAt == "incoming")
                    return Result.Fail("incoming", 1);
                return Result.Ok(call.ArgAt<int>(0) == 3 ? new List<EffectResult> { target, steal } : new List<EffectResult> { secondary });
            });
            service.Apply(Arg.Any<int>(), Arg.Any<EffectResult>()).Returns(_ =>
            {
                calls.Add("apply");
                return failAt == "apply" ? Result.Fail("apply", 1) : Result.Ok();
            });
            service.Consume(2).Returns(_ =>
            {
                calls.Add("consume");
                return Result.Ok();
            });
            var result = Pipeline.Run(service, 1, 2, 3, 4);
            if (failAt == "none")
            {
                Ok(result);
                calls.ShouldBe(new[] { "check", "outgoing", "incoming", "apply", "apply", "incoming", "apply", "consume" });
                service.Received(1).Apply(1, steal);
            }
            else if (failAt == "zero")
            {
                Ok(result).ShouldBeEmpty();
                calls.ShouldBe(new[] { "check" });
            }
            else if (failAt == "empty")
            {
                Ok(result);
                service.DidNotReceive().Consume(Arg.Any<int>());
            }
            else
            {
                result.Failed(out var failure).ShouldBeTrue();
                calls.Last().ShouldBe(failAt);
                service.DidNotReceive().Consume(Arg.Any<int>());
                if (failAt == "check")
                    service.DidNotReceive().Outgoing(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int[]>());
                if (failAt is "check" or "outgoing")
                    service.DidNotReceive().Incoming(Arg.Any<int>(), Arg.Any<EffectResult>());
                if (failAt is "check" or "outgoing" or "incoming")
                    service.DidNotReceive().Apply(Arg.Any<int>(), Arg.Any<EffectResult>());
            }
        }
    }

    // RSL-11: Given the Incoming call for Tick's first pending application returns a failed Result, when Tick handles that failure, then it returns a failed Result without applying target or Steal results and without counting down durations or Counter stats. Stored result contents and component state are unchanged, and the failure is not replaced by a successful empty Data list (A5, §1.4).
    [Fact]
    public void FailedTick_Should_NotApplyEffectsOrAdvanceTime()
    {
        var g = new Game();
        g.Orc.Stats[Stun].Current = 2;
        g.Store(g.Hit(Poison, -2, 3));
        var service = new FailingIncoming(g.World);
        var before = g.Snapshot();
        Fail(service.Tick(g.Orc.Id, 1));
        service.Calls.ShouldBe(1);
        g.Snapshot().ShouldBe(before);
    }
}

internal sealed class FailingIncoming(World world) : DefaultEffectService(world)
{
    public int Calls { get; private set; }

    public override Result<List<EffectResult>> Incoming(int targetId, EffectResult result)
    {
        Calls++;
        return Result.Fail("Incoming failed", 1);
    }
}
