namespace Dexterity;

public interface IComponentRepository
{
    Result<WorldPackage> Save();
    Result Load(WorldPackage package);
    Dictionary<int, Component> Items { get; }
    List<ComponentRelation> Relations { get; }
    /// <summary>Creates an unregistered component with declared stats, Detached state and ID -1, without allocating an identity.</summary>
    Result<Component> CreateDetached(int typeId, int categoryId);
    /// <summary>Validates and registers a never-attached component, assigning a positive ID and Active state. Returns the same instance.</summary>
    Result<Component> Attach(Component component);
    /// <summary>Creates a detached component and immediately attaches it.</summary>
    Result<Component> Create(int typeId, int categoryId);
    Result<Component> Get(int componentId);
    /// <summary>Returns all registered components with the exact category ID, including retained Deleted components; returns an empty list when none match.</summary>
    List<Component> GetByCategory(int categoryId);
    /// <summary>Returns all registered components whose full name matches using ordinal case-insensitive comparison, including retained Deleted components; returns an empty list when none match.</summary>
    List<Component> GetByName(string name);
    List<ComponentRelation> GetChildRelations(int componentId);
    List<ComponentRelation> GetParentRelations(int componentId);
    List<Component> GetChildren(int componentId);
    List<Component> GetParents(int componentId);
    Result SetStat(int componentId, Stat stat);
    Result Delete(int componentId);
    Result Cleanup();
}
