namespace Dexterity.Generation;

public interface IGenerationService
{
    /// <summary>Validates the world configuration, catalog and every template, including branches that conditions would skip, without consuming randomness.</summary>
    Result Validate();
    /// <summary>Validates the full catalog and returns template references matching the optional type/category and all case-insensitive tags.</summary>
    Result<List<GenerationTemplate>> Find(TemplateFilter filter);
    /// <summary>Looks up an ordinal, case-sensitive template ID and validates the selected definition before returning its catalog reference.</summary>
    Result<GenerationTemplate> Get(string templateId);
    /// <summary>Generates values, populates a detached component, attaches it and initializes its recalculated stats.</summary>
    /// <remarks>A request seed uses a private random source. Failed creation retains repository changes already made and consumed random draws.</remarks>
    Result<GeneratedComponent> Create(GenerationRequest request);
}
