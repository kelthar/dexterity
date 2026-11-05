namespace Dexterity;

public class EffectResult
{
    public List<EffectResultItem> Items { get; set; } = new();
    public int? TargetId { get; set; }
    public int SourceId { get; set; }
    public int InstrumentId { get; set; }

    public double Sum(int effectTypeId) => Items.Where(x => x.Duration == 0 && x.EffectTypeId == effectTypeId).Sum(x => x.Amount);
    public double SumStat(int statId, World world)
    {
        var types = new HashSet<int>(world.EffectTypes.Where(x => x.StatTypeId == statId).Select(x => x.Id));
        return Items.Where(x => x.Duration == 0 && types.Contains(x.EffectTypeId)).Sum(x => x.Amount);
    }
}
