namespace Dexterity;

public class TagService : ITagService
{
    private readonly World _world;
    public TagService(World world) => _world = world;

    public Result<List<string>> GetTags(int componentId)
    {
        if (_world.Repository.Get(componentId).Failed(out var lookup))
            return lookup.As<List<string>>();
        var component = lookup.Data!;
        var type = _world.ComponentTypes.FirstOrDefault(x => x.Id == component.TypeId);
        if (type == null)
            return Result.Fail($"Unknown component type {component.TypeId}.", 400);

        // compose dynamically; preserve first occurrence and ignore case
        var tags = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Add(IEnumerable<string> values)
        {
            foreach (var value in values)
                if (seen.Add(value))
                    tags.Add(value);
        }
        Add(component.Tags);
        Add(type.Tags);
        int? categoryId = component.CategoryId;
        var visited = new HashSet<int>();
        while (categoryId.HasValue)
        {
            if (!visited.Add(categoryId.Value))
                return Result.Fail("Category cycle while composing tags.", 400);
            var category = _world.Categories.FirstOrDefault(x => x.Id == categoryId);
            if (category == null)
                return Result.Fail($"Unknown category {categoryId}.", 400);
            Add(category.Tags);
            categoryId = category.ParentCategoryId;
        }
        return Result.Ok(tags);
    }
}
