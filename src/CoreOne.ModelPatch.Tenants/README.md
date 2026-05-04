# CoreOne.ModelPatch.Tenants

Optional multi-tenancy plugin for [CoreOne.ModelPatch](https://www.nuget.org/packages/CoreOne.ModelPatch). Automatically validates and injects the current tenant key into every PATCH operation so your EF Core entities stay properly scoped without any manual work in your controllers or services.

## 📦 Installation

```bash
dotnet add package CoreOne.ModelPatch.Tenants
```

**Requirements:** net9.0 or net10.0, CoreOne.ModelPatch registered in DI

---

## How It Works

1. You mark your entities with `[TenantOwned]` and one of their properties with `[TenantKey]`.
2. On every patch call the `TenantPrePatchPlugin` runs before the delta is applied.
3. It reads the current tenant ID from the configured `ITenantProvider`.
4. If the delta already contains a tenant key, it validates it matches the current tenant.
5. If the delta does not contain a tenant key, it injects one automatically.
6. For updates it also verifies the existing record in the database belongs to the same tenant.

---

## Quick Start

### 1. Mark Your Entities

```csharp
using CoreOne.Identity.Attributes;

[TenantOwned]
public class Invoice
{
    public Guid Id { get; set; }

    [TenantKey]
    public string TenantId { get; set; }

    public string Description { get; set; }
    public decimal Amount { get; set; }
}
```

### 2. Register Services

```csharp
// Program.cs
services.AddModelPatch();          // Register the core patch service first
services.AddTenantSupport(options =>
{
    // Choose how the tenant ID is extracted from the incoming HTTP request
    options.TenatAccessor = new UserClaimTenantAccessor("tenant_id");
    options.ThrowOnTenantMismatch = true; // Throw when a cross-tenant write is attempted
});
```

### 3. Use Normally — Tenant Isolation Is Automatic

```csharp
public class InvoiceService(IDataModelService<AppDbContext> patchService)
{
    public Task<PatchResult> Update(Delta<Invoice> delta, CancellationToken ct)
    {
        // No need to set TenantId manually — the plugin injects it for you
        return patchService.Patch(delta, ct);
    }
}
```

---

## Tenant Accessor Options

The `TenatAccessor` property on `TenantPluginOptions` controls how the tenant ID is extracted from the incoming HTTP request. Choose the strategy that matches your architecture:

### User Claim (most common for JWT)

```csharp
options.TenatAccessor = new UserClaimTenantAccessor("tenant_id");
```

Reads the tenant ID from the authenticated user's claims. The argument is the claim type name.

---

### HTTP Header

```csharp
options.TenatAccessor = new AuthorizationHeaderTenantAccessor("X-Tenant-Id");
```

Reads the tenant ID from a request header. Useful for service-to-service calls.

---

### Route Parameter

```csharp
options.TenatAccessor = new RouteParameterTenantAccessor("tenantId");
```

Reads the tenant ID from a URL route segment, e.g. `/api/{tenantId}/invoices`.

---

## Custom Tenant Provider

If none of the built-in accessors fit, implement `ITenantProvider` directly:

```csharp
public class MyTenantProvider(IHttpContextAccessor http) : ITenantProvider
{
    public ValueTask<object?> GetTenantKey()
    {
        var tenantId = http.HttpContext?.Session.GetString("TenantId");
        return ValueTask.FromResult<object?>(tenantId);
    }
}

// Register with the generic overload
services.AddTenantSupport<MyTenantProvider>(options =>
{
    options.ThrowOnTenantMismatch = true;
});
```

---

## TenantPluginOptions Reference

| Property | Type | Default | Description |
|---|---|---|---|
| `TenatAccessor` | `HttpContexTenatAccessor?` | `null` | Strategy for extracting tenant ID from `HttpContext` |
| `ThrowOnTenantMismatch` | `bool` | `true` | Throw when the incoming tenant key doesn't match the current tenant |
| `MultiTenantEntityTypes` | `HashSet<Type>` | empty | Restrict tenant enforcement to specific entity types (empty = all `[TenantOwned]` types) |

---

## Plugin Execution Order

The `TenantPrePatchPlugin` runs with `Order = 9999`, which means it executes **first** in the pre-patch pipeline — before strict field validation, concurrency checks, and attribute restrictions. This ensures the tenant key is always present and valid before any other processing occurs.

---

## Related Packages

| Package | Description |
|---|---|
| [CoreOne.ModelPatch](https://www.nuget.org/packages/CoreOne.ModelPatch) | Core PATCH implementation — required |
| [CoreOne.ModelPatch.Abstract](https://www.nuget.org/packages/CoreOne.ModelPatch.Abstract) | Interfaces and models — useful for plugin or shared-library projects |

---

## License

MIT — see [LICENSE](https://github.com/jlopez788/CoreOne.ModelPatch/blob/main/LICENSE) on GitHub.
