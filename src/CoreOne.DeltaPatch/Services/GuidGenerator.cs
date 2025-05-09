using CoreOne.ModelPatch;

namespace CoreOne.ModelPatch.Services;

public class GuidGenerator : IKeyGenerator
{
    public Guid Create() => Guid.NewGuid();
}