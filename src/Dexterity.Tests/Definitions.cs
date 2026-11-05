using static Dexterity.Tests.Game;

namespace Dexterity.Tests;

public sealed partial class AcceptanceTests
{
    // WLD-01: Given the fixture, when the world is built, then it returns a successful `Result<World>` whose Data exposes stats, effect types, component types, categories, slots, modifier types and calculators.
    [Fact]
    public void WorldBuilder_Should_ExposeAllDefinitions()
    {
        var g = new Game();
        var world = Ok(g.Builder.Build(g.World));
        world.Stats.Count.ShouldBe(6);
        world.EffectTypes.Count.ShouldBe(9);
        world.ComponentTypes.Count.ShouldBe(3);
        world.Categories.Count.ShouldBe(21);
        world.Slots.Count.ShouldBe(8);
        world.Modifiers.Count.ShouldBe(10);
        world.Calculators.ShouldBeEmpty();
    }

    // WLD-20: Given an EquipmentSlot with `Categories = [Ring, Amulet]`, when the world is built, then both category references are accepted and the world builds.
    [Fact]
    public void WorldBuilder_Should_AcceptAncestorSlotCategories()
    {
        var g = new Game();
        var world = Ok(g.Builder.Build(g.World));
        world.Stats.Count.ShouldBe(6);
        world.EffectTypes.Count.ShouldBe(9);
        world.ComponentTypes.Count.ShouldBe(3);
        world.Categories.Count.ShouldBe(21);
        world.Slots.Count.ShouldBe(8);
        world.Modifiers.Count.ShouldBe(10);
        world.Calculators.ShouldBeEmpty();
    }

    // WLD-22: Given root categories Weapon, Jewelry, Armour, Consumable, Gem and SkillRoot each have `ParentCategoryId = null`, and child categories reference their existing direct parents, when the world is built, then it builds. Roots need no parent category, and multiple independent category trees are allowed.
    [Fact]
    public void WorldBuilder_Should_AcceptRootCategories()
    {
        var g = new Game();
        var world = Ok(g.Builder.Build(g.World));
        world.Stats.Count.ShouldBe(6);
        world.EffectTypes.Count.ShouldBe(9);
        world.ComponentTypes.Count.ShouldBe(3);
        world.Categories.Count.ShouldBe(21);
        world.Slots.Count.ShouldBe(8);
        world.Modifiers.Count.ShouldBe(10);
        world.Calculators.ShouldBeEmpty();
    }

    // WLD-02: Given two stats (or effect types, component types, categories, slots, modifier types) with the same Id, when the world is built, then it fails and names the duplicate.
    [Fact]
    public void WorldBuilder_Should_RejectDuplicateDefinitionIds()
    {
        var g = new Game();
        Action<World>[] duplicates =
        {
            w => w.Stats.Add(w.Stats[0]),
            w => w.EffectTypes.Add(w.EffectTypes[0]),
            w => w.ComponentTypes.Add(w.ComponentTypes[0]),
            w => w.Categories.Add(w.Categories[0]),
            w => w.Slots.Add(w.Slots[0]),
            w => w.Modifiers.Add(w.Modifiers[0])
        };
        foreach (var duplicate in duplicates)
        {
            var game = new Game();
            duplicate(game.World);
            var result = game.Builder.Build(game.World);
            Fail(result);
            string.Join(" ", result.States).ShouldContain("duplicate");
        }
    }

    // WLD-03: Given an EffectType whose `StatTypeId` doesn't exist, then the build fails.
    [Fact]
    public void WorldBuilder_Should_RejectUnknownEffectStat()
    {
        var g = new Game();
        g.World.EffectTypes[0].StatTypeId = 999;
        Fail(g.Builder.Build(g.World));
    }

    // WLD-04: Given a category with a non-null `ParentCategoryId` that doesn't reference an existing category, then the build fails. A null parent is valid and denotes a root category (WLD-22).
    [Fact]
    public void WorldBuilder_Should_RejectUnknownParentCategory()
    {
        var g = new Game();
        g.World.Categories[0].ParentCategoryId = 999;
        Fail(g.Builder.Build(g.World));
    }

