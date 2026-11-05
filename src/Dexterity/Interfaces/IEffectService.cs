namespace Dexterity;

public interface IEffectService
{
    Result<List<EffectResult>> Attack(int sourceId, int instrumentId, int primaryTargetId, int[]? secondaryTargetIds = null);
    Result<List<EffectResult>> Use(int sourceId, int instrumentId, int primaryTargetId, int[]? secondaryTargetIds = null);
    Result<List<EffectResult>> Outgoing(int sourceId, int instrumentId, int primaryTargetId, int[]? secondaryTargetIds = null);
    Result<List<EffectResult>> Incoming(int targetId, EffectResult result);
    Result<List<EffectResult>> Tick(int targetId, double elapsed);
    Result Apply(int targetId, EffectResult result);
    Result Recalculate(int componentId);
    Result<bool> CanConsume(int instrumentId);
    Result Consume(int instrumentId);
}
