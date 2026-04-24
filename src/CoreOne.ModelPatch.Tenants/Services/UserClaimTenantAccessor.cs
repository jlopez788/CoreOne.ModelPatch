using Microsoft.AspNetCore.Http;

namespace CoreOne.ModelPatch.Tenants.Services;

public class UserClaimTenantAccessor(string claimType) : HttpContexTenatAccessor
{
    public override string ToString() => claimType;

    protected override object? OnGetTenantKey(HttpContext context)
    {
        return !string.IsNullOrEmpty(claimType) ?
            context.User.FindFirst(c => c.Type == claimType)?.Value : null;
    }
}