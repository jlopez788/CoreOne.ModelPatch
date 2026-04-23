using CoreOne.Identity.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CoreOne.ModelPatch.Tenants;

/// <summary>
/// Default ITenantProvider implementation that extracts tenant ID from HttpContext.
/// Supports common patterns like headers, route parameters, and claims.
/// </summary>
public class HttpContextTenantProvider(IHttpContextAccessor httpContextAccessor, TenantProviderOptions options) : ITenantProvider
{
    public ValueTask<object?> GetTenantKey()
    {
        return ValueTask.FromResult(getTenantKey());

        object? getTenantKey()
        {
            var context = httpContextAccessor.HttpContext;
            if (context is null)
                return null;

            // Try to get from header
            if (!string.IsNullOrEmpty(options.HeaderName) && context.Request.Headers.TryGetValue(options.HeaderName, out var headerValue))
            {
                return headerValue.ToString();
            }

            // Try to get from route parameter
            if (!string.IsNullOrEmpty(options.RouteParameterName) && context.GetRouteValue(options.RouteParameterName) is not null)
            {
                return context.GetRouteValue(options.RouteParameterName)!;
            }

            // Try to get from claims
            if (!string.IsNullOrEmpty(options.ClaimType))
            {
                var claim = context.User?.FindFirst(options.ClaimType);
                if (claim is not null)
                {
                    return claim.Value;
                }
            }

            return null;
        }
    }
}

/// <summary>
/// Configuration options for HttpContextTenantProvider.
/// </summary>
public class TenantProviderOptions
{
    /// <summary>
    /// HTTP header name to look for tenant ID. Default: "X-Tenant-Id".
    /// </summary>
    public string HeaderName { get; set; } = "X-Tenant-Id";

    /// <summary>
    /// Route parameter name to look for tenant ID. Leave empty to disable.
    /// </summary>
    public string? RouteParameterName { get; set; }

    /// <summary>
    /// Claim type to look for tenant ID. Leave empty to disable.
    /// </summary>
    public string? ClaimType { get; set; }
}