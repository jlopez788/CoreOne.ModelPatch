namespace CoreOne.ModelPatch;

/// <summary>
///
/// </summary>
public class Delta : Data<string, object>
{
    /// <summary>
    ///
    /// </summary>
    public Delta() : base(MStringComparer.OrdinalIgnoreCase) { }
}

/// <summary>
///
/// </summary>
/// <typeparam name="TModel"></typeparam>
public class Delta<TModel> : Delta where TModel : class, new()
{
}