    // WLD-05: Given a category cycle (A → B → A), then the build fails.
    [Fact]
    public void WorldBuilder_Should_RejectCategoryCycles()
    {
        var g = new Game();
        g.World.Categories[0].ParentCategoryId = Sword;
        Fail(g.Builder.Build(g.World));
    }

    // WLD-06: Given an EquipmentSlot whose `Categories` contains an unknown category Id, then the build fails, even if another entry is valid. Check both `[Ring, unknown]` and `[unknown, Ring]`.
    [Fact]
    public void WorldBuilder_Should_RejectUnknownSlotCategories()
    {
        var g = new Game();
        foreach (var categories in new[]
        {
            new List<int>
            {
                Ring,
                999
            },
            new List<int>
            {
                999,
                Ring
            }
        }

        )
        {
            g.World.Slots[0].Categories = categories;
            Fail(g.Builder.Build(g.World));
        }
    }

    // WLD-07: Given a ComponentType whose `StatIds` contains an unknown stat, then the build fails.
    [Fact]
    public void WorldBuilder_Should_RejectUnknownComponentStats()
    {
        var g = new Game();
        g.World.ComponentTypes[0].StatIds.Add(999);
        Fail(g.Builder.Build(g.World));
    }

    // WLD-08: Given two StatCalculators for different stats with the same Order, or with a negative Order, then the world builds (A28).
    [Fact]
    public void WorldBuilder_Should_AcceptSharedAndNegativeCalculatorOrders()
    {
        var g = new Game();
        g.World.Calculators.AddRange(new[] { new Calculator { Order = -1 }, new Calculator { Order = -1, StatTypeId = Str } });
        Ok(g.Builder.Build(g.World));
    }

    // WLD-09: Given two StatCalculators for the same stat and the same ComponentType, then the build fails.
    [Fact]
    public void WorldBuilder_Should_RejectDuplicateStatCalculators()
    {
        var g = new Game();
        g.World.Calculators.AddRange(new[] { new Calculator(), new Calculator() });
        Fail(g.Builder.Build(g.World));
    }

    // WLD-10: Given ModifierData with an unknown `TypeId`, then attaching or validating it fails.
    [Fact]
    public void WorldBuilder_Should_RejectUnknownModifierTypes()
    {
        var g = new Game();
        Fail(g.Builder.Validate(Mod("unknown", Physical, 1)));
    }

    // WLD-11: Given ModifierData with a set but unknown `EffectTypeId`, then it fails.
    [Fact]
    public void WorldBuilder_Should_RejectUnknownModifierEffectTypes()
    {
        var g = new Game();
        Fail(g.Builder.Validate(Mod("decrease", 999, 1)));
    }

    // WLD-12: Given ModifierData without a required Data key (Splash without `percent` or `range`, RandomDecrease without `max`, Critical without `chance`, Decrease without `value`), then it fails.
    [Fact]
    public void WorldBuilder_Should_RejectMissingModifierDataKeys()
    {
        var g = new Game();
        foreach (var(type, keys)in new[]
        {
            ("splash", new[] { "percent", "range" }),
            ("randomdecrease", new[] { "min", "max" }),
            ("critical", new[] { "chance", "multiplier" }),
            ("decrease", new[] { "value" })
        }

        )
            foreach (var missing in keys)
            {
                var data = new ModifierData
                {
                    TypeId = type,
                    EffectTypeId = Physical,
                    Data = keys.ToDictionary(k => k, _ => 1d)
                };
                data.Data.Remove(missing);
                Fail(g.Builder.Validate(data));
            }
    }

    // WLD-13: Given RandomDecrease or RandomIncrease with `min > max`, then it fails.
    [Fact]
    public void WorldBuilder_Should_RejectReversedRandomBounds()
    {
        var g = new Game();
        foreach (var type in new[]
        {
            "randomdecrease",
            "randomincrease"
        }

        )
            Fail(g.Builder.Validate(RandomMod(type, Physical, 9, 5)));
    }

