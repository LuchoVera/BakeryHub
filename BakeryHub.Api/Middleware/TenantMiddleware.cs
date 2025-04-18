using Microsoft.Extensions.Primitives;

namespace BakeryHub.Api.Middleware;
public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;
    public const string TenantIdHeaderName = "X-Tenant-ID";
    private const string TenantContextItemsKey = "TenantId_MyApp";

    public TenantResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Request.Headers.TryGetValue(TenantIdHeaderName, out StringValues tenantIdFromHeader);
        string? tenantIdLower = tenantIdFromHeader.FirstOrDefault()?.ToLowerInvariant();

        if (!string.IsNullOrEmpty(tenantIdLower))
        {
            context.Items[TenantContextItemsKey] = tenantIdLower;
        }

        await _next(context);
    }
}
