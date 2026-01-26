namespace CoreOne.ModelPatch.Models;

/// <summary>
/// Result of a patch operation with metadata about affected rows
/// </summary>
/// <typeparam name="T">Type of data returned</typeparam>
public class PatchResult<T> : IResult<T>
{
    /// <summary>
    /// Error or informational message if operation failed or has warnings
    /// </summary>
    public string? Message { get; init; }
    /// <summary>
    /// The data returned from the operation, or null if failed
    /// </summary>
    public T? Model { get; }
    /// <summary>
    /// Whether the operation succeeded, failed, or completed with warnings
    /// </summary>
    public ResultType ResultType { get; init; }
    /// <summary>
    /// Number of database rows affected by the operation
    /// </summary>
    public int Rows { get; init; }
    /// <summary>
    /// Convenience property indicating successful completion
    /// </summary>
    public bool Success => ResultType == ResultType.Success;

    /// <summary>
    /// Creates an empty result (typically for failure scenarios)
    /// </summary>
    public PatchResult()
    { }

    /// <summary>
    /// Creates a result with data and row count
    /// </summary>
    /// <param name="model">Data to return</param>
    /// <param name="rows">Number of affected rows</param>
    public PatchResult(T? model, int rows)
    {
        Model = model;
        Rows = rows;
    }

    /// <summary>
    /// Creates a result from another result, copying status and message
    /// </summary>
    /// <param name="result">Source result to copy from</param>
    public PatchResult(IResult result)
    {
        ResultType = result.ResultType;
        Message = result.Message;
    }
}