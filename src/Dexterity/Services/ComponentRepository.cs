namespace Dexterity;

public class ComponentRepository : IComponentRepository
{
    protected World World { get; }
    private long _nextId = 1;
    public ComponentRepository(World world) => World = world;
    public Dictionary<int, Component> Items { get; } = new();
    public List<ComponentRelation> Relations { get; } = new();

    public Result<WorldPackage> Save()
    {
        try
        {
            // export the allocation high-water mark, including permanently reserved IDs
            if (Items.Any(x => x.Value == null || x.Key != x.Value.Id))
                return EffectRules.Failure<WorldPackage>(Result.Fail("Repository keys must match component IDs.", 400));
            var package = new WorldPackage
            {
                Components = Items.Values.ToList(), Relations = Relations.ToList(),
                NextComponentId = Math.Max(_nextId, (long)Items.Keys.DefaultIfEmpty(0).Max() + 1)
            };
            if (new PackageValidator(World).Validate(package).Failed(out var validation))
                return EffectRules.Failure<WorldPackage>(validation);
            return Result.Ok(PackageMapper.Copy(package));
        }
        catch (Exception exception)
        {
            return EffectRules.Failure<WorldPackage>(EffectRules.Exception(exception, "World save failed."));
        }
    }

    public Result Load(WorldPackage package)
    {
        try
        {
            // validate and detach all input before replacing live state
            if (new PackageValidator(World).Validate(package).Failed(out var validation))
                return validation;
            var copy = PackageMapper.Copy(package);
            var items = copy.Components.ToDictionary(x => x.Id);
            var oldItems = Items.ToArray();
            var oldRelations = Relations.ToArray();
            var nextId = _nextId;
            try
            {
                Items.Clear();
                foreach (var pair in items)
                    Items.Add(pair.Key, pair.Value);
                Relations.Clear();
                Relations.AddRange(copy.Relations);
                _nextId = copy.NextComponentId;
            }
            catch
            {
                Items.Clear();
                foreach (var pair in oldItems)
                    Items.Add(pair.Key, pair.Value);
                Relations.Clear();
                Relations.AddRange(oldRelations);
                _nextId = nextId;
                throw;
            }
            return Result.Ok();
        }
        catch (Exception exception)
        {
            return EffectRules.Exception(exception, "World load failed.");
        }
    }

    public Result<Component> Create(int typeId, int categoryId)
    {
        if (CreateDetached(typeId, categoryId).Failed(out var detached))
            return EffectRules.Failure<Component>(detached);
        return Attach(detached.Data!);
    }

    public Result<Component> CreateDetached(int typeId, int categoryId)
    {
        // validate definitions before allocating identity
        var type = World.ComponentTypes.FirstOrDefault(x => x.Id == typeId);
        var category = World.Categories.FirstOrDefault(x => x.Id == categoryId);
        if (type == null || category == null)
            return Result.Fail("Unknown component type or category.", 400);
        if (type.StatIds.Any(id => World.Stats.All(x => x.Id != id)))
            return Result.Fail("Component type references an unknown stat.", 400);
        if (category.Usable == Usable.Charges && (!World.ChargesStatId.HasValue || !type.StatIds.Contains(World.ChargesStatId.Value) ||
            !World.Stats.Any(x => x.Id == World.ChargesStatId && x.Type == StatKind.Pool)))
            return Result.Fail("A Charges component must have the world's Pool charges stat.", 400);
        var component = new Component { TypeId = typeId, CategoryId = categoryId };
        foreach (var statId in type.StatIds.Distinct())
            component.Stats.Add(statId, new Stat { TypeId = statId });
        return Result.Ok(component);
    }

