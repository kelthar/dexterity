namespace Dexterity;

public interface IWorldLogic
{
    double GetDistance(int firstId, int secondId);
    RelationKind GetRelationKind(int firstId, int secondId);
}
