namespace CoreOne.ModelPatch.Extensions;

/// <summary>
/// Extension methods for querying and filtering processed model collections
/// </summary>
public static class ProcessedModelExtensions
{
    /// <summary>
    /// Counts entities in the result that match an optional predicate
    /// </summary>
    /// <param name="result">Patch operation result</param>
    /// <param name="predicate">Optional filter for counting specific entities (e.g., only Created)</param>
    /// <returns>Number of matching entities, or 0 if operation failed</returns>
    public static int Count(this IResult<ProcessedModelCollection> result, Predicate<ModelState>? predicate = null)
    {
        return result.Success && result.Model?.Count > 0 ?
            predicate is null ?
            result.Model.Count :
            result.Model.Count(p => predicate(p)) : 0;
    }

    /// <summary>
    /// Extracts entities of a specific type from the result
    /// </summary>
    /// <typeparam name="T">Entity type to extract</typeparam>
    /// <param name="result">Patch operation result</param>
    /// <param name="predicate">Optional filter for specific entities (e.g., only Updated)</param>
    /// <returns>Filtered collection of entities matching the type and predicate</returns>
    public static IEnumerable<T> OfType<T>(this IResult<ProcessedModelCollection> result, Predicate<ModelState>? predicate = null)
    {
        return result.Success && result.Model?.Count > 0 ?
            predicate is null ?
            result.Model.Select(p => p.Model).OfType<T>() :
            result.Model.Where(p => predicate(p))
                .Select(p => p.Model)
                .OfType<T>() : [];
    }
}