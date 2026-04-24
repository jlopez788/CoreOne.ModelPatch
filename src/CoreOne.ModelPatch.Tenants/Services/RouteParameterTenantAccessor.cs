using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CoreOne.ModelPatch.Tenants.Services;

public class RouteParameterTenantAccessor(string parameterName) : HttpContexTenatAccessor
{
    public override string ToString() => parameterName;

    protected override object? OnGetTenantKey(HttpContext context)
    {
        return !string.IsNullOrEmpty(parameterName) &&
            context.GetRouteValue(parameterName) is not null ?
            context.GetRouteValue(parameterName)! : null;
    }
}