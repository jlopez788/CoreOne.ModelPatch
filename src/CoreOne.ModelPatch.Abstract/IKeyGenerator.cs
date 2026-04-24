namespace CoreOne.ModelPatch.Abstract;

public interface IKeyGenerator
{
    /// <summary>
    /// Generates a new unique identifier for an entity
    /// </summary>
    /// <returns>Create primary key value</returns>
    KeyModel Create();
}

public interface IKeyGenerator<TKey> : IKeyGenerator where TKey : notnull
{
    /// <summary>
    /// Generates a new strongly-typed unique identifier
    /// </summary>
    /// <returns>New key value of type <typeparamref name="TKey"/></returns>
    new TKey Create();

    /// <summary>
    /// Bridges the untyped interface to the typed implementation
    /// </summary>
    KeyModel IKeyGenerator.Create() => KeyModel.Create(Create()!);
}