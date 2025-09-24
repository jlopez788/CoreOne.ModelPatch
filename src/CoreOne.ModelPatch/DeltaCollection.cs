namespace CoreOne.ModelPatch;

/// <summary>
/// 
/// </summary>
/// <typeparam name="TModel"></typeparam>
public class DeltaCollection<TModel> : List<Delta<TModel>> where TModel : class, new()
{ }