    // WLD-14: Given an out-of-range value (percent or chance < 0 or > 100, negative range, negative multiplier, negative value or amount, negative Duration), then it fails.
    [Fact]
    public void WorldBuilder_Should_RejectOutOfRangeModifierValues()
    {
        var g = new Game();
        foreach (var data in new[]
        {
            Mod("steal", Physical, -1, "percent"),
            Mod("steal", Physical, 101, "percent"),
            new ModifierData
            {
                TypeId = "critical",
                EffectTypeId = Physical,
                Data = new()
                {
                    ["chance"] = -1,
                    ["multiplier"] = 2
                }
            },
            new ModifierData
            {
                TypeId = "critical",
                EffectTypeId = Physical,
                Data = new()
                {
                    ["chance"] = 101,
                    ["multiplier"] = 2
                }
            },
            new ModifierData
            {
                TypeId = "splash",
                EffectTypeId = Physical,
                Data = new()
                {
                    ["range"] = -1,
                    ["percent"] = 50
                }
            },
            Mod("multiplier", Physical, -1, "multiplier"),
            Mod("decrease", Physical, -1),
            Mod("preventflat", Physical, -1, "amount"),
            new ModifierData
            {
                TypeId = "decrease",
                EffectTypeId = Physical,
                Duration = -1,
                Data = new()
                {
                    ["value"] = 1
                }
            }
        }

        )
            Fail(g.Builder.Validate(data));
    }

    // WLD-15: Given boundary values (percent 0 and 100, chance 0 and 100, multiplier 0, range 0, min == max), then they are accepted.
    [Fact]
    public void WorldBuilder_Should_AcceptModifierBoundaryValues()
    {
        var g = new Game();
        foreach (var value in new[]
        {
            0d,
            100d
        }

        )
        {
            Ok(g.Builder.Validate(Mod("steal", Physical, value, "percent")));
            var crit = Mod("critical", Physical, value, "chance");
            crit.Data["multiplier"] = 2;
            Ok(g.Builder.Validate(crit));
        }

        Ok(g.Builder.Validate(Mod("multiplier", Physical, 0, "multiplier")));
        var splash = Mod("splash", Physical, 0, "range");
        splash.Data["percent"] = 50;
        Ok(g.Builder.Validate(splash));
        Ok(g.Builder.Validate(RandomMod("randomdecrease", Physical, 6, 6)));
    }

    // WLD-16: Given a modifier with `Scope = Use` and `Phase = StatsRecalc` or `Phase = Incoming`, then it fails, because these are never collected (see COL-13).
    [Fact]
    public void WorldBuilder_Should_RejectUseModifiersOutsideOutgoing()
    {
        var g = new Game();
        foreach (var phase in new[]
        {
            Phase.Incoming,
            Phase.StatsRecalc
        }

        )
            Fail(g.Builder.Validate(Mod("decrease", Physical, 1, phase: phase)));
    }

    // WLD-17: Given a modifier (any phase) without an `EffectTypeId`, then the world builds. The modifier matches nothing (D9).
    [Fact]
    public void WorldBuilder_Should_AcceptModifiersWithoutEffectTypes()
    {
        var g = new Game();
        foreach (var phase in Enum.GetValues<Phase>())
        {
            var modifier = Mod("decrease", null, 1, scope: phase == Phase.Outgoing ? Scope.Use : Scope.Passive, phase: phase);
            var component = g.Make("Longsword");
            component.ModifierData = new()
            {
                modifier
            };
            Ok(g.Builder.Validate(modifier));
            Ok(g.Builder.Build(g.World));
        }
    }

    // WLD-18: Given a StatsRecalc modifier with IncludedTags, ExcludedTags or a RelationKind other than None, then it fails (A20).
    [Fact]
    public void WorldBuilder_Should_RejectStatsRecalcFilters()
    {
        var g = new Game();
        foreach (var filter in new[]
        {
            "include",
            "exclude",
            "relation"
        }

        )
        {
            var data = Mod("increase", StrBonus, 2, scope: Scope.Passive, phase: Phase.StatsRecalc);
            if (filter == "include")
                data.IncludedTags.Add("item");
            if (filter == "exclude")
                data.ExcludedTags.Add("item");
            if (filter == "relation")
                data.RelationKind = RelationKind.Self;
            Fail(g.Builder.Validate(data));
        }
    }

