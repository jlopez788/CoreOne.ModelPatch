namespace CoreOne.ModelPatch.Services;

/// <summary>
///
/// </summary>
public class GuidGenerator : IKeyGenerator
{
    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    public Guid Create() => Guid.CreateVersion7();
}