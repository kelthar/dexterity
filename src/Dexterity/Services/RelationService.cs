namespace Dexterity;

public class RelationService : IRelationService
{
    private readonly World _world;
    private readonly IEffectService _effects;
    private long _nextId = 1;
    public RelationService(World world) { _world = world; _effects = new DefaultEffectService(world); }

    public Result Add(int parentId, int targetId) => Attach(parentId, targetId, RelationType.Inventory);
    public Result Connect(int parentId, int targetId) => Attach(parentId, targetId, RelationType.Connected);

    private Result Endpoints(int parentId, int targetId)
    {
        if (_world.Repository.Get(parentId).Failed(out var parent))
            return EffectRules.Failure(parent);
        if (_world.Repository.Get(targetId).Failed(out var target))
            return EffectRules.Failure(target);
        if (parent.Data!.State != ComponentState.Active || target.Data!.State != ComponentState.Active)
            return Result.Fail("Gameplay relations require Active components.", 409);
        return Result.Ok();
    }

    private Result Attach(int parentId, int targetId, RelationType kind)
    {
        // validate all constraints before changing the graph
        if (Endpoints(parentId, targetId).Failed(out var endpoints))
            return endpoints;
        var repo = _world.Repository;
        var target = repo.Items[targetId];
        var type = _world.ComponentTypes.FirstOrDefault(x => x.Id == target.TypeId);
        if (type == null)
            return Result.Fail("Unknown component type.", 400);
        if (repo.Relations.Any(x => x.ParentId == parentId && x.TargetId == targetId))
            return Result.Fail("A relation already exists between these components.", 409);
        if (!type.MultipleParentComponents && repo.Relations.Any(x => x.TargetId == targetId))
            return Result.Fail("Component already has a parent.", 409);
        var seen = new HashSet<int>();
        var pending = new Stack<int>();
        pending.Push(targetId);
        while (pending.Count > 0)
        {
            var id = pending.Pop();
            if (id == parentId)
                return Result.Fail("Relation would create a component cycle.", 409);
            if (!seen.Add(id))
                continue;
            foreach (var child in repo.Relations.Where(x => x.ParentId == id))
                pending.Push(child.TargetId);
        }
        // Shared skill levels are grouped by name, independently of category.
        if (kind == RelationType.Connected && type.MultipleParentComponents && repo.Relations
            .Where(x => x.ParentId == parentId && x.Type == RelationType.Connected)
            .Any(x => repo.Items.TryGetValue(x.TargetId, out var child) && child.TypeId == target.TypeId && string.Equals(child.Name, target.Name, StringComparison.OrdinalIgnoreCase)))
            return Result.Fail($"A level of '{target.Name}' is already connected.", 409);
        _nextId = Math.Max(_nextId, (long)repo.Relations.Select(x => x.Id).DefaultIfEmpty(0).Max() + 1);
        if (_nextId > int.MaxValue)
            return Result.Fail("Relation identity space is exhausted.", 409);
        repo.Relations.Add(new ComponentRelation { Id = (int)_nextId++, ParentId = parentId, TargetId = targetId, Type = kind });
        if (_effects.Recalculate(parentId).Failed(out var recalculated))
            return recalculated;
        return Result.Ok();
    }

    public Result Equip(int parentId, int targetId, params int[] slotIds)
    {
        if (Endpoints(parentId, targetId).Failed(out var endpoints))
            return endpoints;
        var repo = _world.Repository;
        var relation = repo.Relations.FirstOrDefault(x => x.ParentId == parentId && x.TargetId == targetId);
        if (relation == null || relation.Type != RelationType.Inventory)
            return Result.Fail("Only an item in this parent's inventory can be equipped.", 409);
        var target = repo.Items[targetId];
        var category = _world.Categories.FirstOrDefault(x => x.Id == target.CategoryId);
        if (category == null || !category.Equipable)
            return Result.Fail("Component category is not equipable.", 400);
        if (slotIds == null || slotIds.Length == 0 || slotIds.Distinct().Count() != slotIds.Length)
            return Result.Fail("Select at least one distinct equipment slot.", 400);
        foreach (var id in slotIds)
        {
            var slot = _world.Slots.FirstOrDefault(x => x.Id == id);
            if (slot == null)
                return Result.Fail($"Unknown equipment slot {id}.", 400);
            if (slot.Categories != null && slot.Categories.Count > 0 && !slot.Categories.Any(ancestor => EffectRules.Descends(_world, target.CategoryId, ancestor)))
                return Result.Fail($"Component category does not match equipment slot {slot.Name}.", 400);
        }

        // Displace whole relations, including other slots of a multi-slot item.
        foreach (var occupied in repo.Relations.Where(x => x.ParentId == parentId && x.Type == RelationType.Equipped && x.SlotIds.Intersect(slotIds).Any()))
        {
            occupied.Type = RelationType.Inventory;
            occupied.SlotIds.Clear();
        }
        relation.Type = RelationType.Equipped;
        relation.SlotIds.AddRange(slotIds);
        if (_effects.Recalculate(parentId).Failed(out var recalculated))
            return recalculated;
        return Result.Ok();
    }

    public Result Unequip(int parentId, int targetId)
    {
        if (Endpoints(parentId, targetId).Failed(out var endpoints))
            return endpoints;
        var relation = _world.Repository.Relations.FirstOrDefault(x => x.ParentId == parentId && x.TargetId == targetId);
        if (relation == null || relation.Type != RelationType.Equipped)
            return Result.Fail("Component is not equipped by this parent.", 409);
        relation.Type = RelationType.Inventory;
        relation.SlotIds.Clear();
        if (_effects.Recalculate(parentId).Failed(out var recalculated))
            return recalculated;
        return Result.Ok();
    }

    public Result Remove(int parentId, int targetId)
    {
        if (Endpoints(parentId, targetId).Failed(out var endpoints))
            return endpoints;
        var relation = _world.Repository.Relations.FirstOrDefault(x => x.ParentId == parentId && x.TargetId == targetId);
        if (relation == null)
            return Result.Fail("Component relation was not found.", 404);
        _world.Repository.Relations.Remove(relation);
        if (_effects.Recalculate(parentId).Failed(out var recalculated))
            return recalculated;
        return Result.Ok();
    }
}