    // WLD-19: Given a StatsRecalc modifier with a type other than Increase or Decrease, then it fails.
    [Fact]
    public void WorldBuilder_Should_RejectUnsupportedStatsRecalcModifiers()
    {
        var g = new Game();
        foreach (var type in new[]
        {
            "randomdecrease",
            "randomincrease",
            "multiplier",
            "critical",
            "splash",
            "preventflat",
            "preventpercent",
            "steal"
        }

        )
        {
            var data = new ModifierData
            {
                TypeId = type,
                EffectTypeId = StrBonus,
                Scope = Scope.Passive,
                Phase = Phase.Outgoing,
                Data = new()
                {
                    ["min"] = 1,
                    ["max"] = 2,
                    ["value"] = 1,
                    ["amount"] = 1,
                    ["multiplier"] = 2,
                    ["chance"] = 50,
                    ["percent"] = 50,
                    ["range"] = 3
                }
            };
            Ok(g.Builder.Validate(data));
            data.Phase = Phase.StatsRecalc;
            Fail(g.Builder.Validate(data));
        }
    }

    // WLD-21: Given an EquipmentSlot with `Categories = null` or `[]` (separate cases), when the world is built, then it builds with no category restriction on that slot.
    [Fact]
    public void WorldBuilder_Should_AcceptUnrestrictedEquipmentSlots()
    {
        var g = new Game();
        foreach (var categories in new List<int>? []
        {
            null,
            new()
        }

        )
        {
            g.World.Slots.Single(x => x.Id == Belt).Categories = categories;
            Ok(g.Builder.Build(g.World));
        }
    }

    // CMP-01: When `Create(Item, Sword)` is called, then a successful `Result<Component>` is returned whose Data is a component with a new integer Id, `State = Active`, `TypeId = Item`, `CategoryId = Sword`, empty component MetaData, Tags and ModifierData, and an empty `Effects: List<EffectResult>`.
    [Fact]
    public void ComponentRepository_Should_CreateActiveComponentsWithEmptyCollections()
    {
        var g = new Game();
        var item = Ok(g.Repo.Create(Item, Sword));
        item.Id.ShouldBeGreaterThan(0);
        item.State.ShouldBe(ComponentState.Active);
        item.TypeId.ShouldBe(Item);
        item.CategoryId.ShouldBe(Sword);
        item.MetaData.ShouldBeEmpty();
        item.Tags.ShouldBeEmpty();
        item.ModifierData.ShouldBeEmpty();
        item.Effects.ShouldBeEmpty();
    }

    // CMP-02: When 1 000 components are created, then all Ids are unique.
    [Fact]
    public void ComponentRepository_Should_AssignUniqueIds()
    {
        var g = new Game();
        Enumerable.Range(0, 1000).Select(_ => Ok(g.Repo.Create(Item, Sword)).Id).Distinct().Count().ShouldBe(1000);
    }

    // CMP-03: When a Character is created, then it has a Stat entry for every stat in `Character.StatIds` (HP, MANA, STR, STUN) and none for other stats.
    [Fact]
    public void Character_Should_ReceiveOnlyItsDefinedStats()
    {
        var g = new Game();
        Ok(g.Repo.Create(Character, SkillRoot)).Stats.Keys.OrderBy(x => x).ShouldBe(new[] { HP, Mana, Str, Stun });
    }

    // CMP-04: When `Create` is called with an unknown type or category, then it fails.
    [Fact]
    public void ComponentRepository_Should_RejectUnknownTypesAndCategories()
    {
        var g = new Game();
        foreach (var pair in new[]
        {
            (999, Sword),
            (Item, 999)
        }

        )
        {
            var before = g.Snapshot();
            Fail(g.Repo.Create(pair.Item1, pair.Item2));
            g.Snapshot().ShouldBe(before);
        }
    }

    // CMP-05: When a component is fetched by Id, then a successful `Result<Component>` is returned whose Data is the same component instance.
    [Fact]
    public void ComponentRepository_Should_ReturnTheOriginalComponentInstance()
    {
        var g = new Game();
        Ok(g.Repo.Get(g.Hero.Id)).ShouldBeSameAs(g.Hero);
    }

    // CMP-06: When a component is fetched by an unknown Id, then a failed `Result<Component>` is returned with an error identifying the missing component; no exception is thrown and no successful component payload is returned.
    [Fact]
    public void ComponentRepository_Should_DescribeMissingComponents()
    {
        var g = new Game();
        var result = g.Repo.Get(999);
        Fail(result);
        result.Data.ShouldBeNull();
    }

