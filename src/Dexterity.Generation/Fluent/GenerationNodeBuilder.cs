namespace Dexterity.Generation;

/// <summary>References a template and the node receiving newly authored values and effects.</summary>
public abstract class GenerationNodeBuilder<TSelf> where TSelf : GenerationNodeBuilder<TSelf>
{
    internal GenerationTemplate Template { get; } = null!;
    internal GenerationNode Node { get; } = null!;

    /// <summary>References an existing budget node without copying its definition.</summary>
    protected GenerationNodeBuilder(GenerationTemplate template, GenerationNode node)
    {
        Template = template;
        Node = node;
    }

    /// <summary>Creates an independent effect context without adding a budget group.</summary>
    public GenerationEffectBuilder EffectType(int id)
    {
        ArgumentNullException.ThrowIfNull(this);
        return new GenerationEffectBuilder(Template, Node, new ModifierData { EffectTypeId = id, Scope = Scope.Use, Phase = Phase.Outgoing, RelationKind = RelationKind.Enemies });
    }

    /// <summary>Configures an effect synchronously and returns this node's context.</summary>
    public TSelf EffectType(int id, Action<GenerationEffectBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(this);
        ArgumentNullException.ThrowIfNull(configure);
        configure(EffectType(id));
        return (TSelf)this;
    }

    /// <summary>Appends a named output using the supplied value strategy.</summary>
    public TSelf Value(string id, Action<GenerationValueBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(this);
        FluentRules.Value(Template, Node, id, configure);
        return (TSelf)this;
    }
}
