namespace Dexterity.Generation.Tests;

/// <summary>Each built-in parameter independently varies through all three generation strategies.</summary>
public sealed class FluentModifierCases : TheoryData<string, string, int>
{
    /// <summary>Enumerates MOD-01–14 strategy cases without sharing worlds or random providers.</summary>
    public FluentModifierCases()
    {
        foreach (var type in FluentTestSupport.Types)
            foreach (var key in FluentTestSupport.Keys(type))
                for (var strategy = 0; strategy < 3; strategy++)
                    Add(type, key, strategy);
    }
}
