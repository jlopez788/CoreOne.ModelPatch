namespace OneCore.ModelPatch.Services;

public class IdGuidGenerator : IKeyGenerator
{
    public Guid Create() => ID.Create();
}