    public Result<Component> Attach(Component component)
    {
        // reject existing identities and validate all editable data before registration
        if (component == null)
            return Result.Fail("A component is required.", 400);
        if (component.Id != -1 || component.State != ComponentState.Detached || Items.Values.Any(x => ReferenceEquals(x, component)))
            return Result.Fail("Only never-attached Detached components with ID -1 can be attached.", 409);
        try
        {
            if (new ComponentValidator(World).Validate(component).Failed(out var validation))
                return EffectRules.Failure<Component>(validation);
            if (component.Effects.Count != 0)
                return Result.Fail("New components cannot have scheduled effects before attachment.", 400);

            // allocate only after validation; insertion failure leaves the object detached
            var nextId = Math.Max(_nextId, (long)Items.Keys.DefaultIfEmpty(0).Max() + 1);
            if (nextId > int.MaxValue)
                return Result.Fail("Component identity space is exhausted.", 409);
            if (!Items.TryAdd((int)nextId, component))
                return Result.Fail("Component identity is already registered.", 409);
            component.Id = (int)nextId;
            component.State = ComponentState.Active;
            _nextId = nextId + 1;
            return Result.Ok(component);
        }
        catch (Exception exception)
        {
            return EffectRules.Failure<Component>(EffectRules.Exception(exception, "Component attachment failed."));
        }
    }
    private void AdvanceId() => _nextId = Math.Max(_nextId, (long)Items.Keys.DefaultIfEmpty(0).Max() + 1);
    public Result<Component> Get(int componentId) => Items.TryGetValue(componentId, out var component)
        ? Result.Ok(component) : Result.Fail($"Component {componentId} was not found.", 404);
    /// <summary>Gets registered components matching the category ID without including descendant categories.</summary>
    public List<Component> GetByCategory(int categoryId) => Items.Values.Where(x => x.CategoryId == categoryId).ToList();
    /// <summary>Gets registered components with a case-insensitive full-name match.</summary>
    public List<Component> GetByName(string name) => Items.Values.Where(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)).ToList();
    public List<ComponentRelation> GetChildRelations(int componentId) => Relations.Where(x => x.ParentId == componentId).ToList();
    public List<ComponentRelation> GetParentRelations(int componentId) => Relations.Where(x => x.TargetId == componentId).ToList();
    public List<Component> GetChildren(int componentId) => GetChildRelations(componentId).Select(x => x.TargetId).Distinct().Where(Items.ContainsKey).Select(id => Items[id]).ToList();
    public List<Component> GetParents(int componentId) => GetParentRelations(componentId).Select(x => x.ParentId).Distinct().Where(Items.ContainsKey).Select(id => Items[id]).ToList();

    public Result SetStat(int componentId, Stat stat)
    {
        if (Get(componentId).Failed(out var lookup))
            return EffectRules.Failure(lookup);
        var component = lookup.Data!;
        if (component.State != ComponentState.Active || stat == null || !World.ComponentTypes.Any(x => x.Id == component.TypeId && x.StatIds.Contains(stat.TypeId)))
            return Result.Fail("This component cannot have that stat.", 400);
        if (!EffectRules.Finite(stat.Current) || !EffectRules.Finite(stat.Max) || !EffectRules.Finite(stat.ModifiedMax) || !EffectRules.Finite(stat.Bonus) || stat.Max < 0 || stat.ModifiedMax < 0)
            return Result.Fail("Stat values must be finite and maxima non-negative.", 400);
        component.Stats[stat.TypeId] = new Stat
        {
            TypeId = stat.TypeId, Max = stat.Max, ModifiedMax = stat.ModifiedMax, Bonus = stat.Bonus,
            Current = EffectRules.Clamp(stat.Current, stat.ModifiedMax)
        };
        return Result.Ok();
    }

    public Result Delete(int componentId)
    {
        if (Get(componentId).Failed(out var lookup))
            return EffectRules.Failure(lookup);
        if (lookup.Data!.State != ComponentState.Active)
            return Result.Fail("Component is already Deleted.", 409);

        // cascade only to children that lose their last parent
        var deleted = new HashSet<int>();
        var pending = new Queue<int>();
        pending.Enqueue(componentId);
        while (pending.Count > 0)
        {
            var id = pending.Dequeue();
            if (!deleted.Add(id))
                continue;
            foreach (var childId in Relations.Where(x => x.ParentId == id).Select(x => x.TargetId).Distinct())
                if (Relations.Where(x => x.TargetId == childId).All(x => deleted.Contains(x.ParentId)))
                    pending.Enqueue(childId);
        }
        var parents = Relations.Where(x => deleted.Contains(x.TargetId) && !deleted.Contains(x.ParentId)).Select(x => x.ParentId).Distinct().ToArray();
        foreach (var id in deleted)
            if (Items.TryGetValue(id, out var component))
                component.State = ComponentState.Deleted;
        Relations.RemoveAll(x => deleted.Contains(x.TargetId) || deleted.Contains(x.ParentId));
        foreach (var parent in parents)
            if (StatRecalculation.Apply(World, parent).Failed(out var recalculated))
                return recalculated;
        if (Cleanup().Failed(out var cleaned))
            return cleaned;
        return Result.Ok();
    }

    public Result Cleanup()
    {
        // All targets and both action-level and item-level provenance keep origins alive.
        AdvanceId();
        var referenced = new HashSet<int>();
        foreach (var component in Items.Values)
            foreach (var effect in component.Effects)
            {
                var active = effect.Items.Where(x => x.Duration > 0).ToArray();
                if (active.Length == 0)
                    continue;
                referenced.Add(component.Id);
                if (effect.TargetId.HasValue)
                    referenced.Add(effect.TargetId.Value);
                referenced.Add(effect.SourceId);
                referenced.Add(effect.InstrumentId);
                foreach (var item in active)
                {
                    referenced.Add(item.SourceId);
                    referenced.Add(item.InstrumentId);
                }
            }
        foreach (var id in Items.Values.Where(x => x.State == ComponentState.Deleted && !referenced.Contains(x.Id)).Select(x => x.Id).ToArray())
            Items.Remove(id);
        return Result.Ok();
    }
}
