namespace Dexterity;

public static class ResultExtentions
{
    public static int ParseResultEnum(this Enum source, int fallback)
    {
        try
        {
            return (int)Convert.ChangeType(source, typeof(int));
        }
        catch (Exception ex)
        {
            return fallback;
        }

    }
}
