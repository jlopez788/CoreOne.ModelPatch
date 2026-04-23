using CoreOne.ModelPatch.Tenants.Services;

namespace CoreOne.ModelPatch.Tenants;

/// <summary>
/// Configuration options for the tenant plugin system.
/// </summary>
public class TenantPluginOptions
{
    /// <summary>
    /// Entity types that support multi-tenancy.
    /// If empty, all entities with the TenantIdPropertyName are treated as multi-tenant.
    /// </summary>
    public HashSet<Type> MultiTenantEntityTypes { get; init; } = [];

    /// <summary>
    /// Tenant accessor to extract tenant ID from HttpContext
    /// </summary>
    public HttpContexTenatAccessor? TenatAccessor { get; set; }
    /// <summary>
    /// Whether to throw an exception when the tenant ID doesn't match during validation.
    /// If false, logs a warning instead.
    /// </summary>
    public bool ThrowOnTenantMismatch { get; set; } = true;
}