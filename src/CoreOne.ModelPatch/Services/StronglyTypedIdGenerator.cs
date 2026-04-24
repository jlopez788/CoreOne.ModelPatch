namespace CoreOne.ModelPatch.Services;

public class StronglyTypedIdGenerator<T> : IKeyGenerator<T> where T : ICoreId<T>
{
    private static readonly Func<Guid, T> CreateInstance = GenerateFactory();

    /// <summary>
    /// Generates a new version 7 GUID wrapped in a strongly-typed ID
    /// </summary>
    /// <returns>Strongly-typed ID containing a time-ordered GUID</returns>
    public T Create() => CreateInstance(ID.Create().AsGuid());

    private static Func<Guid, T> GenerateFactory()
    {
        var idType = typeof(T);
        var guidType = typeof(Guid);

        // Find the constructor that takes exactly one Guid
        var constructor = idType.GetConstructor([guidType]) ?? throw new InvalidOperationException(
                $"Type {idType.Name} must have a constructor that accepts a single Guid.");

        // Build an expression tree: guid => new TId(guid)
        var parameter = Expression.Parameter(guidType, "guid");
        var constructorCall = Expression.New(constructor, parameter);
        var lambda = Expression.Lambda<Func<Guid, T>>(constructorCall, parameter);
        return lambda.Compile();
    }
}