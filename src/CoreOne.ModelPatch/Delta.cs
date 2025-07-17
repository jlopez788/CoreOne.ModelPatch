namespace CoreOne.ModelPatch;

public class Delta : Data<string, object>
{
    public Delta() : base(MStringComparer.OrdinalIgnoreCase) { }
}

public class Delta<TModel> : Delta where TModel : class, new()
{
}