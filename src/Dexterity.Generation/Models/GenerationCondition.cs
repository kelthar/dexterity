namespace Dexterity.Generation;

public class GenerationCondition
{
    /// <summary>Case-sensitive request-context key; a missing key makes the condition fail, including for inequality.</summary>
    public string Key { get; set; } = "";
    /// <summary>Comparison to apply; equality operators compare ordinal strings, while ordering operators compare numbers.</summary>
    public ConditionOperator Operator { get; set; }
    /// <summary>Comparison operand; numeric operators require finite invariant dot-decimal text without separators or exponents.</summary>
    public string Value { get; set; } = "";
}