    // CMP-07: When a stat that is not in the component type's `StatIds` is set (LEVEL on a Character), then it fails.
    [Fact]
    public void ComponentRepository_Should_RejectStatsOutsideTheComponentType()
    {
        var g = new Game();
        var before = g.Snapshot();
        Fail(g.Repo.SetStat(g.Hero.Id, new Stat { TypeId = Level }));
        g.Snapshot().ShouldBe(before);
    }

    // CMP-08: Given Cleave1 is Connected to both Hero and Ally, then ComponentRepository contains one Cleave1 record. Both ComponentRelations contain the same integer TargetId, with their respective integer ParentIds, and neither actor nor relation embeds another Component (D38).
    [Fact]
    public void SharedSkills_Should_HaveOneRepositoryRecord()
    {
        var g = new Game();
        var skill = g.Own("Cleave1");
        Ok(g.Relations.Connect(g.Ally.Id, skill.Id));
        g.Repo.Items.Values.Count(x => x.Id == skill.Id).ShouldBe(1);
        g.Repo.GetParentRelations(skill.Id).Select(x => x.ParentId).ShouldBe(new[] { g.Hero.Id, g.Ally.Id });
        g.Repo.GetParents(skill.Id).ShouldBe(new[] { g.Hero, g.Ally });
        g.Repo.GetChildren(g.Hero.Id).ShouldContain(skill);
    }

    // CMP-09: Given a World containing shared Cleave1, FireBomb and pending FireBomb effects on two actors, when representing that model for serialization, then each Component record occurs once in ComponentRepository. Relations, EffectResults, EffectResultItems and EffectContexts contain only scalar component Ids, including for shared and cyclic origin references; no component is duplicated under another component, result or context (D38).
    [Fact]
    public void Serialization_Should_RepresentEachComponentOnce()
    {
        var g = new Game();
        var skill = g.Own("Cleave1");
        g.Link(g.Ally, skill);
        var bomb = g.Own("FireBomb");
        g.Store(g.Hit(Fire, -1, 2, instrument: bomb));
        g.Store(g.Hit(Fire, -1, 2, g.Ally, bomb));
        foreach (var type in new[]
        {
            typeof(Component),
            typeof(ComponentRelation),
            typeof(EffectResult),
            typeof(EffectResultItem),
            typeof(EffectContext)
        }

        )
            type.GetProperties().ShouldNotContain(p => p.PropertyType == typeof(Component) || p.PropertyType == typeof(List<Component>));
        var json = System.Text.Json.JsonSerializer.Serialize(g.World);
        using var document = System.Text.Json.JsonDocument.Parse(json);
        document.RootElement.GetProperty("Repository").GetProperty("Items").EnumerateObject().Count().ShouldBe(g.Repo.Items.Count);
    }

    // CMP-10: Given FireBomb is retained with State Deleted because the Orc has a pending burn, when `Get(FireBomb)` is called, then it returns a successful `Result<Component>` with that same Id and State Deleted and preserved Tags, Data and ModifierData. Resolving a pending origin does not reactivate it (D39).
    [Fact]
    public void DeletedComponents_Should_RetainTheirIdentityAndOriginData()
    {
        var g = new Game();
        var bomb = g.Make("FireBomb");
        bomb.State = ComponentState.Deleted;
        bomb.Tags.Add("origin");
        bomb.MetaData["origin"] = "kept";
        g.Store(g.Hit(Fire, -1, 2, instrument: bomb));
        var before = g.Snapshot();
        Ok(g.Repo.Get(bomb.Id)).ShouldBeSameAs(bomb);
        g.Snapshot().ShouldBe(before);
    }

    // CMP-11: Given a Deleted origin is referenced only by an EffectResultItem's InstrumentId in an unexpired stored application, not by the result's InstrumentId, when cleanup checks it, then it remains in ComponentRepository. Item-level provenance dependencies also prevent premature physical removal (D39).
    [Fact]
    public void Cleanup_Should_RetainReferencedDeletedComponents()
    {
        var g = new Game();
        var origin = g.Make("Ruby");
        origin.State = ComponentState.Deleted;
        var hit = g.Hit(Fire, -1, 2);
        hit.Items[0] = hit.Items[0] with
        {
            InstrumentId = origin.Id
        };
        g.Store(hit);
        Ok(g.Repo.Cleanup());
        g.Repo.Items.ShouldContainKey(origin.Id);
    }

