using System.Text.Json;
using NSubstitute;

namespace Dexterity.Tests;

internal sealed class Game
{
    public const int HP = 1, Mana = 2, Str = 3, Stun = 4, Level = 5, Charges = 6;
    public const int Physical = 1, Fire = 2, Poison = 3, Heal = 4, StunEffect = 5,
        StrBonus = 6, HpBonus = 7, ManaBonus = 8, Recharge = 9;
    public const int Character = 1, Item = 2, Skill = 3;
    public const int Weapon = 1, Melee = 2, Sword = 3, Axe = 4, Unarmed = 5,
        Ranged = 6, Bow = 7, Thrown = 8, Wand = 9, Jewelry = 10, Ring = 11,
        Amulet = 12, Armour = 13, Consumable = 14, Potion = 15, Grenade = 16,
        Scroll = 17, Gem = 18, SkillRoot = 19, Combat = 20, Spell = 21;
    public const int MainHand = 1, OffHand = 2, RingLeft = 3, RingRight = 4,
        Neck = 5, Body = 6, Belt = 7, Accessory = 8;

    public World World { get; } = new();
    public IComponentRepository Repo => World.Repository;
    public IEffectService Effects { get; set; }
    public IRelationService Relations { get; }
    public IWorldBuilder Builder { get; }
    public ITagService Tags { get; }
    public IRandomSource Random { get; } = Substitute.For<IRandomSource>();
    public IWorldLogic Logic { get; } = Substitute.For<IWorldLogic>();
    public Component Hero { get; }
    public Component Ally { get; }
    public Component Orc { get; }
    public Component Goblin1 { get; }
    public Component Goblin2 { get; }
    public Component Goblin3 { get; }
    private int _next = 1;

