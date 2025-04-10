using BakeryHub.Application.Interfaces;
using Microsoft.AspNetCore.Http;

namespace BakeryHub.Application.Services;

public class TenantService : ITenantService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private const string TenantContextItemsKey = "TenantId_MyApp";

    public TenantService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? GetCurrentTenantId()
    {
        return _httpContextAccessor.HttpContext?.Items[TenantContextItemsKey] as string;
    }
}
