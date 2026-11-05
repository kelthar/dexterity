namespace Dexterity.Generation;

public enum GenerationValueKind
{
    /// <summary>Emits the whole number of units affordable from the allocated budget.</summary>
    Weighted = 0,
    /// <summary>Samples inclusive bounds, caps by affordability and honors the minimum even if it requires overspending.</summary>
    Random = 1,
    /// <summary>Emits literal text without participating in budget allocation or worth accounting.</summary>
    Fixed = 2
}
