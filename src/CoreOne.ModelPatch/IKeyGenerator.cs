namespace CoreOne.ModelPatch;

public interface IKeyGenerator
{
    /// <summary>
    /// Generates a new unique identifier for an entity
    /// </summary>
    /// <returns>Create primary key value</returns>
    KeyModel Create();
}

public interface IKeyGenerator<TKey> : IKeyGenerator
{
}