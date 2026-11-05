namespace Dexterity;

public interface ITagService
{
    Result<List<string>> GetTags(int componentId);
}
