namespace CoreOne.ModelPatch;

/// <summary>
/// Case-insensitive dictionary that holds partial model data for PATCH operations
/// </summary>
public class Delta : Data<string, object>
{
    /// <summary>
    /// Initializes a new delta with case-insensitive property name matching
    /// </summary>
    public Delta() : base(MStringComparer.OrdinalIgnoreCase) { }
}

/// <summary>
/// Strongly-typed delta for partial entity updates
/// </summary>
/// <typeparam name="TModel">Entity type to patch</typeparam>
public class Delta<TModel> : Delta where TModel : class, new()
{
}