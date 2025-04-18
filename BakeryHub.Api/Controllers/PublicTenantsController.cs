using BakeryHub.Application.Dtos;
using BakeryHub.Application.Interfaces;
using BakeryHub.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BakeryHub.Api.Controllers;

[ApiController]
[Route("api/public/tenants")]
public class PublicTenantsController : ControllerBase
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IAccountService _accountService;
    private readonly IProductService _productService;

    public PublicTenantsController(
        ITenantRepository tenantRepository,
        IProductService productService,
        IAccountService accountService)
    {
        _tenantRepository = tenantRepository;
        _productService = productService;
        _accountService = accountService;
    }

    [HttpGet("{subdomain}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(TenantPublicInfoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TenantPublicInfoDto>> GetTenantPublicInfo(string subdomain)
    {
        var tenant = await _tenantRepository.GetBySubdomainAsync(subdomain.ToLowerInvariant());
        if (tenant == null) return NotFound("Tenant not found.");

        var tenantInfo = new TenantPublicInfoDto
        {
            Name = tenant.Name,
            Subdomain = tenant.Subdomain
        };
        return Ok(tenantInfo);
    }

    [HttpGet("{subdomain}/products")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<ProductDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<ProductDto>>> GetTenantPublicProducts(string subdomain)
    {
        if (string.IsNullOrWhiteSpace(subdomain))
        {
            return BadRequest("Subdomain cannot be empty.");
        }

        var tenant = await _tenantRepository.GetBySubdomainAsync(subdomain.ToLowerInvariant());
        if (tenant == null)
        {
            return NotFound($"Tenant '{subdomain}' not found.");
        }

        var productDtos = await _productService.GetPublicProductsByTenantIdAsync(tenant.Id);
        return Ok(productDtos);
    }

    [HttpPost("{subdomain}/register-customer")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RegisterCustomerForTenant(string subdomain, [FromBody] CustomerRegisterDto registerDto)
    {
        if (string.IsNullOrWhiteSpace(subdomain))
        {
            return BadRequest(new { message = "Subdomain cannot be empty." });
        }
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var tenant = await _tenantRepository.GetBySubdomainAsync(subdomain.ToLowerInvariant());
        if (tenant == null)
        {
            return NotFound(new { message = $"Bakery '{subdomain}' not found." });
        }

        var (result, userId) = await _accountService.RegisterCustomerForTenantAsync(registerDto, tenant.Id);

        if (result.Succeeded && userId != null)
        {
            return Ok(new { Message = "Customer registration successful.", UserId = userId });
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(error.Code ?? string.Empty, error.Description);
        }
        return ValidationProblem(ModelState);
    }
}
