using CoreOne.ModelPatch.Abstract.Attributes;

namespace CoreOne.ModelPatch.Plugins;

/// <summary>
/// Runs model-level validation after patching and before persistence.
/// </summary>
public class ModelStateValidationPlugin(IServiceProvider services) : IPostPatchPlugin
{
    public int Order => 1000;

    public ValueTask<IResult> Execute(ModelProcessContext context, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult<IResult>(context.Model.ValidateModel(services, true));
    }
}