    public Game()
    {
        // definitions
        World.ChargesStatId = Charges;
        World.Random = Random;
        World.Logic = Logic;
        World.Repository = new ComponentRepository(World);
        World.Stats = new()
        {
            new() { Id = HP, Name = "HP", Type = StatKind.Pool },
            new() { Id = Mana, Name = "MANA", Type = StatKind.Pool },
            new() { Id = Str, Name = "STR", Type = StatKind.Fixed },
            new() { Id = Stun, Name = "STUN", Type = StatKind.Counter },
            new() { Id = Level, Name = "LEVEL", Type = StatKind.Fixed },
            new() { Id = Charges, Name = "CHARGES", Type = StatKind.Pool }
        };
        var names = new[] { "Physical", "Fire", "Poison", "Heal", "Stun", "StrBonus", "HpBonus", "ManaBonus", "Recharge" };
        var stats = new[] { HP, HP, HP, HP, Stun, Str, HP, Mana, Charges };
        World.EffectTypes = names.Select((name, i) => new EffectType { Id = i + 1, Name = name, StatTypeId = stats[i] }).ToList();
        World.ComponentTypes = new()
        {
            new() { Id = Character, Name = "Character", Tags = new() { "actor" }, StatIds = new() { HP, Mana, Str, Stun } },
            new() { Id = Item, Name = "Item", Tags = new() { "item" }, StatIds = new() { Charges } },
            new() { Id = Skill, Name = "Skill", Tags = new() { "skill" }, StatIds = new() { Level }, MultipleParentComponents = true }
        };
        Category(Weapon, "Weapon", null, true, Usable.Eternal);
        Category(Melee, "Melee", Weapon, true, Usable.Eternal);
        Category(Sword, "Sword", Melee, true, Usable.Eternal);
        Category(Axe, "Axe", Melee, true, Usable.Eternal);
        Category(Unarmed, "Unarmed", Melee, true, Usable.Eternal);
        Category(Ranged, "Ranged", Weapon, true, Usable.Eternal);
        Category(Bow, "Bow", Ranged, true, Usable.Eternal);
        Category(Thrown, "Thrown", Ranged, true, Usable.Consumed);
        Category(Wand, "Wand", Weapon, true, Usable.Charges);
        Category(Jewelry, "Jewelry"); Category(Ring, "Ring", Jewelry, true);
        Category(Amulet, "Amulet", Jewelry, true); Category(Armour, "Armour", null, true);
        Category(Consumable, "Consumable", null, false, Usable.Consumed);
        Category(Potion, "Potion", Consumable, false, Usable.Consumed);
        Category(Grenade, "Grenade", Consumable, false, Usable.Consumed);
        Category(Scroll, "Scroll", Consumable, false, Usable.Charges);
        Category(Gem, "Gem"); Category(SkillRoot, "SkillRoot");
        Category(Combat, "Combat", SkillRoot); Category(Spell, "Spell", SkillRoot, false, Usable.Eternal);
        World.Slots = new()
        {
            Slot(MainHand, "MainHand", Weapon), Slot(OffHand, "OffHand", Weapon),
            Slot(RingLeft, "RingLeft", Ring), Slot(RingRight, "RingRight", Ring),
            Slot(Neck, "Neck", Amulet), Slot(Body, "Body", Armour),
            Slot(Belt, "Belt"), Slot(Accessory, "Accessory", Ring, Amulet)
        };
        World.Modifiers = new() { new Decrease(), new Increase(), new RandomDecrease(Random), new RandomIncrease(Random),
            new Multiplier(), new Critical(Random), new Splash(World), new PreventFlat(), new PreventPercent(), new Steal() };
        Random.Roll().Returns(0);
        Random.Range(Arg.Any<double>(), Arg.Any<double>()).Returns(call => (call.ArgAt<double>(0) + call.ArgAt<double>(1)) / 2);

        // actors and deterministic relationships
        Hero = Actor("Hero", 100); Ally = Actor("Ally", 100); Orc = Actor("Orc", 100);
        Goblin1 = Actor("Goblin1", 50); Goblin2 = Actor("Goblin2", 50); Goblin3 = Actor("Goblin3", 50);
        Logic.GetRelationKind(Arg.Any<int>(), Arg.Any<int>()).Returns(call =>
        {
            int first = call.ArgAt<int>(0), second = call.ArgAt<int>(1);
            if (first == second) return RelationKind.Self;
            return (first <= Ally.Id) == (second <= Ally.Id) ? RelationKind.Friends : RelationKind.Enemies;
        });
        Logic.GetDistance(Arg.Any<int>(), Arg.Any<int>()).Returns(call =>
        {
            var target = Repo.Items[call.ArgAt<int>(1)];
            return double.Parse(target.MetaData["distance"], System.Globalization.CultureInfo.InvariantCulture);
        });
        Hero.MetaData["distance"] = "1"; Ally.MetaData["distance"] = "1"; Orc.MetaData["distance"] = "0";
        Goblin1.MetaData["distance"] = "2"; Goblin2.MetaData["distance"] = "10"; Goblin3.MetaData["distance"] = "3";
        Effects = new DefaultEffectService(World);
        Relations = new RelationService(World);
        Builder = new WorldBuilder(World);
        Tags = new TagService(World);
    }

    public ComponentCategory Category(int id, string name, int? parent = null, bool equipable = false, Usable usable = Usable.No)
    {
        var category = new ComponentCategory { Id = id, Name = name, ParentCategoryId = parent, Equipable = equipable, Usable = usable, Tags = new() { name.ToLowerInvariant() } };
        World.Categories.Add(category);
        return category;
    }
    private static EquipmentSlot Slot(int id, string name, params int[] categories) => new() { Id = id, Name = name, Categories = categories.ToList() };
    public Component Seed(string name, int type, int category)
    {
        var component = new Component { Id = _next++, State = ComponentState.Active, Name = name, TypeId = type, CategoryId = category };
        foreach (var stat in World.ComponentTypes.Single(x => x.Id == type).StatIds)
            component.Stats.Add(stat, new Stat { TypeId = stat });
        Repo.Items.Add(component.Id, component);
        return component;
    }
    private Component Actor(string name, double hp)
    {
        var actor = Seed(name, Character, SkillRoot);
        Set(actor, HP, hp); Set(actor, Mana, 50); Set(actor, Str, 10); Set(actor, Stun, 10, 0);
        return actor;
    }
    public static void Set(Component component, int stat, double max, double? current = null) =>
        component.Stats[stat] = new Stat { TypeId = stat, Current = current ?? max, Max = max, ModifiedMax = max };

