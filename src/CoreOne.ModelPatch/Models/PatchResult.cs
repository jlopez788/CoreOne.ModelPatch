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
    public T? Model { get; init; }
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
    /// <param name="resultType">Outcome of the operation</param>
    /// <param name="message">Optional status message</param>
    public PatchResult(T? model, int rows, ResultType resultType = ResultType.Success, string? message = null)
    {
        Model = model;
        Rows = rows;
        ResultType = resultType;
        Message = message;
    }

    /// <summary>
    /// Creates a result from another typed result, copying status, message, and model
    /// </summary>
    /// <param name="result">Source result to copy from</param>
    /// <param name="rows">Optional affected row count</param>
    public PatchResult(IResult<T> result, int rows = 0)
    {
        Model = result.Model;
        ResultType = result.ResultType;
        Message = result.Message;
        Rows = rows;
    }

    /// <summary>
    /// Creates a result from a non-generic result, copying status and message
    /// </summary>
    /// <param name="result">Source result to copy from</param>
    /// <param name="model">Optional model to attach to the result</param>
    /// <param name="rows">Optional affected row count</param>
    public PatchResult(IResult result, T? model = default, int rows = 0)
    {
        Model = model;
        ResultType = result.ResultType;
        Message = result.Message;
        Rows = rows;
    }
}

/// <summary>
/// Result of a patch operation with summary helpers for processed entities
/// </summary>
public class PatchResult : IReadOnlyCollectionResult<ModelState>
{
    /// <summary>
    /// Number of entities created during the patch
    /// </summary>
    public int Created => Count(p => p.CrudType == CrudType.Created);
    /// <summary>
    /// Number of entities deleted during the patch
    /// </summary>
    public int Deleted => Count(p => p.CrudType == CrudType.Deleted);
    /// <summary>
    /// Collection of processed entities, or an empty collection when patching failed
    /// </summary>
    public IReadOnlyCollection<ModelState>? Items { get; private set; }
    /// <summary>
    /// Error or informational message if operation failed or has warnings
    /// </summary>
    public string? Message { get; init; }
    /// <summary>
    /// Number of entities read without applying changes during the patch
    /// </summary>
    public int Read => Count(p => p.CrudType == CrudType.Read);
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
    /// Number of entities left unchanged during the patch
    /// </summary>
    public int Unchanged => Read;
    /// <summary>
    /// Number of entities updated during the patch
    /// </summary>
    public int Updated => Count(p => p.CrudType == CrudType.Updated);

    /// <summary>
    /// Creates an empty patch result
    /// </summary>
    public PatchResult()
    { }

    /// <summary>
    /// Creates a patch result with processed items and affected row count
    /// </summary>
    /// <param name="items">Processed entities</param>
    /// <param name="rows">Affected row count</param>
    /// <param name="resultType">Outcome of the operation</param>
    /// <param name="message">Optional status message</param>
    public PatchResult(ProcessedModelCollection? items, int rows, ResultType resultType = ResultType.Success, string? message = null)
    {
        Items = items;
        Rows = rows;
        ResultType = resultType;
        Message = message;
    }

    /// <summary>
    /// Creates a patch result from another result instance
    /// </summary>
    /// <param name="result">Source result to copy from</param>
    /// <param name="rows">Optional affected row count</param>
    public PatchResult(IResult<ProcessedModelCollection> result, int rows = 0)
    {
        Items = result.Model;
        ResultType = result.ResultType;
        Message = result.Message;
        Rows = rows;
    }

    /// <summary>
    /// Counts processed entities matching an optional predicate
    /// </summary>
    /// <param name="predicate">Optional filter for specific item states</param>
    /// <returns>Matching item count</returns>
    public int Count(Predicate<ModelState>? predicate = null)
    {
        return Success && Items?.Count > 0 ?
            predicate is null ?
            Items.Count :
            Items.Count(p => predicate(p)) : 0;
    }

    /// <summary>
    /// Retrieves processed entities of a given CLR type
    /// </summary>
    /// <typeparam name="T">Requested entity type</typeparam>
    /// <param name="predicate">Optional filter for specific item states</param>
    /// <returns>Matching entities</returns>
    public IEnumerable<T> Get<T>(Predicate<ModelState>? predicate = null)
    {
        return Success && Items?.Count > 0 ?
            predicate is null ?
            Items.Select(p => p.Model).OfType<T>() :
            Items.Where(p => predicate(p))
                .Select(p => p.Model)
                .OfType<T>() : [];
    }
}