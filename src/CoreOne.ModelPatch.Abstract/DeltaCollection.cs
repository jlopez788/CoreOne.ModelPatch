namespace CoreOne.ModelPatch.Abstract;

/// <summary>
/// Collection of deltas for batch patching multiple entities of the same type
/// </summary>
/// <typeparam name="TModel">Entity type to patch</typeparam>
public class DeltaCollection<TModel> : List<Delta<TModel>> where TModel : class, new()
{ }