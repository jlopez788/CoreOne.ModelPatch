using Microsoft.AspNetCore.Http;

namespace CoreOne.ModelPatch.Tenants.Services;

public abstract class HttpContexTenatAccessor
{
    public object? GetTenantKey(HttpContext context) => GetTenantKeyCore(context);

    protected abstract object? GetTenantKeyCore(HttpContext context);
}