    public Component Make(string name)
    {
        var category = name switch
        {
            "Longsword" or "Greatsword" or "RustySword" or "VampDagger" or "PoisonDagger" or "LeechDagger" => Sword,
            "Fists" => Unarmed, "Battleaxe" => Axe, "Shortbow" => Bow, "ThrowingKnife" => Thrown,
            "FireWand" => Wand, "WhetstoneCharm" or "ArrowWard" or "VigorAmulet" => Amulet,
            "PlateArmour" or "PoisonPlateArmour" => Armour, "Ruby" or "KnifeRuby" or "WandRuby" => Gem,
            "Cleave1" or "Cleave2" or "AxeMastery" or "Precision" or "Toughness" => Combat,
            "Fireball" => Spell, "HealingPotion" or "RegenPotion" or "HolyWater" => Potion,
            "StunGrenade" or "FireBomb" => Grenade, "HealingScroll" => Scroll, _ => Ring
        };
        var component = Seed(name.StartsWith("Cleave") ? "Cleave" : name, category is Combat or Spell ? Skill : Item, category);
        ModifierData Use(string type, int effect, double value, string key = "value") => Mod(type, effect, value, key);
        ModifierData Passive(string type, int effect, double value, Phase phase = Phase.Outgoing, string key = "value") => Mod(type, effect, value, key, Scope.Passive, phase);
        ModifierData Enemy(ModifierData data) { data.RelationKind = RelationKind.Enemies; return data; }
        ModifierData Friendly(ModifierData data) { data.RelationKind = RelationKind.Self | RelationKind.Friends; return data; }
        ModifierData Timed(ModifierData data, double duration) { data.Duration = duration; return data; }
        ModifierData Tagged(ModifierData data, string tag) { data.IncludedTags.Add(tag); return data; }
        component.ModifierData = name switch
        {
            "Longsword" => new() { Use("decrease", Physical, 10) }, "Fists" => new() { Use("decrease", Physical, 2) },
            "Greatsword" => new() { Use("decrease", Physical, 14) }, "Battleaxe" => new() { Use("decrease", Physical, 12) },
            "Shortbow" => new() { Use("decrease", Physical, 8) }, "ThrowingKnife" => new() { Use("decrease", Physical, 6) },
            "RustySword" => new() { RandomMod("randomdecrease", Physical, 5, 9) },
            "VampDagger" => new() { Use("decrease", Physical, 10), Use("steal", Physical, 50, "percent"), Use("steal", Fire, 50, "percent") },
            "PoisonDagger" => new() { Use("decrease", Physical, 4), Timed(Use("decrease", Poison, 2), 3) },
            "LeechDagger" => new() { Timed(Use("decrease", Poison, 2), 3), Use("steal", Poison, 50, "percent") },
            "RingOfFire" => new() { Enemy(Passive("decrease", Fire, 4)) },
            "WardRing" => new() { Passive("preventpercent", Fire, 50, Phase.Incoming, "percent") },
            "StrengthRing" => new() { Passive("increase", StrBonus, 2, Phase.StatsRecalc) },
            "CursedRing" => new() { Passive("decrease", StrBonus, 2, Phase.StatsRecalc) },
            "VigorAmulet" or "Toughness" => new() { Passive("increase", HpBonus, 10, Phase.StatsRecalc) },
            "WhetstoneCharm" => new() { Tagged(Passive("decrease", Physical, 2), "sword") },
            "ArrowWard" => new() { Tagged(Passive("preventpercent", Physical, 50, Phase.Incoming, "percent"), "ranged") },
            "PlateArmour" => new() { Passive("preventflat", Physical, 3, Phase.Incoming, "amount") },
            "PoisonPlateArmour" => new() { Passive("preventflat", Poison, 1, Phase.Incoming, "amount") },
            "BlightRing" => new() { Passive("multiplier", Heal, 0.5, Phase.Incoming, "multiplier") },
            "Ruby" or "KnifeRuby" => new() { Passive("multiplier", Fire, 2, key: "multiplier") },
            "Cleave1" or "Cleave2" => new[] { Physical, Fire, Poison }.Select(effect =>
            {
                var data = Enemy(Tagged(Passive("splash", effect, name == "Cleave1" ? 50 : 75, key: "percent"), "melee"));
                data.Data["range"] = 3;
                return data;
            }).ToList(),
            "AxeMastery" => new() { Tagged(Passive("multiplier", Physical, 1.5, key: "multiplier"), "axe") },
            "Precision" => new[] { Physical, Fire }.Select(effect =>
            {
                var data = Passive("critical", effect, 100, key: "chance"); data.Data["multiplier"] = 2; return data;
            }).ToList(),
            "Fireball" => new() { Enemy(Use("decrease", Fire, 6)) },
            "HealingPotion" => new() { Friendly(Use("increase", Heal, 10)) },
            "RegenPotion" => new() { Timed(Friendly(Use("increase", Heal, 5)), 2) },
            "StunGrenade" => new() { Enemy(Use("increase", StunEffect, 2)) },
            "FireWand" => new() { Enemy(Use("decrease", Fire, 5)), Passive("increase", ManaBonus, 5, Phase.StatsRecalc) },
            "HealingScroll" => new() { Friendly(Use("increase", Heal, 5)) },
            "FireBomb" => new() { Enemy(Use("decrease", Fire, 3)), Timed(Enemy(Use("decrease", Fire, 1)), 2) },
            "HolyWater" => new() { Friendly(Use("increase", Heal, 5)), Enemy(Use("decrease", Fire, 5)) },
            _ => new()
        };
        if (category == Combat) Set(component, Level, name == "Cleave2" ? 2 : 1);
        if (name == "FireWand") Set(component, Charges, 3);
        if (name == "HealingScroll") Set(component, Charges, 2);
        return component;
    }
    public static ModifierData Mod(string type, int? effect, double value, string key = "value", Scope scope = Scope.Use, Phase phase = Phase.Outgoing) =>
        new() { TypeId = type, EffectTypeId = effect, Scope = scope, Phase = phase, Data = new() { [key] = value } };
    public static ModifierData RandomMod(string type, int effect, double min, double max) =>
        new() { TypeId = type, EffectTypeId = effect, Data = new() { ["min"] = min, ["max"] = max } };

