namespace Dexterity;

public class Result : ResultBase
{
    public Result Add(StateType type, string? log = null, int? code = null, Exception? ex = null)
    {
        AddState(type, log, code, ex);
        return this;
    }

    public bool Failed(out Result result)
    {
        result = this;
        return !Success;
    }

    public Result(bool success = false)
    {
        Success = success;
    }

    public Result()
    {

    }

    public Result SetFail()
    {
        Success = false;
        return this;
    }

    public Result SetSuccess()
    {
        Success = true;
        return this;
    }

    public static Task<Result<T>> Task<T>(T data) => System.Threading.Tasks.Task.FromResult(Result<T>.Ok(data));

    #region messages
    public Result Error(string? log, int? code = null, Exception? ex = null) => Add(StateType.Error, log, code, ex);
    public Result Info(string? log, int? code = null, Exception? ex = null) => Add(StateType.Info, log, code, ex);
    public Result Merge(IResult result)
    {
        States.AddRange(result.States);

        return this;
    }
    #endregion //messages

    #region states
    public static Result Fail() => new() { Success = false };
    public static Result Fail(string error, int? code = null) => (new Result { Success = false }).Error(error, code);
    public static Result Ok() => new() { Success = true };
    public static Result<T> Ok<T>(T data) => Result<T>.Ok(data);
    public static Result<T> If<T>(bool state, T data) => state ? new Result<T> { Success = true, Data = data } : new Result<T> { Success = false };
    public static Result If(bool state) => new Result { Success = state };
    #endregion states

    #region operators
    public static implicit operator Result(Enum error) => new() { Success = false, Code = error.ParseResultEnum(-1) };
    public static implicit operator bool(Result result) => result.Success;
    #endregion
}
