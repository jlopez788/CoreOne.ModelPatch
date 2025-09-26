namespace CoreOne.ModelPatch.Extensions;

/// <summary>
///
/// </summary>
public static class DeltaExtensions
{
    /// <summary>
    /// Converts a model to delta
    /// </summary>
    /// <typeparam name="TModel"></typeparam>
    /// <param name="model"></param>
    /// <returns></returns>
    public static Delta<TModel> ToDelta<TModel>(this TModel? model) where TModel : class, new()
    {
        return model is null ? [] : Process<TModel, Delta<TModel>>(model, []);
    }

    /// <summary>
    ///
    /// </summary>
    /// <typeparam name="TModel"></typeparam>
    /// <param name="models"></param>
    /// <returns></returns>
    public static DeltaCollection<TModel> ToDeltaCollection<TModel>(this IEnumerable<TModel?>? models) where TModel : class, new()
    {
        var next = models?.ExcludeNulls() ?? [];
        return Process<IEnumerable<TModel>, DeltaCollection<TModel>>(next, []);
    }

    private static TDelta Process<TModel, TDelta>(TModel model, TDelta defaultValue) => Utility.DeserializeObject<TDelta>(Utility.Serialize(model)) ?? defaultValue;
}