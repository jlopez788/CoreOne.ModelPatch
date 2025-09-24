namespace CoreOne.ModelPatch.Models;

/// <summary>
///
/// </summary>
/// <typeparam name="T"></typeparam>
public class PatchResult<T> : IResult<T>
{
    /// <summary>
    ///
    /// </summary>
    public string? Message { get; init; }
    /// <summary>
    ///
    /// </summary>
    public T? Model { get; }
    /// <summary>
    ///
    /// </summary>
    public ResultType ResultType { get; init; }
    /// <summary>
    ///
    /// </summary>
    public int Rows { get; init; }
    /// <summary>
    ///
    /// </summary>
    public bool Success => ResultType == ResultType.Success;

    /// <summary>
    ///
    /// </summary>
    public PatchResult()
    { }

    /// <summary>
    ///
    /// </summary>
    /// <param name="model"></param>
    /// <param name="rows"></param>
    public PatchResult(T? model, int rows)
    {
        Model = model;
        Rows = rows;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="result"></param>
    public PatchResult(IResult result)
    {
        ResultType = result.ResultType;
        Message = result.Message;
    }
}