namespace CoreOne.ModelPatch.Tenants;

/// <summary>
/// Configuration options for the tenant plugin system.
/// </summary>
public class TenantPluginOptions
{
    /// <summary>
    /// Whether to automatically inject the tenant ID into new entities during pre-patch.
    /// </summary>
    public bool AutoInjectTenantId { get; set; } = true;

    /// <summary>
    /// Entity types that support multi-tenancy.
    /// If empty, all entities with the TenantIdPropertyName are treated as multi-tenant.
    /// </summary>
    public HashSet<Type> MultiTenantEntityTypes { get; init; } = [];

    /// <summary>
    /// Custom function to extract tenant ID from an entity instance.
    /// If not set, uses reflection to read the TenantIdPropertyName property.
    /// </summary>
    public Func<object, object>? TenantIdExtractor { get; set; }

    /// <summary>
    /// Custom function to set tenant ID on an entity instance.
    /// If not set, uses reflection to write the TenantIdPropertyName property.
    /// </summary>
    public Action<object, object>? TenantIdSetter { get; set; }

    /// <summary>
    /// Whether to throw an exception when the tenant ID doesn't match during validation.
    /// If false, logs a warning instead.
    /// </summary>
    public bool ThrowOnTenantMismatch { get; set; } = true;
}