using System.Diagnostics;

namespace CoreOne.ModelPatch.Abstract.Models;

/// <summary>
/// Represents a processed entity with information about what operation was performed
/// </summary>
/// <typeparam name="T">Entity type</typeparam>
/// <param name="model">The processed entity instance</param>
/// <param name="crudType">Operation performed on the entity</param>
[DebuggerDisplay("Crud: {CrudType}... Type: {ModelType}")]
public class ModelState<T>(T model, CrudType crudType)
{
    /// <summary>
    /// Operation performed during patch processing (Created, Updated, etc.)
    /// </summary>
    public CrudType CrudType { get; init; } = crudType;
    /// <summary>
    /// The entity instance after patching
    /// </summary>
    public T Model { get; init; } = model;
    /// <summary>
    /// Human-readable type name for debugging
    /// </summary>
    protected string ModelType { get; init; } = typeof(T).Name;
}

/// <summary>
/// Non-generic model state for heterogeneous collections
/// </summary>
public class ModelState : ModelState<object>
{
    /// <summary>
    /// Creates a model state with runtime type inference
    /// </summary>
    /// <param name="model">The processed entity</param>
    /// <param name="crudType">Operation performed</param>
    public ModelState(object model, CrudType crudType) : base(model, crudType)
    {
        ModelType = model.GetType().Name;
    }
}