    // Seed arrangements directly, so service tests reach the operation under test even while Create is a stub.
    public Component Own(string name, int slot = 0, Component? owner = null)
    {
        var component = Make(name);
        Link(owner ?? Hero, component, slot != 0 ? RelationType.Equipped : component.TypeId == Skill ? RelationType.Connected : RelationType.Inventory,
            slot == 0 ? Array.Empty<int>() : new[] { slot });
        return component;
    }
    public ComponentRelation Link(Component parent, Component child, RelationType type = RelationType.Connected, params int[] slots)
    {
        var relation = new ComponentRelation { Id = Repo.Relations.Count + 1, ParentId = parent.Id, TargetId = child.Id, Type = type, SlotIds = slots.ToList() };
        Repo.Relations.Add(relation);
        return relation;
    }
    public ComponentRelation Relation(Component child, Component? parent = null) => Repo.Relations.Single(x => x.TargetId == child.Id && (parent == null || x.ParentId == parent.Id));
    public static T Ok<T>(Result<T> call)
    {
        call.Failed(out var result).ShouldBeFalse(string.Join("; ", call.States));
        ((object?)result.Data).ShouldNotBeNull();
        return result.Data!;
    }
    public static void Ok(Result call) => call.Failed(out _).ShouldBeFalse(string.Join("; ", call.States));
    public static void Fail(IResult call)
    {
        call.Failed().ShouldBeTrue();
        call.States.ShouldContain(x => x.Type == StateType.Error && !string.IsNullOrWhiteSpace(x.Text));
        // The supplied generic conversion preserves States, but deliberately does not copy Code.
        if (call is Result) call.Code.ShouldNotBe(0);
    }
    public List<EffectResult> Out(Component instrument, Component? target = null, params Component[] secondary) =>
        Ok(Effects.Outgoing(Hero.Id, instrument.Id, (target ?? Orc).Id, secondary.Select(x => x.Id).ToArray()));
    public List<EffectResult> In(EffectResult result) => Ok(Effects.Incoming(result.TargetId!.Value, result));
    public List<EffectResult> Tick(Component? target = null, double elapsed = 1) => Ok(Effects.Tick((target ?? Orc).Id, elapsed));
    public EffectResult Hit(int effect = Physical, double amount = -10, double duration = 0, Component? target = null, Component? instrument = null) => new()
    {
        TargetId = (target ?? Orc).Id, SourceId = Hero.Id, InstrumentId = (instrument ?? Hero).Id,
        Items = new() { new() { EffectTypeId = effect, Amount = amount, Duration = duration, SourceId = Hero.Id, InstrumentId = (instrument ?? Hero).Id } }
    };
    public void Store(EffectResult result) => Repo.Items[result.TargetId!.Value].Effects.Add(result);
    public string Snapshot() => JsonSerializer.Serialize(new { Repo.Items, Repo.Relations });
    public static double Current(Component component, int stat = HP) => component.Stats[stat].Current;
    public Component Headline(string weapon = "Longsword", bool armour = true)
    {
        var instrument = Own(weapon, MainHand); Own("RingOfFire", RingLeft); Own("Cleave1");
        if (armour) Own("PlateArmour", Body, Orc);
        return instrument;
    }
}

