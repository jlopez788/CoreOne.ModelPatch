using CoreOne.Identity.Contracts;
using Microsoft.AspNetCore.Http;

namespace CoreOne.ModelPatch.Tenants;

/// <summary>
/// Default ITenantProvider implementation that extracts tenant ID from HttpContext.
/// Supports common patterns like headers, route parameters, and claims.
/// </summary>
public class HttpContextTenantProvider(IHttpContextAccessor httpContextAccessor, TenantPluginOptions options) : ITenantProvider
{
    public ValueTask<object?> GetTenantKey()
    {
        return ValueTask.FromResult(getTenantKey());

        object? getTenantKey()
        {
            var context = httpContextAccessor.HttpContext;
            return context is null ? null : (options.TenatAccessor?.GetTenantKey(context));
        }
    }
}