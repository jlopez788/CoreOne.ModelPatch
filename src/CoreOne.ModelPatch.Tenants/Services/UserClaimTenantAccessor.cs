using Microsoft.AspNetCore.Http;

namespace CoreOne.ModelPatch.Tenants.Services;

public class UserClaimTenantAccessor(string claimType) : HttpContexTenatAccessor
{
    protected override object? GetTenantKeyCore(HttpContext context)
    {
        return !string.IsNullOrEmpty(claimType) ?
            context.User.FindFirst(c => c.Type == claimType)?.Value : null;
    }
}