namespace Dexterity.Generation;

public class StatBinding
{
    /// <summary>Destination stat type, which must be declared by the template's component type.</summary>
    public int StatTypeId { get; set; }
    /// <summary>Generated value key for the non-negative maximum; if absent from the output, this binding is skipped.</summary>
    public string MaxKey { get; set; } = "";
    /// <summary>Optional generated current-value key, required in the output when the maximum is emitted; must be between zero and that maximum.</summary>
    public string? CurrentKey { get; set; }
}
