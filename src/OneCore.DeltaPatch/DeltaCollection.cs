namespace OneCore.ModelPatch;

public class DeltaCollection<TModel> : List<Delta<TModel>> where TModel : class, new()
{ }