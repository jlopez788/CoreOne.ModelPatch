using CoreOne.ModelPatch.Services;
using CoreOne.ModelPatch.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace CoreOne.ModelPatch.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers CoreOne.ModelPatch services using the Microsoft options pattern.
    /// </summary>
    /// <param name="services">Service collection to configure</param>
    /// <param name="configure">Optional options callback</param>
    /// <returns>The same service collection for chaining</returns>
    public static IServiceCollection AddModelPatch(this IServiceCollection services, Action<ModelOptions>? configure = null)
    {
        services.AddOptions<ModelOptions>();
        if (configure is not null)
            services.Configure(configure);

        // Keep concrete ModelOptions injection working for existing consumers.
        services.TryAddSingleton(p => p.GetRequiredService<IOptions<ModelOptions>>().Value);
        services.TryAddScoped(typeof(IDataModelService<>), typeof(DataModelService<>));
        services.TryAddScoped(typeof(IKeyGenerator<Guid>), typeof(GuidGenerator));
        services.TryAddScoped(typeof(IKeyGenerator<>), typeof(StronglyTypedIdGenerator<>));
        services.TryAddScoped<PatchPluginProvider>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPrePatchPlugin, StrictPropertyValidationPlugin>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPrePatchPlugin, ConcurrencyTokenValidationPlugin>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPostPatchPlugin, ModelStateValidationPlugin>());
        return services;
    }
}