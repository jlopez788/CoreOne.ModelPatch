namespace CoreOne.ModelPatch.Services;

/// <summary>
/// Default key generator that produces time-ordered version 7 GUIDs
/// </summary>
public class GuidGenerator : IKeyGenerator<Guid>
{
    public Guid Create() => Guid.CreateVersion7();
}