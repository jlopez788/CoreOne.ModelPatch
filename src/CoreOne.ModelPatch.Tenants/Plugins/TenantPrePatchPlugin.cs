using CoreOne.Identity.Attributes;
using CoreOne.Identity.Contracts;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace CoreOne.ModelPatch.Tenants.Plugins;

/// <summary>
/// Plugin that validates an entity belongs to the current tenant after patching.
/// Ensures that updates to existing entities don't violate tenant isolation.
/// </summary>
public class TenantPrePatchPlugin(ITenantProvider tenantProvider, TenantPluginOptions options, ILogger<TenantPrePatchPlugin> logger) : IPrePatchPlugin
{
    private readonly ITenantProvider _tenantProvider = tenantProvider ?? throw new ArgumentNullException(nameof(tenantProvider));
    private readonly TenantPluginOptions _options = options ?? new TenantPluginOptions();
    public int Order => 9999; // Run early in the pipeline, after patch is applied

    public async ValueTask<IResult> Execute(ModelProcessContext context, CancellationToken cancellationToken = default)
    {
        if (context.Type.GetCustomAttribute<TenantOwnedAttribute>() is null)
            return Result.Ok;

        var tenantInfo = context.Context.Properties
              .Select(p => new {
                  tenantKey = p.Value.GetCustomAttribute<TenantKeyAttribute>(),
                  metadata = p.Value
              }).FirstOrDefault(p => p.tenantKey is not null);

        if (tenantInfo is null || tenantInfo.metadata == Metadata.Empty)
            return Result.Ok;

        // Get current tenant ID
        var tenantKey = await _tenantProvider.GetTenantKey();
        if (tenantKey is null)
        {
            logger.LogWarning("Unable to resolve tenant ID for validation of entity type {EntityType}", context.Type.Name);
            return Result.Fail("Invalid tenant key");
        }

        var tenantPropertyName = tenantInfo.metadata.Name;
        var hasIncomingTenant = context.Delta.TryGetValue(tenantPropertyName, out var incomingTenant);
        if (hasIncomingTenant)
        {
            if (!TenantKeysMatch(incomingTenant, tenantKey))
            {
                var message = $"Tenant key mismatch for {context.Type.Name}.";
                logger.LogWarning(message);
                return Result.Fail(message);
            }
        }
        else
        {
            context.Delta[tenantPropertyName] = tenantKey;
        }

        if (context.State == CrudType.Updated)
        {
            var currentTenant = tenantInfo.metadata.GetValue(context.Model);
            if (!TenantKeysMatch(currentTenant, tenantKey))
            {
                var message = $"Entity of type {context.Type.Name} does not belong to tenant {currentTenant}. Found tenant {tenantKey}.";
                logger.LogWarning(message);
                return _options.ThrowOnTenantMismatch ? Result.Fail(message) : Result.Ok;
            }
        }

        return Result.Ok;
    }

    private static bool TenantKeysMatch(object? left, object? right)
    {
        if (left is null || right is null)
            return left is null && right is null;

        if (Equals(left, right))
            return true;

        return string.Equals(left.ToString(), right.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}