namespace CoreOne.ModelPatch.Services;

public class IdGuidGenerator : IKeyGenerator
{
    public Guid Create() => Guid.CreateVersion7();
}