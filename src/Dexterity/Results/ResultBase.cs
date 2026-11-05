namespace Dexterity;

public class ResultBase : IResult
{
    public bool Failed() => !Success;
    public bool Success { get; set; }
    public int Code { get; set; }
    public List<ResultState> States { get; set; } = [];

    public void AddState(StateType type, string? log = null, int? code = null, Exception? ex = null)
    {
        if (code.HasValue)
            Code = code.Value;

        States.Add(new ResultState() { Type = type, Text = log, Code = code ?? 0, Exception = ex });
    }
}
