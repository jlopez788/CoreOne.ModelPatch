namespace OneCore.ModelPatch.Services;

public class GuidGenerator : IKeyGenerator
{
    public Guid Create() => Guid.NewGuid();
}