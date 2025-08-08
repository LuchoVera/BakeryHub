using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using BakeryHub.Domain.Interfaces;
using BakeryHub.Modules.Catalog.Application.Interfaces;
using BakeryHub.Modules.Catalog.Application.Dtos;
using Microsoft.AspNetCore.Http;
using BakeryHub.Modules.Accounts.Domain.Models;
using BakeryHub.Shared.Kernel;

namespace BakeryHub.Modules.Catalog.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProductsController : AdminControllerBase
{
    private readonly IProductService _productService;
    private readonly ITenantRepository _tenantRepository;

    public ProductsController(IProductService productService, ITenantRepository tenantRepository, UserManager<ApplicationUser> userManager)
        : base(userManager)
    {
        _productService = productService;
        _tenantRepository = tenantRepository;
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(IEnumerable<ProductDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<ProductDto>>> GetAllProductsForAdmin([FromQuery] List<string>? tags)
    {
        var adminTenantId = await GetCurrentAdminTenantIdAsync();
        if (adminTenantId == null) return Forbid("Admin not associated with a tenant.");

        var productDtos = await _productService.GetAllProductsForAdminAsync(adminTenantId.Value, tags);
        return Ok(productDtos);
    }

    [HttpGet("category/{categoryId:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(IEnumerable<ProductDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<ProductDto>>> GetAvailableProductsByCategoryForAdmin(Guid categoryId)
    {
        var adminTenantId = await GetCurrentAdminTenantIdAsync();
        if (adminTenantId == null) return Forbid("Admin not associated with a tenant.");

        var productDtos = await _productService.GetAvailableProductsByCategoryForAdminAsync(categoryId, adminTenantId.Value);
        return Ok(productDtos);
    }

    [HttpGet("{id:guid}", Name = "GetProductById")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDto>> GetProductById(Guid id)
    {
        var adminTenantId = await GetCurrentAdminTenantIdAsync();
        if (adminTenantId == null) return Forbid("Admin not associated with a tenant.");

        var productDto = await _productService.GetProductByIdForAdminAsync(id, adminTenantId.Value);
        if (productDto == null) return NotFound($"Product with ID {id} not found for your tenant.");
        return Ok(productDto);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProductDto>> CreateProduct([FromBody] CreateProductDto productDto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var adminTenantId = await GetCurrentAdminTenantIdAsync();
        if (adminTenantId == null) return Forbid("Admin not associated with a tenant.");

        var createdProductDto = await _productService.CreateProductForAdminAsync(productDto, adminTenantId.Value);
        if (createdProductDto == null) return BadRequest("Failed to create product (e.g., invalid CategoryId or tag issue).");

        return CreatedAtAction(nameof(GetProductById), new { id = createdProductDto.Id }, createdProductDto);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateProduct(Guid id, [FromBody] UpdateProductDto productDto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var adminTenantId = await GetCurrentAdminTenantIdAsync();
        if (adminTenantId == null) return Forbid("Admin not associated with a tenant.");

        var success = await _productService.UpdateProductForAdminAsync(id, productDto, adminTenantId.Value);
        if (!success) return NotFound($"Product with ID {id} not found for your tenant or update failed.");
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteProduct(Guid id)
    {
        var adminTenantId = await GetCurrentAdminTenantIdAsync();
        if (adminTenantId == null) return Forbid("Admin not associated with a tenant.");

        try
        {
            var success = await _productService.DeleteProductForAdminAsync(id, adminTenantId.Value);

            if (!success)
            {
                return NotFound($"Product with ID {id} not found for your tenant.");
            }

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPatch("{id:guid}/availability")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetProductAvailability(Guid id, [FromBody] bool isAvailable)
    {
        var adminTenantId = await GetCurrentAdminTenantIdAsync();
        if (adminTenantId == null) return Forbid("Admin not associated with a tenant.");

        var success = await _productService.SetProductAvailabilityForAdminAsync(id, isAvailable, adminTenantId.Value);

        if (!success) return NotFound($"Product with ID {id} not found for your tenant.");
        return NoContent();
    }

    [HttpGet("{subdomain}/products/{productId:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDto>> GetPublicProductDetails(string subdomain, Guid productId)
    {
        var tenant = await _tenantRepository.GetBySubdomainAsync(subdomain.ToLowerInvariant());
        if (tenant == null) return NotFound($"Bakery '{subdomain}' not found.");

        var productDto = await _productService.GetPublicProductByIdAsync(productId, tenant.Id);
        if (productDto == null) return NotFound("Product details not found or not available.");

        return Ok(productDto);
    }
}
