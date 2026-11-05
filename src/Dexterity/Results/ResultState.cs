namespace Dexterity;

public class ResultState
{
    public string? Text { get; set; }
    public StateType Type { get; set; }
    public int? Code { get; set; }
    public Exception? Exception { get; set; }

    public override string ToString()
    {
        return $"{Type}: {Text}";
    }
}