internal static class Pipeline
{
    public static Result<List<EffectResult>> Run(IEffectService service, int source, int instrument, int target, params int[] secondary)
    {
        // check eligibility
        if (service.CanConsume(instrument).Failed(out var check)) return check.As<List<EffectResult>>();
        if (!check.Data) return Result.Ok(new List<EffectResult>());
        if (service.Outgoing(source, instrument, target, secondary).Failed(out var outgoing)) return outgoing;

        // resolve every destination before payment
        var resolved = new List<EffectResult>();
        foreach (var effect in outgoing.Data!)
        {
            if (service.Incoming(effect.TargetId!.Value, effect).Failed(out var incoming)) return incoming;
            foreach (var item in incoming.Data!)
            {
                if (service.Apply(item.TargetId!.Value, item).Failed(out var applied)) return applied;
                resolved.Add(item);
            }
        }

        // timed-only actions also pay
        if (outgoing.Data.Any(x => x.Items.Count > 0) && service.Consume(instrument).Failed(out var consumed)) return consumed;
        return Result.Ok(resolved);
    }
}

internal sealed class Calculator : IStatCalculator
{
    public int StatTypeId { get; init; } = Game.HP;
    public int ComponentTypeId { get; init; } = Game.Character;
    public int Order { get; init; }
    public Func<Component, double> Value { get; init; } = _ => 100;
    public List<int> Calls { get; } = new();
    public Result<double> Calculate(int componentId, World world)
    {
        Calls.Add(componentId);
        return Result.Ok(Value(world.Repository.Items[componentId]));
    }
}

internal sealed class SpyModifier : IModifierType
{
    public string Id { get; init; } = "spy";
    public string Name => "Spy";
    public int Order { get; init; } = 150;
    public List<EffectContext> Calls { get; } = new();
    public List<int> Holders { get; } = new();
    public Func<EffectResult, ModifierData, EffectContext, EffectResult> Run { get; init; } = (result, _, _) => result;
    public EffectResult Calculate(EffectResult result, ModifierData modifier, EffectContext context, int holderId)
    {
        Calls.Add(context);
        Holders.Add(holderId);
        return Run(result, modifier, context);
    }
}
