namespace CoreOne.ModelPatch.Services;

/// <summary>
/// Default key generator that produces time-ordered version 7 GUIDs
/// </summary>
public class GuidGenerator : IKeyGenerator
{
    /// <summary>
    /// Generates a new version 7 GUID with timestamp-based ordering
    /// </summary>
    /// <returns>Time-ordered GUID suitable for primary keys</returns>
    public Guid Create() => Guid.CreateVersion7();
}