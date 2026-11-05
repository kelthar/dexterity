using Riok.Mapperly.Abstractions;

namespace Dexterity;

[Mapper(UseDeepCloning = true)]
internal static partial class PackageMapper
{
    internal static partial WorldPackage Copy(WorldPackage source);
}

[Mapper(UseDeepCloning = false)]
internal static partial class ConfigurationMapper
{
    [MapperIgnoreTarget(nameof(World.Repository))]
    [MapperIgnoreSource(nameof(World.Repository))]
    internal static partial World Copy(World source);
}
