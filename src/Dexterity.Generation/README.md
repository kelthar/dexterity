# Dexterity.Generation

A .NET 8 template-based generator for Dexterity components. It ports the core
Fourtwo.Generation concepts without depending on the old Fourtwo package.

## Installation

```sh
dotnet add package Dexterity
dotnet add package Dexterity.Generation
```

## Create a component

Configure and build a Dexterity `World` first. Define templates with explicit
component type/category IDs, budget entries, and bindings to stats, data and
numeric modifier parameters.

```csharp
var template = new GenerationTemplate
{
    Id = "sword",
    Name = "Longsword",
    TypeId = itemTypeId,
    CategoryId = swordCategoryId
};
template.AddWeightValue("damage", weight: 3, worthPerValue: 2);
template.AddWeightValue("durability", weight: 1);
template.StatBindings.Add(new StatBinding
{
    StatTypeId = durabilityStatId,
    MaxKey = "durability"
});
template.Modifiers.Add(new ModifierTemplate
{
    Base = new ModifierData
    {
        TypeId = "decrease",
        EffectTypeId = physicalEffectTypeId,
        Phase = Phase.Outgoing,
        Scope = Scope.Use,
        RelationKind = RelationKind.Enemies
    },
    DataKeys = new() { ["value"] = "damage" }
});
var catalog = new GenerationCatalog { Templates = new() { template } };
IGenerationService generation = new GenerationService(world, catalog, new DefaultGenerationRandom());
if (generation.Create(new GenerationRequest
    {
        TemplateId = "sword",
        Worth = 40,
        Seed = 123
    }).Failed(out var created))
{
    // Handle created.Code and created.States.
    return;
}
var component = created.Data!.Component;
var componentId = component.Id;
var actualWorth = created.Data.Generation.TotalWorth;
```

This produces damage 15 and durability 10. The caller assigns any actual price,
adds the component to inventory, and equips/connects it through normal Dexterity
services. Discovery tags and copied component tags are separate.

## Fluent modifier examples

Use `using Dexterity.Generation;` to access the fluent extensions. Calls update
the template immediately; pass that same template to the catalog without a
`Build()` step. The effect IDs below refer to effects registered in your world.

### Damage and critical hits

```csharp
var weapon = new GenerationTemplate
{
    Id = "critical-sword",
    Name = "Critical sword",
    TypeId = itemTypeId,
    CategoryId = swordCategoryId
};

weapon.EffectType(physicalEffectTypeId).Attack()
    .Decrease(x => x.Value.Weight(worthPerValue: 2, weight: 5))
    .Critical(x => x.Chance.Fixed(25).Multiplier.Fixed(2));
```

With a worth budget of 40, this generates damage 20 and a 25% runtime chance to
double matching damage. Fixed parameters consume no worth. `Attack()` configures
outgoing use modifiers against enemies; it does not execute an attack.

### Random damage bounds

```csharp
weapon.EffectType(physicalEffectTypeId).Attack()
    .RandomDecrease(x => x.Min.Fixed(3).Max.Random(
        worthPerValue: 1, min: 6, max: 12));
```

`Max.Random(...)` chooses the maximum once during component generation. Later,
`RandomDecrease` samples damage between the generated minimum and maximum when
used at runtime. This appends another modifier to `weapon`.

### Self-healing

```csharp
healingTemplate.EffectType(healEffectTypeId).UseSelf()
    .Increase(x => x.Value.Fixed(10));
```

Here `healingTemplate` is a configured `GenerationTemplate` and `healEffectTypeId`
is the world's healing effect. `UseSelf()` targets the user rather than an enemy.

### Defensive and passive modifiers

```csharp
armorTemplate.EffectType(physicalEffectTypeId).Defend()
    .PreventFlat(x => x.Amount.Weight(worthPerValue: 1))
    .PreventPercent(x => x.Percent.Fixed(25));

strengthTemplate.EffectType(strengthEffectTypeId).Passive()
    .Increase(x => x.Value.Fixed(3));
```

Use configured templates for `armorTemplate` and `strengthTemplate`, with the
appropriate registered effects. `Defend()` configures passive incoming modifiers;
`Passive()` configures stat-recalculation modifiers. Generated providers participate
through normal Dexterity ownership/equipment relations. A multiplier of `2` means
twice the contribution; percentage inputs such as `Percent` and `Chance` use
values from 0 to 100.

### A conditional modifier group

```csharp
weapon.EffectType(physicalEffectTypeId).Attack()
    .Group("rare-bonus", weight: 1, group =>
    {
        group.When("rarity", ConditionOperator.Equal, "rare");
        group.Splash(x => x.Range.Fixed(3).Percent.Fixed(50));
    });
```

The splash modifier is included only when the generation request's `Context`
contains `"rarity" = "rare"`. Conditions read request context, and multiple
conditions use AND. Explicit groups create nested budget scopes; opening an
`EffectType(...)` scope alone does not. Even a fixed-only group can control whether
its modifiers are included.

## Generation rules

- Weighted shares use exact largest-remainder allocation, stable by Order and
  list position. Unused shares are not redistributed after generation.
- Fixed values are free. Recursively Fixed-only/empty groups take no allocation.
- Random integer endpoints are inclusive. Random minima may exceed allocation;
  reports expose actual TotalWorth, net UnusedWorth and Overspend.
- Chance 0 and 100 mean always. Other chances use a strict 0..99 threshold.
- Conditions read request Context only. String comparisons are ordinal; numeric
  inputs use plain dot decimals with no thousands separators or exponents.
- Each request generates once. A Seed uses an independent local random source;
  otherwise the injected stream advances, including draws consumed by failures.
- The returned `Component` is the live repository instance, with recalculated stats
  and creation-only Current defaults. `Generation.Values` retains generated source
  text and the report includes worth accounting.

## Component construction

Generation calls `IComponentRepository.CreateDetached`, then binds the template's
name, component tags, MetaData, stats and concrete modifier copies directly onto that
component. During binding it has `State = Detached`, `Id = -1`, and no repository
membership or allocated identity.

After successful binding, `Attach` validates and registers the same instance,
assigns a positive ID and changes its state to Active. Generation then recalculates
stats using the registered component. Explicit initial Current values are preserved
and clamped to the final maximum; omitted values default to the final maximum for
Pool/Fixed stats and zero for Counters, including unbound stats. Template collections
and modifiers are copied so generated edits do not change templates or other results.

## Creation failures

Generation and binding validation finish before `IComponentRepository.Attach`.
Binding or attachment validation failures leave no repository entry or allocated ID.
Once attached, recalculation updates the repository directly. If recalculation or
final current initialization fails, the attached component and changes already made
remain in the repository. Consumed random draws are retained on failure.

## Dependency injection

Register the configured World and GenerationCatalog instances, then call
`Dexterity.Generation.ServiceExtensions.Register(services)` explicitly. The
default generation random source is singleton; the service is transient. An
already registered `IGenerationRandom` is preserved. The qualified call avoids
ambiguity with Dexterity's own parameterless Register extension.

Serialize operations and definition edits on a shared mutable world/catalog.

## Tests

```sh
dotnet test src/Dexterity.sln
```

Specification and criteria: `.ai/spectre/dexterity-generation.md` and
`.ai/spectre/dexterity-generation-ac.md` at the repository root.