    // CMP-12: Given a Deleted record has no remaining pending-effect dependencies and cleanup removes it, when a new component is created, then the new Id differs from the removed Id. Looking up the old Id fails rather than resolving to the new component (D38, D39).
    [Fact]
    public void ComponentRepository_Should_NotReuseRemovedIds()
    {
        var g = new Game();
        var old = g.Make("Longsword");
        old.State = ComponentState.Deleted;
        Ok(g.Repo.Cleanup());
        Ok(g.Repo.Create(Item, Sword)).Id.ShouldNotBe(old.Id);
        Fail(g.Repo.Get(old.Id));
    }

    // CMP-13: Given an Active component with no parent relations and no pending Effects or references, when cleanup checks the repository, then it remains Active in ComponentRepository. Lack of references alone never deletes an Active record (D39).
    [Fact]
    public void Cleanup_Should_RetainActiveComponents()
    {
        var g = new Game();
        var item = g.Make("Ruby");
        Ok(g.Repo.Cleanup());
        g.Repo.Items[item.Id].ShouldBeSameAs(item);
        item.State.ShouldBe(ComponentState.Active);
    }

    // CMP-14: Given a Deleted record still owns an unexpired stored EffectResult, when cleanup checks it, then the record and its Effects remain in ComponentRepository. Logical deletion does not discard unresolved effects (D39).
    [Fact]
    public void Cleanup_Should_RetainDeletedOwnersOfPendingEffects()
    {
        var g = new Game();
        var item = g.Make("Ruby");
        item.State = ComponentState.Deleted;
        item.Effects.Add(g.Hit(Fire, -1, 2, item));
        var before = g.Snapshot();
        Ok(g.Repo.Cleanup());
        g.Snapshot().ShouldBe(before);
    }

    // TAG-01: Given a Longsword with its own tag `silver`, when its tags are read, then they are `silver, item, sword, melee, weapon` (D3).
    [Fact]
    public void Longsword_Should_ComposeOwnTypeAndAncestorCategoryTags()
    {
        var g = new Game();
        var sword = g.Make("Longsword");
        sword.Tags.Add("silver");
        Ok(g.Tags.GetTags(sword.Id)).ShouldBe(new[] { "silver", "item", "sword", "melee", "weapon" });
    }

    // TAG-02: Given the same tag on the component and its category, then it appears once.
    [Fact]
    public void Tags_Should_NotContainDuplicates()
    {
        var g = new Game();
        var sword = g.Make("Longsword");
        sword.Tags.Add("SWORD");
        Ok(g.Tags.GetTags(sword.Id)).Count(x => x.Equals("sword", StringComparison.OrdinalIgnoreCase)).ShouldBe(1);
    }

    // TAG-03: Given a component without its own tags, then it still has its type and category tags.
    [Fact]
    public void Tags_Should_IncludeTypeAndCategoryTagsWithoutOwnTags()
    {
        var g = new Game();
        Ok(g.Tags.GetTags(g.Make("Longsword").Id)).ShouldBe(new[] { "item", "sword", "melee", "weapon" });
    }

    // TAG-04: Given a tag added to the Melee category after the sword was created, when the Longsword's tags are read, then they include the new tag (tags are composed on read).
    [Fact]
    public void Tags_Should_ReflectChangedCategoryTags()
    {
        var g = new Game();
        var sword = g.Make("Longsword");
        Ok(g.Tags.GetTags(sword.Id));
        g.World.Categories.Single(x => x.Id == Melee).Tags.Add("new");
        Ok(g.Tags.GetTags(sword.Id)).ShouldContain("new");
    }

    // TAG-06: Given PlateArmour has no own tags and its Armour category has `ParentCategoryId = null`, when its composed tags are read, then they are `item, armour`. Ancestor traversal stops at the root without an error or tags from unrelated category trees (D3).
    [Fact]
    public void PlateArmour_Should_ComposeItemAndArmourTags()
    {
        var g = new Game();
        Ok(g.Tags.GetTags(g.Make("PlateArmour").Id)).ShouldBe(new[] { "item", "armour" });
    }
}
