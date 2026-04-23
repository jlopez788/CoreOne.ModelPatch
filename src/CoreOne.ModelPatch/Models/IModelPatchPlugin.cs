namespace CoreOne.ModelPatch.Models;

/// <summary>
/// Base interface for all model patch plugins.
/// Plugins are executed in order of their Order property (ascending).
/// </summary>
public interface IModelPatchPlugin
{
    /// <summary>
    /// Execution order in the plugin pipeline (higher values execute first).
    /// Default: 100.
    /// </summary>
    int Order => 100;
}

/// <summary>
/// Plugin hook executed before the delta is applied to the entity.
/// Allows transforming the delta, injecting values, or validating pre-conditions.
/// </summary>
public interface IPrePatchPlugin : IModelPatchPlugin
{
    /// <summary>
    /// Called before the delta is applied to the entity.
    /// </summary>
    /// <param name="context">The plugin context with delta and entity type.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask<IResult> Execute(ModelProcessContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// Plugin hook executed after the delta is applied to the entity.
/// Allows validation, auditing, or side effects.
/// </summary>
public interface IPostPatchPlugin : IModelPatchPlugin
{
    /// <summary>
    /// Called after the delta is applied to the entity and it's ready to be saved to the database.
    /// </summary>
    /// <param name="context">The plugin context with delta, entity type, and current entity state.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask<IResult> Execute(ModelProcessContext context, CancellationToken cancellationToken = default);
}