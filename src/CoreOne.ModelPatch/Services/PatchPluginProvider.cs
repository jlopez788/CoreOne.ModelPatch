using System.Runtime.CompilerServices;

namespace CoreOne.ModelPatch.Services;

/// <summary>
/// Manages the plugin pipeline for model patching.
/// Collects plugins from DI and executes them in order during patch operations.
/// </summary>
public class PatchPluginProvider(IEnumerable<IPrePatchPlugin> prePatchPlugins, IEnumerable<IPostPatchPlugin> postPatchPlugins, ILogger<PatchPluginProvider> logger)
{
    private readonly ICollection<IPostPatchPlugin> _postPatchPlugins = [.. postPatchPlugins.OrderByDescending(p => p.Order)];
    private readonly ICollection<IPrePatchPlugin> _prePatchPlugins = [.. prePatchPlugins.OrderByDescending(p => p.Order)];
    /// <summary>
    /// Returns whether any plugins are registered.
    /// </summary>
    public bool HasPlugins => _prePatchPlugins.Count != 0 || _postPatchPlugins.Count != 0;

    /// <summary>
    /// Executes all registered post-patch plugins.
    /// </summary>
    public ValueTask<IResult> ExecutePostPatchPluginsAsync(ModelProcessContext context, CancellationToken cancellationToken = default)
    {
        return ExecutePlugins(_postPatchPlugins, context.Type.Name, p => p.Execute(context, cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Executes all registered pre-patch plugins.
    /// </summary>
    public ValueTask<IResult> ExecutePrePatchPluginsAsync(ModelProcessContext context, CancellationToken cancellationToken = default)
    {
        return ExecutePlugins(_prePatchPlugins, context.Type.Name, p => p.Execute(context, cancellationToken), cancellationToken);
    }

    private async ValueTask<IResult> ExecutePlugins<T>(IEnumerable<T> plugins, string entityName, Func<T, ValueTask<IResult>> callback, CancellationToken cancellationToken, [CallerMemberName] string? name = null) where T : notnull
    {
        return await plugins.AggregateResultAsync(Result.Ok, async (next, plugin) => await processPlugin(plugin));

        async ValueTask<IResult> processPlugin(T plugin)
        {
            try
            {
                if (cancellationToken.IsCancellationRequested)
                    return Result.Fail("Task cancellation");

                logger.LogDebug("Executing {name} plugin: {PluginType}", name, plugin.GetType().Name);
                var result = await callback.Invoke(plugin).ConfigureAwait(false);
                if (!result.Success)
                {
                    logger.LogWarning("{name} plugin {PluginType} failed for entity {EntityType}: {Message}", name, plugin.GetType().Name, entityName, result.Message);
                    return result;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "{name} plugin {PluginType} failed for entity {EntityType}", name, plugin.GetType().Name, entityName);
                return Result.FromException(ex);
            }

            return Result.Ok;
        }
    }
}