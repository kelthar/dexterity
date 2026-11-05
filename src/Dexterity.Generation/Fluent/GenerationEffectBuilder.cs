namespace Dexterity.Generation;

/// <summary>Captures effect settings for modifiers authored into the current budget node.</summary>
public class GenerationEffectBuilder : GenerationNodeBuilder<GenerationEffectBuilder>
{
    private readonly ModifierData _settings = null!;

    /// <summary>References a node and captures independently mutable effect settings.</summary>
    internal GenerationEffectBuilder(GenerationTemplate template, GenerationNode node, ModifierData settings) : base(template, node) => _settings = GenerationMapper.Copy(settings);

    /// <summary>Targets enemies with outgoing use modifiers.</summary>
    public GenerationEffectBuilder Attack() => Target(Scope.Use, Phase.Outgoing, RelationKind.Enemies);

    /// <summary>Targets self with outgoing use modifiers.</summary>
    public GenerationEffectBuilder UseSelf() => Target(Scope.Use, Phase.Outgoing, RelationKind.Self);

    /// <summary>Configures passive incoming modifiers against enemies.</summary>
    public GenerationEffectBuilder Defend() => Target(Scope.Passive, Phase.Incoming, RelationKind.Enemies);

    /// <summary>Configures stat-recalculation modifiers without relation filtering.</summary>
    public GenerationEffectBuilder Passive() => Target(Scope.Passive, Phase.StatsRecalc, RelationKind.None);

    /// <summary>Sets explicit targeting for subsequently authored modifiers.</summary>
    public GenerationEffectBuilder Target(Scope scope, Phase phase, RelationKind relations)
    {
        ArgumentNullException.ThrowIfNull(this);
        if (!Enum.IsDefined(scope) || !Enum.IsDefined(phase) || (relations & ~(RelationKind.Self | RelationKind.Friends | RelationKind.Enemies)) != 0)
            throw new ArgumentOutOfRangeException(nameof(scope), "Scope, phase and relation flags must be defined values.");
        _settings.Scope = scope;
        _settings.Phase = phase;
        _settings.RelationKind = relations;
        return this;
    }

    /// <summary>Appends included tags, copying the caller's collection.</summary>
    public GenerationEffectBuilder IncludeTags(params string[] tags)
    {
        ArgumentNullException.ThrowIfNull(this);
        FluentRules.Tags(_settings.IncludedTags, tags);
        return this;
    }

    /// <summary>Appends excluded tags, copying the caller's collection.</summary>
    public GenerationEffectBuilder ExcludeTags(params string[] tags)
    {
        ArgumentNullException.ThrowIfNull(this);
        FluentRules.Tags(_settings.ExcludedTags, tags);
        return this;
    }

    /// <summary>Appends a budget group inheriting a copy of these effect settings.</summary>
    public GenerationEffectBuilder Group(string id, int weight, Action<GenerationEffectGroupBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(this);
        ArgumentNullException.ThrowIfNull(configure);
        var group = FluentRules.Group(Template, Node, id, weight);
        configure(new GenerationEffectGroupBuilder(Template, group, _settings));
        return this;
    }

    /// <summary>Authors a decrease with a generated Value parameter.</summary>
    public GenerationEffectBuilder Decrease(Action<ValueModifierBuilder> configure) => Add("decrease", configure, (t, n, m, i) => new ValueModifierBuilder(t, n, m, i), "value");

    /// <summary>Authors an increase with a generated Value parameter.</summary>
    public GenerationEffectBuilder Increase(Action<ValueModifierBuilder> configure) => Add("increase", configure, (t, n, m, i) => new ValueModifierBuilder(t, n, m, i), "value");

    /// <summary>Authors runtime-random damage with generated Min and Max parameters.</summary>
    public GenerationEffectBuilder RandomDecrease(Action<RangeModifierBuilder> configure) => Add("randomdecrease", configure, (t, n, m, i) => new RangeModifierBuilder(t, n, m, i), "min", "max");

    /// <summary>Authors runtime-random healing with generated Min and Max parameters.</summary>
    public GenerationEffectBuilder RandomIncrease(Action<RangeModifierBuilder> configure) => Add("randomincrease", configure, (t, n, m, i) => new RangeModifierBuilder(t, n, m, i), "min", "max");

    /// <summary>Authors a scaling modifier; Multiplier 2 means twice the existing contribution.</summary>
    public GenerationEffectBuilder Multiplier(Action<MultiplierModifierBuilder> configure) => Add("multiplier", configure, (t, n, m, i) => new MultiplierModifierBuilder(t, n, m, i), "multiplier");

    /// <summary>Authors critical scaling with percentage Chance and numeric Multiplier.</summary>
    public GenerationEffectBuilder Critical(Action<CriticalModifierBuilder> configure) => Add("critical", configure, (t, n, m, i) => new CriticalModifierBuilder(t, n, m, i), "chance", "multiplier");

    /// <summary>Authors splash with generated Range and percentage Percent.</summary>
    public GenerationEffectBuilder Splash(Action<SplashModifierBuilder> configure) => Add("splash", configure, (t, n, m, i) => new SplashModifierBuilder(t, n, m, i), "range", "percent");

    /// <summary>Authors flat incoming prevention with a generated Amount.</summary>
    public GenerationEffectBuilder PreventFlat(Action<AmountModifierBuilder> configure) => Add("preventflat", configure, (t, n, m, i) => new AmountModifierBuilder(t, n, m, i), "amount");

    /// <summary>Authors percentage incoming prevention.</summary>
    public GenerationEffectBuilder PreventPercent(Action<PercentModifierBuilder> configure) => Add("preventpercent", configure, (t, n, m, i) => new PercentModifierBuilder(t, n, m, i), "percent");

    /// <summary>Authors steal using a percentage of the runtime post-mitigation basis.</summary>
    public GenerationEffectBuilder Steal(Action<PercentModifierBuilder> configure) => Add("steal", configure, (t, n, m, i) => new PercentModifierBuilder(t, n, m, i), "percent");

    /// <summary>Appends a typed modifier, runs its callback and checks its required bindings.</summary>
    private GenerationEffectBuilder Add<T>(string type, Action<T> configure, Func<GenerationTemplate, GenerationNode, ModifierTemplate, int, T> factory, params string[] required)
    {
        ArgumentNullException.ThrowIfNull(this);
        ArgumentNullException.ThrowIfNull(configure);

        // Capture settings once; later changes to the effect context must not leak back.
        var ordinal = Template.Modifiers.Count;
        var modifier = new ModifierTemplate { Base = GenerationMapper.Copy(_settings) };
        modifier.Base.TypeId = type;
        Template.Modifiers.Add(modifier);
        configure(factory(Template, Node, modifier, ordinal));

        // Validate authoring completeness now rather than defer to an absent Build stage.
        foreach (var key in required)
        {
            if (!modifier.DataKeys.ContainsKey(key))
                throw new InvalidOperationException($"Modifier '{type}' requires parameter '{key}'.");
        }
        return this;
    }
}
