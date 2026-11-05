# Dexterity

Dexterity is a .NET 8 library for building data-driven RPG rules. Characters,
items and skills are all **components** in a configurable **world**. You define
their stats, categories, equipment slots and effects, then use services to manage
inventory, equipment, connected skills and combat.

The core library provides:

- Stat pools such as health and mana, fixed stats such as strength, and counters.
- Inventory, equipment and connected-component relationships.
- Active and passive modifiers, including damage, healing, mitigation, critical
  hits, splash damage and life steal.
- Outgoing/incoming effect calculation, explicit application and timed effects.
- One-call Attack/Use actions.
- JSON-serializable gameplay-state packages for save/load.
- Derived-stat calculators, tag filtering and replaceable game-world logic.
- An in-memory repository and `Result`-based service contracts.

Your game supplies the definitions and decides when actions happen, which targets
to select, and how to persist or display the results. The companion
[Dexterity.Generation](../Dexterity.Generation/README.md) library can create
components from templates and worth budgets.

## Contents

1. [Core concepts](#core-concepts)
2. [Reference the project](#reference-the-project)
3. [Configure a world](#configure-a-world)
4. [Create characters and initialize stats](#create-characters-and-initialize-stats)
5. [Give a character an item and equip it](#give-a-character-an-item-and-equip-it)
6. [Create and connect a skill](#create-and-connect-a-skill)
7. [Use a weapon or skill](#use-a-weapon-or-skill)
8. [Save and load gameplay state](#save-and-load-gameplay-state)
9. [Additional configuration](#additional-configuration)
10. [Dependency injection](#dependency-injection)
11. [Run the tests](#run-the-tests)

## Core concepts

| Concept | Purpose | Example |
| --- | --- | --- |
| `World` | Holds definitions and runtime dependencies | Stats, categories, repository and random source |
| `ComponentType` | Defines which stats a component can have and whether it can have multiple parents | Character, Item, Skill |
| `ComponentCategory` | Classifies components and defines equipability and usage | Sword, Armour, Spell |
| `Component` | A runtime instance with stats, tags, data and modifier definitions | Hero, Longsword, Firebolt |
| `StatType` / `Stat` | Defines a stat's behavior / stores its runtime values | Health definition / Hero's current health |
| `EffectType` | Maps an effect to the stat it changes | Physical damage → Health |
| `ModifierData` | Configures how an effect is produced or changed | Decrease physical health by 12 |
| `ComponentRelation` | Links components by integer IDs | Hero has Longsword equipped |

Types, categories, stats, effects and slots have separate integer ID spaces.
Modifier implementations use string IDs such as `"decrease"` and `"increase"`.
Components are stored once in the repository; parents refer to them through
relations rather than embedding copies.

## Reference the project

From your application project, add a reference to the library:

```sh
dotnet add reference /path/to/fourtwo.rpgsystem/src/Dexterity/Dexterity.csproj
```

The following numbered sections form one example. Paste their C# blocks in order
into a .NET 8 console application's `Program.cs`.

## Configure a world

### 1. Define IDs and result helpers

Use named constants so definitions and runtime operations use the same IDs.
The helpers below keep this console example readable: they inspect `.Failed(...)`,
write failures to stderr and stop the example. In an application service, handle
or propagate a failed Result instead of using these throwing helpers.

```csharp
using Dexterity;
using System.Text.Json;

const int Health = 1, Strength = 2;
const int Physical = 1, Fire = 2, StrengthBonus = 3;
const int CharacterType = 1, ItemType = 2, SkillType = 3;
const int CharacterCategory = 1, WeaponCategory = 2, SwordCategory = 3;
const int SpellCategory = 4;
const int MainHand = 1;

static T Require<T>(Result<T> call)
{
    if (call.Failed(out var result))
    {
        var message = string.Join("; ", result.States.Select(x => x.Text));
        Console.Error.WriteLine(message);
        throw new InvalidOperationException(message);
    }

    return result.Data!;
}

static void Check(Result call)
{
    if (call.Failed(out var result))
    {
        var message = string.Join("; ", result.States.Select(x => x.Text));
        Console.Error.WriteLine(message);
        throw new InvalidOperationException(message);
    }
}
```

### 2. Define stats, types, categories, slots and modifiers

This world supports health and strength on characters, equippable swords, and
shareable spell components. Physical and fire effects both change health, while
StrengthBonus changes strength.

```csharp
var world = new World
{
    Stats = new()
    {
        new() { Id = Health, Name = "Health", Type = StatKind.Pool },
        new() { Id = Strength, Name = "Strength", Type = StatKind.Fixed }
    },
    EffectTypes = new()
    {
        new() { Id = Physical, Name = "Physical", StatTypeId = Health },
        new() { Id = Fire, Name = "Fire", StatTypeId = Health },
        new() { Id = StrengthBonus, Name = "Strength bonus", StatTypeId = Strength }
    },
    ComponentTypes = new()
    {
        new()
        {
            Id = CharacterType, Name = "Character",
            StatIds = new() { Health, Strength }, Tags = new() { "character" }
        },
        new() { Id = ItemType, Name = "Item", Tags = new() { "item" } },
        new()
        {
            Id = SkillType, Name = "Skill", Tags = new() { "skill" },
            MultipleParentComponents = true
        }
    },
    Categories = new()
    {
        new() { Id = CharacterCategory, Name = "Character" },
        new() { Id = WeaponCategory, Name = "Weapon", Tags = new() { "weapon" } },
        new()
        {
            Id = SwordCategory, Name = "Sword", ParentCategoryId = WeaponCategory,
            Equipable = true, Usable = Usable.Eternal
        },
        new() { Id = SpellCategory, Name = "Spell", Usable = Usable.Eternal }
    },
    Slots = new()
    {
        new() { Id = MainHand, Name = "Main hand", Categories = new() { WeaponCategory } }
    },
    Modifiers = new() { new Decrease(), new Increase(), new PreventFlat() },
    Random = new DefaultRandomSource(123)
};

IWorldBuilder builder = new WorldBuilder(world);
Require(builder.Build(world));

IComponentRepository repository = world.Repository;
IRelationService relations = new RelationService(world);
IEffectService effects = new DefaultEffectService(world);
```

`Build` validates definition IDs and references, category ancestry, equipment-slot
categories and calculator definitions. It supplies `ComponentRepository`,
`DefaultWorldLogic` and `DefaultRandomSource` when those dependencies are unset.
It preserves dependencies you supply. Modifier implementations must be registered
explicitly in `World.Modifiers`; Build does not add them automatically.

The main-hand slot accepts the Weapon category and its descendants, including
Sword. Categories organize ancestry and usage; `ComponentType.StatIds` determines
which stats an instance may have.

## Create characters and initialize stats

### 3. Create a hero and an enemy

`Create` allocates an ID and initializes the type's declared stats to zero. Set
their initial values explicitly. `SetStat` copies the supplied stat into the
component and clamps Current to ModifiedMax.

```csharp
static void SetStat(IComponentRepository repository, int componentId,
    int statId, double maximum, double current)
{
    Check(repository.SetStat(componentId, new Stat
    {
        TypeId = statId,
        Max = maximum,
        ModifiedMax = maximum,
        Current = current
    }));
}

var hero = Require(repository.Create(CharacterType, CharacterCategory));
hero.Name = "Hero";
hero.MetaData["faction"] = "heroes";
SetStat(repository, hero.Id, Health, 100, 100);
SetStat(repository, hero.Id, Strength, 10, 10);

var orc = Require(repository.Create(CharacterType, CharacterCategory));
orc.Name = "Orc";
orc.MetaData["faction"] = "monsters";
SetStat(repository, orc.Id, Health, 80, 80);
SetStat(repository, orc.Id, Strength, 8, 8);
```

The default world logic considers matching non-empty factions friends, different
factions enemies, and a component its own Self target. It calculates distance
from optional invariant-culture `MetaData["x"]` and `MetaData["y"]` coordinates, defaulting
missing coordinates to zero. Replace `IWorldLogic` for your game's rules.

## Give a character an item and equip it

### 4. Create a sword with active damage and a passive strength bonus

`Scope.Use` modifiers run when the component is the action's instrument.
`Scope.Passive` modifiers participate through the component's active passive
relationships. Here, equipping the sword grants +2 strength; swinging it produces
12 physical damage against an enemy.

```csharp
var sword = Require(repository.Create(ItemType, SwordCategory));
sword.Name = "Longsword";
sword.ModifierData.Add(new ModifierData
{
    TypeId = "decrease", EffectTypeId = Physical,
    Scope = Scope.Use, Phase = Phase.Outgoing,
    RelationKind = RelationKind.Enemies,
    Data = new() { ["value"] = 12 }
});
sword.ModifierData.Add(new ModifierData
{
    TypeId = "increase", EffectTypeId = StrengthBonus,
    Scope = Scope.Passive, Phase = Phase.StatsRecalc,
    Data = new() { ["value"] = 2 }
});

foreach (var modifier in sword.ModifierData)
    Check(builder.Validate(modifier));

Check(relations.Add(hero.Id, sword.Id));
Console.WriteLine(hero.Stats[Strength].ModifiedMax); // 10: inventory alone grants no bonus

Check(relations.Equip(hero.Id, sword.Id, MainHand));
Console.WriteLine(hero.Stats[Strength].ModifiedMax); // 12: equipment activates the bonus
```

- `Add` creates an Inventory relation. An item must be in that character's
  inventory before `Equip` can equip it.
- `Equip` validates slots and category compatibility and recalculates the parent.
  An existing item occupying the requested slot returns to inventory.
- `Unequip` returns an equipped item to inventory; `Remove` detaches a relation.
- Use `repository.GetChildren(hero.Id)` to get child components, or
  `GetChildRelations(hero.Id)` to inspect their relation types and occupied slots.
- Tags combine instance tags, component-type tags and category ancestry via
  `ITagService.GetTags`. The sword inherits `"item"` and `"weapon"` in this example.

## Create and connect a skill

### 5. Give the hero Firebolt

A skill is another component. Connect it to a character instead of putting it in
an equipment slot. Use modifiers produce the skill's active effect; passive
modifiers on a Connected skill can augment its parent.

```csharp
var firebolt = Require(repository.Create(SkillType, SpellCategory));
firebolt.Name = "Firebolt";
firebolt.MetaData["level"] = "1";
firebolt.ModifierData.Add(new ModifierData
{
    TypeId = "decrease", EffectTypeId = Fire,
    Scope = Scope.Use, Phase = Phase.Outgoing,
    RelationKind = RelationKind.Enemies,
    Data = new() { ["value"] = 20 }
});
Check(builder.Validate(firebolt.ModifierData[0]));
Check(relations.Connect(hero.Id, firebolt.Id));
```

With `MultipleParentComponents = true`, the same skill instance can be connected
to multiple characters. Its data and modifiers are then shared. Create separate
instances when each character needs independent skill state. A parent cannot
connect two shared-type components of the same name (case-insensitive), which
prevents connecting multiple levels of the same named skill simultaneously.

## Use a weapon or skill

### 6. Resolve an action in one call

Use `Attack` for a weapon attack or `Use` for an item/skill action. Both validate
the action and coordinate Outgoing → Incoming → Apply for every target, including
additional reports such as healing from life steal. Use additionally consumes the
instrument once, after every target succeeds.

```csharp
var attack = Require(effects.Attack(hero.Id, sword.Id, orc.Id));
Console.WriteLine(orc.Stats[Health].Current); // 68

var spell = Require(effects.Use(hero.Id, firebolt.Id, orc.Id));
Console.WriteLine(orc.Stats[Health].Current); // 48

Check(relations.Unequip(hero.Id, sword.Id));
Console.WriteLine(hero.Stats[Strength].ModifiedMax); // 10
```

Negative effect amounts damage/drain the mapped stat; positive amounts heal/fill
it. The sword's strength bonus does not automatically scale its damage: configure
additional modifiers or custom rules when you want that relationship.

| Method | Applies effects | Consumes the instrument |
| --- | --- | --- |
| `Attack` | Yes | No |
| `Use` | Yes | Once: Eternal survives, Charges spends one, Consumed is deleted |

**Returned reports are already applied. Do not Apply them again or Consume again
after Use.** Either method can take an optional `int[] secondaryTargetIds` for
splash processing; modifiers determine which secondary targets receive effects.
Attack does not spend charges or delete consumables; use Use when payment is
required. Both reject exhausted or unusable instruments and enforce the existing
ownership/equipment requirements. Neither adds a separate damage/healing filter.

If calculation, application or final consumption fails, the whole action restores
its gameplay state, including earlier targets, stats, timed effects and relations.
Random draws are not rolled back. Calls against the same world must be serialized.
Your game selects targets and controls mana costs and cooldown policy.

The lower-level `Outgoing`, `Incoming`, `Apply`, `CanConsume` and `Consume` methods
remain available when you need to coordinate the pipeline yourself.

## Save and load gameplay state

### 7. Save a package and restore it into a configured world

`IWorldPackageService.Save` creates a detached `WorldPackage`:

- `Version`: package-format version, currently 1.
- `Components`: complete instance state, including stats, tags, data, modifier
  definitions, scheduled timed effects and retained Deleted components.
- `Relations`: inventory, equipment slots and connected/shared skills.
- `NextComponentId`: allocation cursor, including IDs reserved by failed creation.

World definitions are configured by code. The package does not contain stat/effect
definitions, component types, categories, equipment slots, ChargesStatId, calculator
implementations, world logic or random-source state. Configure and Build the world
normally before loading a package, using definition IDs compatible with the save.

The following continues the running example. On application startup, run the same
world-definition setup first, then load the saved JSON into that configured world.

```csharp
IWorldPackageService packages = new WorldPackageService();
var package = Require(packages.Save(world));
var json = JsonSerializer.Serialize(package, new JsonSerializerOptions { WriteIndented = true });
File.WriteAllText("save.json", json);

// Later: the target world must already have its definitions and services configured.
var saved = JsonSerializer.Deserialize<WorldPackage>(File.ReadAllText("save.json"))
    ?? throw new InvalidOperationException("The save file contains no package.");
Check(packages.Load(world, saved));

// Load replaces instance objects. Resolve them again by their saved IDs.
hero = Require(repository.Get(hero.Id));
orc = Require(repository.Get(orc.Id));
sword = Require(repository.Get(sword.Id));
firebolt = Require(repository.Get(firebolt.Id));
Console.WriteLine(orc.Stats[Health].Current); // 48: saved health is preserved
```

Load validates all component references, stats, modifiers, timed-effect origins,
relationship constraints, equipment compatibility and the ID cursor before
replacing live data. Unsupported package versions are rejected. A failed Load
leaves the existing gameplay state intact; a successful Load replaces components,
relations and the ID cursor, retaining the same repository, definitions and runtime
dependencies. Existing component/stat/relation references must be refreshed.

Load does not recalculate stats, tick effects, clean up Deleted origins or consume
random draws. Save and Load deep-copy their input data using Mapperly; the package
and loaded world do not share mutable state. Serialize Save/Load with other
operations on the same world. The caller handles JSON parsing, file/database I/O,
and any game-specific migration when definition IDs or rules change.

## Additional configuration

### Stat values and derived stats

| Property | Meaning |
| --- | --- |
| `Max` | Base maximum, or the value produced by a registered calculator |
| `Bonus` | Net passive StatsRecalc adjustment |
| `ModifiedMax` | Effective maximum after passive adjustments |
| `Current` | Runtime value, bounded by the effective maximum |

`StatKind.Pool` suits health/mana, `Fixed` suits values such as strength, and
`Counter` suits countdown values. Recalculation shifts Current by the change in
ModifiedMax, then clamps it to the new bounds. A full stat therefore stays full
when its effective maximum changes. Tick decreases Counter values toward zero.

Implement `IStatCalculator` to derive a stat from other values. Set its
`ComponentTypeId`, `StatTypeId` and `Order`, and implement
`Result<double> Calculate(int componentId, World world)`. Register it in
`world.Calculators` before Build; lower Order runs first. Recalculate explicitly
with `effects.Recalculate(componentId)` after changing inputs directly. Raw
repository Create does not automatically calculate derived stats or fill pools.

### Modifiers and phases

Register the modifier implementations you use in `world.Modifiers` and validate
their `ModifierData` with `builder.Validate`.

| Implementation | TypeId | Required Data keys |
| --- | --- | --- |
| `Decrease`, `Increase` | `decrease`, `increase` | `value` |
| `RandomDecrease`, `RandomIncrease` | `randomdecrease`, `randomincrease` | `min`, `max` |
| `Multiplier` | `multiplier` | `multiplier` |
| `Critical` | `critical` | `chance`, `multiplier` |
| `Splash` | `splash` | `range`, `percent` |
| `PreventFlat` | `preventflat` | `amount` |
| `PreventPercent` | `preventpercent` | `percent` |
| `Steal` | `steal` | `percent` |

Random modifiers and Critical take an `IRandomSource`; Splash takes the World.
Use `world.Random` when constructing random modifiers so they share your chosen
source. `Phase.Outgoing` builds/transforms an action; `Phase.Incoming` applies
target-side mitigation; `Phase.StatsRecalc` adjusts effective stat maxima.
Use scope requires Outgoing. StatsRecalc supports Increase/Decrease with Passive
scope and no relation/tag filters. Percentages and chances use the range 0–100.

### Timed effects

A positive `ModifierData.Duration` produces timed effect items. Apply schedules
them on the target. Call `effects.Tick(targetId, elapsed)` as game time advances;
Tick resolves and applies scheduled effects, decreases durations/counters, and
returns reports for logging or UI. **Do not Apply Tick's reports again.** The game
chooses the unit of time. Duration-zero items apply immediately through Apply.

### Whole-number effect application

Set `world.Settings.RoundEffectAmounts = true` to apply whole-number effect deltas. The
default is `false`, preserving fractional application.

`Apply` combines immediate effect amounts by target stat, rounds each combined
amount with `Math.Round`, then adds it to Current and clamps to 0..ModifiedMax.
Rounding uses .NET's default midpoint-to-even behavior: 1.5 becomes 2, 2.5 becomes
2, and -2.5 becomes -2. Attack, Use and timed-effect ticks all use this application
rule, including healing produced by steal.

Outgoing calculations and incoming mitigation retain fractional precision while
running. `Incoming` rounds its final immediate result contributions before returning,
so deferred presentation previews already contain whole amounts without applying
any effects. Steal uses the rounded target damage, and its returned healing amounts
are rounded too. `Apply` also rounds immediate contributions for direct callers;
applying an already-rounded preview preserves its amounts. Attack, Use and Tick
reports therefore contain rounded amounts too. For multiple contributions to the
same stat, rounding cumulative totals in item order distributes whole amounts
whose sum matches the rounded stat delta. IDs, effect types and durations are
preserved, including contributions rounded to zero.

Stored timed amounts retain fractional precision and are rounded when each tick
is applied, not when scheduled. Reports describe rounded effect deltas before
clamping; actual changes can be smaller at stat bounds. This setting rounds effect
deltas rather than existing stat values or maxima, and does not round stat
recalculation, durations or elapsed time.

### Consumables and charges

Category `Usable` controls consumption:

- `No`: cannot be used as a consumable instrument.
- `Eternal`: reusable, as with the example sword and skill.
- `Consumed`: deleted when consumed, such as a potion.
- `Charges`: spends one charge per consumption and deletes the exhausted item.

For Charges, define a Pool stat, assign its ID to `world.ChargesStatId`, include it
in the item's `ComponentType.StatIds`, and initialize its values. The one-call Use
method checks eligibility and consumes once after the action succeeds. When using
the lower-level pipeline, call CanConsume before resolution and Consume afterward;
Outgoing, Incoming and Apply do not consume automatically.

### Repository lifecycle

The default repository is in-memory. Use `Get`/`Create`/`SetStat` and relation
services for normal operations. `Delete` handles gameplay deletion;
`Cleanup` removes eligible deleted records. Gameplay operations update state directly;
if a later step fails, changes already made remain in place. This also applies to
component initialization in the generation library. Repository `Save()` and `Load(package)`
implement detached state export/import and preserve the allocation cursor;
custom repositories must provide the same validated, atomic package semantics.

To prepare a component before registering it, use `CreateDetached` and `Attach`:

```csharp
var sword = Require(repository.CreateDetached(ItemType, SwordCategory));
// sword.Id == -1; sword.State == ComponentState.Detached
sword.Name = "Prepared sword";
sword.Tags.Add("crafted");
// Edit sword.Stats, MetaData and ModifierData directly while detached.
Require(repository.Attach(sword));
// Same instance, now Active with a positive ID and visible through Get/Save.
Check(relations.Add(hero.Id, sword.Id));
```

`new Component()` also defaults to ID -1 and Detached state. `CreateDetached`
validates the type/category and initializes all declared stats without allocating
an ID. Detached components are absent from repository queries and saves, cannot
participate in gameplay relations, and can simply be discarded if unwanted.

`Attach` requires ID -1 and Detached state. It revalidates metadata, the exact
declared stats, finite stat values with Current within 0..ModifiedMax, modifiers
and charges configuration. Scheduled effects are not allowed on new components.
Validation failures leave the object detached for correction and retry. Successful
attachment registers the same object and does not recalculate stats or create a
parent relation. Call `effects.Recalculate(sword.Id)` when needed after attachment.

`Create` is a convenience wrapper around `CreateDetached` followed by `Attach`.
Custom repositories implement both methods with these lifecycle semantics.
Deleted components retain their positive IDs after cleanup and cannot be attached
again. Packages accept only Active/Deleted components; they cannot store Detached
components. The existing numeric state values remain Active = 0 and Deleted = 1;
Detached = 2.

## Dependency injection

The example constructs services explicitly. For a DI-based application, register
the configured and built World instance, then call
`Dexterity.ServiceExtensions.Register(services)` on your `IServiceCollection`.
It registers the repository, effect/relation/tag services, world package service,
world builder, world logic and random source through their interfaces. World
registration and Build are still the caller's responsibility.

Services resolve dependencies from that shared mutable World. Serialize gameplay
operations and definition changes for a world; create separate worlds for
independent games. Registration itself does not add persistence or per-player
isolation.

## Run the tests

From the repository root:

```sh
dotnet test src/Dexterity.sln
```

See [Dexterity.Tests](../Dexterity.Tests/) for the core test suite and
[Dexterity.Generation](../Dexterity.Generation/README.md) for template-based creation.
