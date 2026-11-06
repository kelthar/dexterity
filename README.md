# Dexterity

A component-based, customizable .NET solution for handling RPG character
components, with simple generation of characters and items.

Characters, items and skills are all **components** living in a configurable
**world**. You define the stats, categories, equipment slots and effects; Dexterity
handles the relationships and rules between them. Your game decides when actions
happen, which targets are chosen, and how results are stored or shown.

## Why

I am using this for my own game that I tinker on in my free time. The first system had it's flaws and this is the second generation of a rpg system. I made it to be flexible and customizable to a degree. There is always a tradeoff between how generic a system is and how easy it is to use. When I have the time I will create an example project to demonstrate in detail on how to set it up and how to make it easier to work with.

## Projects

| Project | Purpose | Docs |
| --- | --- | --- |
| [`Dexterity`](src/Dexterity) | Core library: components, stats, inventory, equipment, modifiers, combat and save/load | [README](src/Dexterity/README.md) |
| [`Dexterity.Generation`](src/Dexterity.Generation) | Template- and budget-based generation of components | [README](src/Dexterity.Generation/README.md) |
| [`Dexterity.Tests`](src/Dexterity.Tests) | xUnit tests for the core library | – |
| [`Dexterity.Generation.Tests`](src/Dexterity.Generation.Tests) | xUnit tests for the generation library | – |

### Dexterity (core)

A data-driven rules engine. Everything is defined as data in a `World`, so the
same engine can power very different games.

- **Stats**: pools (health, mana), fixed values (strength) and counters, plus
  custom derived-stat calculators.
- **Component model**: component types, hierarchical categories, tags and
  per-instance data.
- **Relations**: inventory, equipment slots, and connected (optionally shared) skills.
- **Modifiers**: active and passive modifiers for damage, healing, random ranges,
  multipliers, critical hits, splash damage, flat/percent mitigation and life steal.
- **Actions**: one-call `Attack` and `Use` that resolve outgoing → incoming → apply
  for every target, with automatic rollback on failure.
- **Effects and items**: timed effects with `Tick`, consumables and charges,
  optional whole-number rounding.
- **Persistence**: JSON-serializable gameplay-state packages for save/load.
- **Extensibility**: replaceable world logic and random source, in-memory repository,
  `Result`-based service contracts and Microsoft DI registration.

See the [Dexterity README](src/Dexterity/README.md) for a full walkthrough.

### Dexterity.Generation

Builds on the core library to generate components from templates and a worth budget.

- **Templates and catalog** with stat bindings and modifier templates.
- **Worth budgets**: weighted shares distributed exactly (largest remainder);
  fixed values cost nothing; random ranges are supported.
- **Fluent API** for modifiers, e.g. `Attack()`, `Defend()`, `Passive()`, `UseSelf()`
  with `Decrease`, `Critical`, `RandomDecrease`, `PreventFlat`, `Splash` and more.
- **Conditional groups** driven by request context (e.g. only rare items get splash).
- **Reproducible results** via seeds, with reports of total, unused and overspent worth.
- **Safe construction**: components are built detached and only attached once valid.
- DI registration through `Dexterity.Generation.ServiceExtensions.Register`.

See the [Dexterity.Generation README](src/Dexterity.Generation/README.md) for details.

## Installation

Install the packages from NuGet:

```sh
dotnet add package Dexterity
dotnet add package Dexterity.Generation
```

## Quick start

Configure a `World`, build it with `WorldBuilder`, then create components through
the repository or the generation service. The project READMEs contain complete
examples.
