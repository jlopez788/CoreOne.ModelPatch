namespace CoreOne.ModelPatch;

/// <summary>
/// Generates primary key values for new entities during patch operations
/// </summary>
public interface IKeyGenerator
{
    /// <summary>
    /// Generates a new unique identifier for an entity
    /// </summary>
    /// <returns>New GUID primary key value</returns>
    Guid Create();
}