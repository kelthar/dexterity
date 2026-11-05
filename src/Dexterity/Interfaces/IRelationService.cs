namespace Dexterity;

public interface IRelationService
{
    Result Add(int parentId, int targetId);
    Result Equip(int parentId, int targetId, params int[] slotIds);
    Result Unequip(int parentId, int targetId);
    Result Connect(int parentId, int targetId);
    Result Remove(int parentId, int targetId);
}
