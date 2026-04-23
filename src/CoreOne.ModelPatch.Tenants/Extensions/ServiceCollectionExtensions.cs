using CoreOne.Identity.Contracts;
using CoreOne.ModelPatch.Tenants.Plugins;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CoreOne.ModelPatch.Tenants;

/// <summary>
/// Extension methods for registering tenant support in CoreOne.ModelPatch.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers tenant plugin support with the default HttpContext-based tenant provider.
    /// </summary>
    /// <param name="services">Service collection to configure</param>
    /// <param name="configure">Optional configuration callback for tenant options</param>
    /// <returns>The same service collection for chaining</returns>
    public static IServiceCollection AddTenantSupport(this IServiceCollection services, Action<TenantPluginOptions>? configure = null)
    {
        return services.AddTenantSupport<HttpContextTenantProvider>(configure);
    }

    /// <summary>
    /// Registers tenant plugin support with a custom tenant provider.
    /// </summary>
    /// <typeparam name="TTenantProvider">Custom ITenantProvider implementation</typeparam>
    /// <param name="services">Service collection to configure</param>
    /// <param name="configure">Optional configuration callback for tenant options</param>
    /// <returns>The same service collection for chaining</returns>
    public static IServiceCollection AddTenantSupport<TTenantProvider>(this IServiceCollection services, Action<TenantPluginOptions>? configure = null)
        where TTenantProvider : class, ITenantProvider
    {
        // Register tenant options
        services.AddSingleton(sp => {
            var options = new TenantPluginOptions();
            configure?.Invoke(options);
            return options;
        });

        // Register tenant provider
        services.TryAddScoped<ITenantProvider, TTenantProvider>();

        // For HttpContextTenantProvider, also register HttpContextAccessor and TenantProviderOptions if needed
        if (typeof(TTenantProvider) == typeof(HttpContextTenantProvider))
        {
            services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        }

        // Register tenant plugins
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPrePatchPlugin, TenantPrePatchPlugin>());
        return services;
    }
}