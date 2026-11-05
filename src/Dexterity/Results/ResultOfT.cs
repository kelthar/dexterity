namespace Dexterity;

public class Result<T> : ResultBase
{
    public T? Data { get; set; }

    public bool Failed(out Result<T> result, out T? val)
    {
        val = Success ? Data : default(T);
        result = this;
        return !Success;
    }

    public bool Failed(out Result<T> result)
    {
        result = this;
        return !Success;
    }

    public Result<T> SetFailed()
    {
        Success = false;
        return this;
    }

    public Result<T> SetSuccess()
    {
        Success = true;
        return this;
    }

    public Result<T> Add(StateType type, string? log = null, int? code = null, Exception? ex = null)
    {
        AddState(type, log, code, ex);
        return this;
    }
    public Result<T> Merge(IResult result)
    {
        foreach (var state in result.States)
            States.Add(state);

        return this;
    }

    public Task<Result<T>> Task() => System.Threading.Tasks.Task.FromResult(this);

    #region messages
    public Result<T> Error(string? log, int? code = null, Exception? ex = null) => Add(StateType.Error, log, code, ex);
    public Result<T> Info(string? log, int? code = null, Exception? ex = null) => Add(StateType.Info, log, code, ex);
    #endregion //messages

    #region states
    // public static Result<T> Fail() => new Result<T>() { Success = false };
    // public static Result<T> Fail(string log, int? code = null) => (new Result<T> { Success = false }).Error(log);
    // public static Result<T> Ok() => new() { Success = true };
    public static Result<T> Ok(T data) => new() { Success = true, Data = data };
    public static Result<T> If(bool state, T data) => state ? new Result<T> { Success = true, Data = data } : new Result<T> { Success = false };
    #endregion //states

    #region implicits
    /// <summary>
    /// Automatically map enum that represents an error code to Result
    /// </summary>
    /// <param name="error">Error enum</param>
    /// <returns></returns>
    public static implicit operator Result<T>(Enum error) => new() { Success = false, Code = error.ParseResultEnum(-1) };
    public static implicit operator Result<T>(T data) => Ok(data);
    public static implicit operator Result<T>(Result source) => new()
    {
        States = source.States,
        Success = source.Success
    };
    public static implicit operator bool(Result<T> result) => result.Success;
    #endregion //implicits

    #region conversion
    public Result<T1> As<T1>() => new Result<T1>() { Success = Success };
    /// <summary>
    /// Pass an mapping function that converts T to TMap
    /// </summary>
    /// <param name="mapper">Mapping function</param>
    /// <returns></returns>
    public TMap Map<TMap>(Func<T, TMap> mapper) => mapper(Data);
    #endregion //conversions
}
