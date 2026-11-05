namespace Dexterity;

public interface IResult
{
    public bool Failed();
    bool Success { get; set; }
    int Code { get; set; }
    List<ResultState> States { get; set; }
}
