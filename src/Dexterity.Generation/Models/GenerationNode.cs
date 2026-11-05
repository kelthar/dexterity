namespace Dexterity.Generation;

public class GenerationNode
{
    /// <summary>Child groups and values, sorted by order with list order breaking ties; instances cannot be reused or form cycles.</summary>
    public List<IGenerationEntry> Items { get; set; } = new();

    /// <summary>Appends and returns a group that distributes its budget share among child entries.</summary>
    public GenerationGroup AddGroup(string id, int weight = 1, int varyWeightBy = 0, int chanceToGet = 0)
    {
        var group = new GenerationGroup { Id = id, Weight = weight, VaryWeightBy = varyWeightBy, ChanceToGet = chanceToGet };
        Items.Add(group);
        return group;
    }

    /// <summary>Appends and returns a numeric value calculated by dividing its weighted budget share by its unit cost.</summary>
    public GenerationValue AddWeightValue(string id, int weight, int worthPerValue = 1, int chanceToGet = 0, int varyWeightBy = 0)
    {
        var value = new GenerationValue { Id = id, Weight = weight, WorthPerValue = worthPerValue, ChanceToGet = chanceToGet, VaryWeightBy = varyWeightBy };
        Items.Add(value);
        return value;
    }

    /// <summary>Appends and returns a random numeric value with inclusive bounds; its minimum can exceed the allocated budget.</summary>
    public GenerationValue AddRandomValue(string id, int min, int max, int worthPerValue = 1, int chanceToGet = 0, int weight = 1, int varyWeightBy = 0)
    {
        var value = new GenerationValue
        {
            Id = id, Kind = GenerationValueKind.Random, MinValue = min, MaxValue = max,
            WorthPerValue = worthPerValue, ChanceToGet = chanceToGet, Weight = weight, VaryWeightBy = varyWeightBy
        };
        Items.Add(value);
        return value;
    }

    /// <summary>Appends and returns a literal text value that consumes no budget.</summary>
    public GenerationValue AddFixedValue(string id, string value, int chanceToGet = 0)
    {
        var entry = new GenerationValue { Id = id, Kind = GenerationValueKind.Fixed, Value = value, ChanceToGet = chanceToGet, WorthPerValue = 0 };
        Items.Add(entry);
        return entry;
    }
}
