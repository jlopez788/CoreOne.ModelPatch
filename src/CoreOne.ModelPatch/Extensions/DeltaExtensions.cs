namespace CoreOne.ModelPatch.Extensions;

/// <summary>
/// Extension methods for converting models to deltas
/// </summary>
public static class DeltaExtensions
{
    /// <summary>
    /// Converts an entity instance into a delta for partial updates
    /// </summary>
    /// <typeparam name="TModel">Entity type</typeparam>
    /// <param name="model">Entity instance to convert</param>
    /// <returns>Delta containing all non-null properties from the model</returns>
    public static Delta<TModel> ToDelta<TModel>(this TModel? model) where TModel : class, new()
    {
        return model is null ? [] : Process<TModel, Delta<TModel>>(model, []);
    }

    /// <summary>
    /// Converts multiple entity instances into a delta collection for batch operations
    /// </summary>
    /// <typeparam name="TModel">Entity type</typeparam>
    /// <param name="models">Collection of entities to convert</param>
    /// <returns>Delta collection ready for batch patching</returns>
    public static DeltaCollection<TModel> ToDeltaCollection<TModel>(this IEnumerable<TModel?>? models) where TModel : class, new()
    {
        var next = models?.ExcludeNulls() ?? [];
        return Process<IEnumerable<TModel>, DeltaCollection<TModel>>(next, []);
    }

    private static TDelta Process<TModel, TDelta>(TModel model, TDelta defaultValue) => Utility.DeserializeObject<TDelta>(Utility.Serialize(model)) ?? defaultValue;
}