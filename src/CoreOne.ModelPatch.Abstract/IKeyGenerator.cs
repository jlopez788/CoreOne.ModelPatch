namespace CoreOne.ModelPatch.Abstract;

public interface IKeyGenerator
{
    KeyModel CreateKey();
}

public interface IKeyGenerator<TKey> : IKeyGenerator where TKey : notnull
{
    /// <summary>
    /// Generates a new strongly-typed unique identifier
    /// </summary>
    /// <returns>New key value of type <typeparamref name="TKey"/></returns>
    TKey Create();
}