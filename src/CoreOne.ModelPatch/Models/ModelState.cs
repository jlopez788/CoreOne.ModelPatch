using System.Diagnostics;

namespace CoreOne.ModelPatch.Models;

/// <summary>
///
/// </summary>
/// <typeparam name="T"></typeparam>
/// <param name="model"></param>
/// <param name="crudType"></param>
[DebuggerDisplay("Crud: {CrudType}... Type: {ModelType}")]
public class ModelState<T>(T model, CrudType crudType)
{
    /// <summary>
    ///
    /// </summary>
    public CrudType CrudType { get; init; } = crudType;
    /// <summary>
    ///
    /// </summary>
    public T Model { get; init; } = model;
    protected string ModelType { get; init; } = typeof(T).Name;
}

public class ModelState : ModelState<object>
{
    public ModelState(object model, CrudType crudType) : base(model, crudType)
    {
        ModelType = model.GetType().Name;
    }
}