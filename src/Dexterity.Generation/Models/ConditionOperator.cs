namespace Dexterity.Generation;

public enum ConditionOperator
{
    /// <summary>Requires ordinal, case-sensitive string equality.</summary>
    Equal = 0,
    /// <summary>Requires an existing context value that is not ordinally equal to the operand.</summary>
    NotEqual = 1,
    /// <summary>Requires the numeric context value to be less than the numeric operand.</summary>
    LessThan = 2,
    /// <summary>Requires the numeric context value to be less than or equal to the numeric operand.</summary>
    LessOrEqual = 3,
    /// <summary>Requires the numeric context value to be greater than the numeric operand.</summary>
    GreaterThan = 4,
    /// <summary>Requires the numeric context value to be greater than or equal to the numeric operand.</summary>
    GreaterOrEqual = 5
}
