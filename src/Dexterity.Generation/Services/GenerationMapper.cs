using Riok.Mapperly.Abstractions;

namespace Dexterity.Generation;

[Mapper(UseDeepCloning = true, RequiredMappingStrategy = RequiredMappingStrategy.Target)]
internal static partial class GenerationMapper
{
    /// <summary>Deep-clones a base modifier so generated overrides cannot mutate the template.</summary>
    public static partial ModifierData Copy(ModifierData source);

    /// <summary>Copies template metadata to a detached component without replacing its lifecycle or initialized collections.</summary>
    [MapProperty(nameof(GenerationTemplate.ComponentTags), nameof(Component.Tags))]
    [MapperIgnoreTarget(nameof(Component.Id))]
    [MapperIgnoreTarget(nameof(Component.State))]
    [MapperIgnoreTarget(nameof(Component.Stats))]
    [MapperIgnoreTarget(nameof(Component.MetaData))]
    [MapperIgnoreTarget(nameof(Component.ModifierData))]
    [MapperIgnoreTarget(nameof(Component.Effects))]
    public static partial void Apply(GenerationTemplate source, Component target);
}
