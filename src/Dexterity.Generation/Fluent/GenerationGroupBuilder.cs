namespace Dexterity.Generation;

/// <summary>Configures a live budget group and its nested entries.</summary>
public sealed class GenerationGroupBuilder : GenerationNodeBuilder<GenerationGroupBuilder>
{
    /// <summary>References the new group in its original template.</summary>
    internal GenerationGroupBuilder(GenerationTemplate template, GenerationGroup group) : base(template, group) { }

    /// <summary>Appends an explicit nested budget group.</summary>
    public GenerationGroupBuilder Group(string id, int weight, Action<GenerationGroupBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(this);
        ArgumentNullException.ThrowIfNull(configure);
        var group = FluentRules.Group(Template, Node, id, weight);
        configure(new GenerationGroupBuilder(Template, group));
        return this;
    }

    /// <summary>Sets the group's ascending execution order.</summary>
    public GenerationGroupBuilder Order(int value)
    {
        ArgumentNullException.ThrowIfNull(this);
        new GenerationEntryBuilder((GenerationGroup)Node).Order(value);
        return this;
    }

    /// <summary>Sets the group's inclusive weight variation.</summary>
    public GenerationGroupBuilder VaryWeightBy(int value)
    {
        ArgumentNullException.ThrowIfNull(this);
        new GenerationEntryBuilder((GenerationGroup)Node).VaryWeightBy(value);
        return this;
    }

    /// <summary>Sets inclusion percent for the entire branch; 0 and 100 mean always.</summary>
    public GenerationGroupBuilder IncludeChance(int value)
    {
        ArgumentNullException.ThrowIfNull(this);
        new GenerationEntryBuilder((GenerationGroup)Node).IncludeChance(value);
        return this;
    }

    /// <summary>Adds a request-context condition for the whole branch.</summary>
    public GenerationGroupBuilder When(string key, ConditionOperator @operator, string value)
    {
        ArgumentNullException.ThrowIfNull(this);
        new GenerationEntryBuilder((GenerationGroup)Node).When(key, @operator, value);
        return this;
    }
}
