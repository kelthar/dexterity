namespace Dexterity.Generation;

public class GenerationService(World world, GenerationCatalog catalog, IGenerationRandom random) : IGenerationService
{
    private readonly GenerationValidator _validator = new(world, catalog);

    /// <inheritdoc />
    public Result Validate()
    {
        try
        {
            if (_validator.CheckCatalog().Failed(out var validation))
                return validation;
            foreach (var template in catalog.Templates)
            {
                if (_validator.Template(template).Failed(out var definition))
                    return GenerationRules.Failure(definition);
            }
            return Result.Ok();
        }
        catch (Exception exception)
        {
            return GenerationRules.Exception(exception);
        }
    }

    /// <inheritdoc />
    public Result<List<GenerationTemplate>> Find(TemplateFilter filter) => Execute(() =>
    {
        if (Validate().Failed(out var validation))
            return GenerationRules.Failure<List<GenerationTemplate>>(validation);
        if (filter == null || !GenerationValidator.Tags(filter.Tags) ||
            (filter.TypeId.HasValue && world.ComponentTypes.All(x => x.Id != filter.TypeId)) ||
            (filter.CategoryId.HasValue && world.Categories.All(x => x.Id != filter.CategoryId)))
            return GenerationRules.Fail<List<GenerationTemplate>>("Filters require valid type/category IDs and tags.");
        var templates = catalog.Templates.Where(x => !filter.TypeId.HasValue || x.TypeId == filter.TypeId)
            .Where(x => !filter.CategoryId.HasValue || x.CategoryId == filter.CategoryId)
            .Where(x => filter.Tags.All(tag => x.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase))).ToList();
        return Result.Ok(templates);
    });

    /// <inheritdoc />
    public Result<GenerationTemplate> Get(string templateId) => Execute(() =>
    {
        if (_validator.Lookup(templateId).Failed(out var selected))
            return GenerationRules.Failure<GenerationTemplate>(selected);
        if (_validator.Template(selected.Data!).Failed(out var definition))
            return GenerationRules.Failure<GenerationTemplate>(definition);
        return selected;
    });

    /// <inheritdoc />
    public Result<GeneratedComponent> Create(GenerationRequest request) => Execute(() => CreateComponent(request));

    /// <summary>Validates and generates a request, then creates and initializes a component.</summary>
    private Result<GeneratedComponent> CreateComponent(GenerationRequest request)
    {
        // validate everything that can be checked before random draws
        if (request == null || request.Context == null || request.Worth < 0 || random == null ||
            request.Context.Any(x => string.IsNullOrWhiteSpace(x.Key) || x.Value == null))
            return GenerationRules.Fail<GeneratedComponent>("A request, non-negative worth, context and random source are required.");
        if (_validator.Lookup(request.TemplateId).Failed(out var selected))
            return GenerationRules.Failure<GeneratedComponent>(selected);
        var template = selected.Data!;
        if (_validator.Template(template).Failed(out var definition))
            return GenerationRules.Failure<GeneratedComponent>(definition);

        // generate once before constructing the detached component
        var source = request.Seed.HasValue ? new DefaultGenerationRandom(request.Seed) : random;
        if (GenerationEngine.Generate(template, request, definition.Data!, source).Failed(out var generated))
            return GenerationRules.Failure<GeneratedComponent>(generated);
        var report = generated.Data!;
        if (world.Repository.CreateDetached(template.TypeId, template.CategoryId).Failed(out var created))
            return GenerationRules.Failure<GeneratedComponent>(created);
        var component = created.Data!;
        if (ComponentBinder.Bind(world, template, report.Values, component).Failed(out var bound))
            return GenerationRules.Failure<GeneratedComponent>(bound);

        // register the prepared object before running world-dependent calculators
        if (world.Repository.Attach(component).Failed(out var attached))
            return GenerationRules.Failure<GeneratedComponent>(attached);
        IEffectService effects = new DefaultEffectService(world);
        if (effects.Recalculate(component.Id).Failed(out var recalculated))
            return GenerationRules.Failure<GeneratedComponent>(recalculated);

        // apply creation-only current defaults after derived maxima are known
        foreach (var pair in component.Stats)
        {
            var stat = pair.Value;
            if (!double.IsFinite(stat.Max) || !double.IsFinite(stat.ModifiedMax) || !double.IsFinite(stat.Bonus) ||
                stat.Max < 0 || stat.ModifiedMax < 0 || !double.IsFinite(stat.Current))
                return GenerationRules.Fail<GeneratedComponent>("Calculated stat values must be finite with non-negative maxima.");
            var kind = world.Stats.First(x => x.Id == pair.Key).Type;
            var initial = bound.Data!.TryGetValue(pair.Key, out var current) ? current : kind == StatKind.Counter ? 0 : stat.ModifiedMax;
            stat.Current = Math.Clamp(initial, 0, stat.ModifiedMax);
        }
        return Result.Ok(new GeneratedComponent { Component = component, Generation = report });
    }

    /// <summary>Runs an operation and converts unexpected exceptions into logged generation failures.</summary>
    private static Result<T> Execute<T>(Func<Result<T>> operation)
    {
        try
        {
            return operation();
        }
        catch (Exception exception)
        {
            return GenerationRules.Failure<T>(GenerationRules.Exception(exception));
        }
    }
}
