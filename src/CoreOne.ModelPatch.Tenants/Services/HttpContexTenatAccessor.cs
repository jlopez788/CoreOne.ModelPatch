using Microsoft.AspNetCore.Http;

namespace CoreOne.ModelPatch.Tenants.Services;

public abstract class HttpContexTenatAccessor
{
    public object? GetTenantKey(HttpContext context) => OnGetTenantKey(context);

    protected abstract object? OnGetTenantKey(HttpContext context);
}