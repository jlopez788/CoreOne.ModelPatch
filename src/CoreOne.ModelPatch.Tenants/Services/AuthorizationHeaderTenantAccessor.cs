using Microsoft.AspNetCore.Http;

namespace CoreOne.ModelPatch.Tenants.Services;

public class AuthorizationHeaderTenantAccessor(string headerName) : HttpContexTenatAccessor
{
    public override string ToString() => headerName;

    protected override object? OnGetTenantKey(HttpContext context)
    {
        return !string.IsNullOrEmpty(headerName) &&
            context.Request.Headers.TryGetValue(headerName, out var headerValue) ?
            headerValue.ToString() : null;
    }
}