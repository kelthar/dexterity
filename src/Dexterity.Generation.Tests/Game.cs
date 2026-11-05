using NSubstitute;

namespace Dexterity.Generation.Tests;

internal sealed class Game
{
    public const int HP = 1, Str = 2, Durability = 3, Stun = 4, Charges = 5;
    public const int Character = 1, Item = 2, Skill = 3, Sword = 2, Wand = 4;
    public World World { get; } = new();
    public GenerationCatalog Catalog { get; } = new();
    public ScriptedRandom Random { get; } = new();
    public GenerationTemplate Template { get; }
    public IGenerationService Service => new GenerationService(World, Catalog, Random);

    public Game()
    {
        World.Stats = new()
        {
            new() { Id = HP, Type = StatKind.Pool }, new() { Id = Str, Type = StatKind.Fixed },
            new() { Id = Durability, Type = StatKind.Pool }, new() { Id = Stun, Type = StatKind.Counter },
            new() { Id = Charges, Type = StatKind.Pool }
        };
        World.ComponentTypes = new()
        {
            new() { Id = Character, Tags = new() { "actor" }, StatIds = new() { HP, Str, Stun } },
            new() { Id = Item, Tags = new() { "item" }, StatIds = new() { Durability, Charges } },
            new() { Id = Skill, StatIds = new() { Str }, MultipleParentComponents = true }
        };
        World.Categories = new()
        {
            new() { Id = 1, Name = "Character" },
            new() { Id = Sword, Name = "Sword", ParentCategoryId = 6, Equipable = true, Usable = Usable.Eternal },
            new() { Id = 3, Name = "Potion", Usable = Usable.Consumed },
            new() { Id = Wand, Name = "Wand", Equipable = true, Usable = Usable.Charges },
            new() { Id = 5, Name = "Skill" },
            new() { Id = 6, Name = "Weapon", Tags = new() { "weapon-category" } }
        };
        World.Slots.Add(new EquipmentSlot { Id = 1, Name = "Hand" });
        World.EffectTypes = new()
        {
            new() { Id = 1, Name = "Physical", StatTypeId = HP },
            new() { Id = 2, Name = "StrengthBonus", StatTypeId = Str }
        };
        World.Modifiers = new()
        {
            new Decrease(), new Increase(), new RandomDecrease(new DefaultRandomSource(1)),
            new RandomIncrease(new DefaultRandomSource(1)), new Multiplier(), new Critical(new DefaultRandomSource(1)),
            new Splash(World), new PreventFlat(), new PreventPercent(), new Steal()
        };
        World.ChargesStatId = Charges;
        World.Random = Substitute.For<IRandomSource>();
        Ok(new WorldBuilder(World).Build(World));
        Template = new GenerationTemplate { Id = "sword", Name = "Longsword", TypeId = Item, CategoryId = Sword };
        Catalog.Templates.Add(Template);
    }

    public void SwordTemplate()
    {
        Template.Tags = new() { "weapon", "melee" };
        Template.ComponentTags.Add("crafted");
        Template.Items = new()
        {
            new GenerationValue { Id = "damage", Weight = 3, WorthPerValue = 2 },
            new GenerationValue { Id = "durability", Weight = 1 },
            new GenerationValue { Id = "tier", Kind = GenerationValueKind.Fixed, Value = "2" }
        };
        Template.StatBindings.Add(new StatBinding { StatTypeId = Durability, MaxKey = "durability" });
        Template.DataBindings.Add(new DataBinding { Key = "tier", ValueKey = "tier" });
        Template.Modifiers.Add(new ModifierTemplate
        {
            Base = new ModifierData { TypeId = "decrease", EffectTypeId = 1, Scope = Scope.Use, Phase = Phase.Outgoing, RelationKind = RelationKind.Enemies },
            DataKeys = new() { ["value"] = "damage" }
        });
    }

    public GenerationRequest Request(int worth = 40, int? seed = null) => new() { TemplateId = Template.Id, Worth = worth, Seed = seed };
    public GeneratedComponent Create(int worth = 40, int? seed = null) => Ok(Service.Create(Request(worth, seed)));
    public Component Component(GeneratedComponent result)
    {
        Ok(World.Repository.Get(result.Component.Id)).ShouldBeSameAs(result.Component);
        return result.Component;
    }

    public static T Ok<T>(Result<T> result)
    {
        ((bool)result).ShouldBeTrue(string.Join("; ", result.States.Select(x => x.Text)));
        return result.Data!;
    }

    public static void Ok(Result result) => ((bool)result).ShouldBeTrue(string.Join("; ", result.States.Select(x => x.Text)));

    public static void Fail(IResult result)
    {
        result.Success.ShouldBeFalse();
        result.Code.ShouldNotBe(0);
        result.States.ShouldNotBeEmpty();
    }
}
