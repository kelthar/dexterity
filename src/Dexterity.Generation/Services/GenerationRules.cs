using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Dexterity.Generation;

internal static partial class GenerationRules
{
    /// <summary>Matches signed integer or dot-decimal text without whitespace, separators or exponents.</summary>
    [GeneratedRegex(@"\A[+-]?[0-9]+(?:\.[0-9]+)?\z", RegexOptions.CultureInvariant)]
    private static partial Regex Number();

    /// <summary>Parses strictly formatted invariant numeric text and rejects non-finite values.</summary>
    internal static bool Numeric(string? text, out double value)
    {
        value = 0;
        return text != null && Number().IsMatch(text) &&
            double.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);
    }

    /// <summary>Copies failure states into a typed result, using HTTP-style code 400 when the source has no code.</summary>
    internal static Result<T> Failure<T>(IResult source) => new()
    {
        Success = false,
        Code = source.Code != 0 ? source.Code : 400,
        States = source.States.ToList()
    };

    /// <summary>Copies failure states into an untyped result, supplying a fallback message and code when absent.</summary>
    internal static Result Failure(IResult source)
    {
        var failure = Result.Fail(source.States.LastOrDefault()?.Text ?? "Generation failed.", source.Code != 0 ? source.Code : 400);
        if (source.States.Count > 0)
            failure.States = source.States.ToList();
        return failure;
    }

    /// <summary>Creates a typed generation failure with a message and status code, defaulting to 400.</summary>
    internal static Result<T> Fail<T>(string message, int code = 400) => Failure<T>(Result.Fail(message, code));

    /// <summary>Writes an unexpected exception to trace output and returns a 500 failure retaining its diagnostic state.</summary>
    internal static Result Exception(Exception exception)
    {
        Trace.TraceError("Component generation failed: {0}", exception);
        return Result.Fail("Component generation failed.", 500).Add(StateType.Error, exception.Message, 500, exception);
    }

    /// <summary>Requires every condition to match request context; missing keys or invalid numeric inputs fail, and equality uses ordinal strings.</summary>
    internal static bool Matches(List<GenerationCondition> conditions, Dictionary<string, string> context)
    {
        foreach (var condition in conditions)
        {
            if (!context.TryGetValue(condition.Key, out var text))
                return false;
            if (condition.Operator == ConditionOperator.Equal)
            {
                if (!string.Equals(text, condition.Value, StringComparison.Ordinal))
                    return false;
                continue;
            }
            if (condition.Operator == ConditionOperator.NotEqual)
            {
                if (string.Equals(text, condition.Value, StringComparison.Ordinal))
                    return false;
                continue;
            }
            if (!Numeric(text, out var value) || !Numeric(condition.Value, out var operand))
                return false;
            var matches = condition.Operator switch
            {
                ConditionOperator.LessThan => value < operand,
                ConditionOperator.LessOrEqual => value <= operand,
                ConditionOperator.GreaterThan => value > operand,
                ConditionOperator.GreaterOrEqual => value >= operand,
                _ => false
            };
            if (!matches)
                return false;
        }
        return true;
    }

    /// <summary>Draws within inclusive bounds and throws if the random provider violates that contract.</summary>
    internal static int Draw(IGenerationRandom random, int min, int max)
    {
        var value = random.Next(min, max);
        if (value < min || value > max)
            throw new InvalidOperationException($"Random provider returned {value} outside {min}..{max}.");
        return value;
    }
}
