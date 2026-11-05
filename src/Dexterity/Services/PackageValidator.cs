namespace Dexterity;

internal sealed class PackageValidator(World world)
{
    internal Result Validate(WorldPackage package)
    {
        // validate code-configured definitions on a separate container
        if (world == null || package == null || package.Version != 1 || package.Components == null || package.Relations == null)
            return Result.Fail("A version 1 world package with component and relation lists is required.", 400);
        var configuration = ConfigurationMapper.Copy(world);
        if (new WorldBuilder(configuration).Build(configuration).Failed(out var definitions))
            return EffectRules.Failure(definitions);
        if (package.Components.Any(x => x == null || x.Id <= 0) || package.Components.GroupBy(x => x.Id).Any(x => x.Count() > 1))
            return Result.Fail("Component IDs must be positive and unique.", 400);
        var maximum = package.Components.Select(x => x.Id).DefaultIfEmpty(0).Max();
        if (package.NextComponentId <= maximum || package.NextComponentId > (long)int.MaxValue + 1)
            return Result.Fail("The next component ID must exceed every saved ID and be within the allocation range.", 400);
        var components = package.Components.ToDictionary(x => x.Id);
        foreach (var component in package.Components)
        {
            if (Component(component, components).Failed(out var validation))
                return validation;
        }
        return Relations(package.Relations, components);
    }

    private Result Component(Component component, Dictionary<int, Component> components)
    {
        // repository packages only contain previously attached components
        if (component.State != ComponentState.Active && component.State != ComponentState.Deleted)
            return Result.Fail("Saved components must be Active or Deleted.", 400);
        if (new ComponentValidator(world).Validate(component).Failed(out var validation))
            return validation;
        foreach (var effect in component.Effects)
        {
            if (effect == null || effect.TargetId != component.Id || effect.Items == null ||
                !components.ContainsKey(effect.SourceId) || !components.ContainsKey(effect.InstrumentId))
                return Result.Fail("Scheduled effects must identify their owner and existing origins.", 400);
            foreach (var item in effect.Items)
            {
                if (item == null || !EffectRules.Finite(item.Amount) || !EffectRules.Finite(item.Duration) || item.Duration <= 0 ||
                    !world.EffectTypes.Any(x => x.Id == item.EffectTypeId) ||
                    !components.ContainsKey(item.SourceId) || !components.ContainsKey(item.InstrumentId))
                    return Result.Fail("Scheduled effect items require known types/origins and finite amounts with positive durations.", 400);
            }
        }
        return Result.Ok();
    }

    private Result Relations(List<ComponentRelation> relations, Dictionary<int, Component> components)
    {
        // validate endpoint, ownership and equipment constraints before graph traversal
        if (relations.Any(x => x == null || x.Id <= 0) || relations.GroupBy(x => x.Id).Any(x => x.Count() > 1) ||
            relations.GroupBy(x => (x.ParentId, x.TargetId)).Any(x => x.Count() > 1))
            return Result.Fail("Relations require positive unique IDs and unique parent/target pairs.", 400);
        var occupied = new HashSet<(int Parent, int Slot)>();
        foreach (var relation in relations)
        {
            if (!Enum.IsDefined(relation.Type) || relation.SlotIds == null ||
                !components.TryGetValue(relation.ParentId, out var parent) || !components.TryGetValue(relation.TargetId, out var child) ||
                parent.State != ComponentState.Active || child.State != ComponentState.Active)
                return Result.Fail("Relations require valid kinds, slots and Active endpoints.", 400);
            if (relation.Type != RelationType.Equipped)
            {
                if (relation.SlotIds.Count != 0)
                    return Result.Fail("Only Equipped relations can occupy slots.", 400);
                continue;
            }
            var category = world.Categories.First(x => x.Id == child.CategoryId);
            if (!category.Equipable || relation.SlotIds.Count == 0)
                return Result.Fail("Equipped components require an equipable category and at least one slot.", 400);
            foreach (var id in relation.SlotIds)
            {
                var slot = world.Slots.FirstOrDefault(x => x.Id == id);
                if (slot == null || !occupied.Add((parent.Id, id)) ||
                    (slot.Categories?.Count > 0 && !slot.Categories.Any(x => EffectRules.Descends(world, child.CategoryId, x))))
                    return Result.Fail("Equipment slots must be known, compatible and uniquely occupied per parent.", 400);
            }
        }
        foreach (var group in relations.GroupBy(x => x.TargetId))
        {
            var type = world.ComponentTypes.First(x => x.Id == components[group.Key].TypeId);
            if (!type.MultipleParentComponents && group.Count() > 1)
                return Result.Fail("This component type does not allow multiple parents.", 400);
        }
        var shared = relations.Where(x => x.Type == RelationType.Connected)
            .Where(x => world.ComponentTypes.First(t => t.Id == components[x.TargetId].TypeId).MultipleParentComponents);
        foreach (var group in shared.GroupBy(x => (x.ParentId, components[x.TargetId].TypeId)))
        {
            if (group.GroupBy(x => components[x.TargetId].Name, StringComparer.OrdinalIgnoreCase).Any(x => x.Count() > 1))
                return Result.Fail("Only one level of a shared named skill can be connected per parent.", 400);
        }

        // topological traversal detects cycles without recursion on deep graphs
        var indegrees = components.Keys.ToDictionary(x => x, _ => 0);
        var children = relations.ToLookup(x => x.ParentId, x => x.TargetId);
        foreach (var relation in relations)
            indegrees[relation.TargetId]++;
        var ready = new Queue<int>(indegrees.Where(x => x.Value == 0).Select(x => x.Key));
        var visited = 0;
        while (ready.TryDequeue(out var id))
        {
            visited++;
            foreach (var child in children[id])
            {
                if (--indegrees[child] == 0)
                    ready.Enqueue(child);
            }
        }
        return visited == components.Count ? Result.Ok() : Result.Fail("Component relations cannot contain cycles.", 400);
    }
}
