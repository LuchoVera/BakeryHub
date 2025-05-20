using System.Security.Claims;
using BakeryHub.Application.Dtos;
using BakeryHub.Application.Dtos.Enums;
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
    private readonly IRecommendationService _recommendationService;
    private readonly IOrderService _orderService;
    private readonly ICategoryService _categoryService;


    public PublicTenantsController(
        ITenantRepository tenantRepository,
        IProductService productService,
        IAccountService accountService,
        IRecommendationService recommendationService,
        IOrderService orderService,
        ICategoryService categoryService)
    {
        _tenantRepository = tenantRepository;
        _productService = productService;
        _accountService = accountService;
        _recommendationService = recommendationService;
        _orderService = orderService;
        _categoryService = categoryService;
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
            Subdomain = tenant.Subdomain,
            PhoneNumber = tenant?.PhoneNumber
        };
        return Ok(tenantInfo);
    }

    [HttpGet("{subdomain}/products")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<ProductDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<ProductDto>>> GetTenantPublicProducts(
        string subdomain,
        [FromQuery] Guid? categoryId = null,
        [FromQuery] decimal? minPrice = null,
        [FromQuery] decimal? maxPrice = null
        )
    {
        if (string.IsNullOrWhiteSpace(subdomain))
            return BadRequest("Subdomain cannot be empty.");

        if (minPrice.HasValue && minPrice < 0) return BadRequest("Minimum price cannot be negative.");
        if (maxPrice.HasValue && maxPrice < 0) return BadRequest("Maximum price cannot be negative.");
        if (minPrice.HasValue && maxPrice.HasValue && minPrice > maxPrice) return BadRequest("Minimum price cannot be greater than maximum price.");

        var tenant = await _tenantRepository.GetBySubdomainAsync(subdomain.ToLowerInvariant());
        if (tenant == null)
            return NotFound($"Tenant '{subdomain}' not found.");

        var productDtos = await _productService.GetPublicProductsByTenantIdAsync(
            tenant.Id,
            null,
            categoryId,
            minPrice,
            maxPrice
            );

        return Ok(productDtos);
    }

    [HttpPost("{subdomain}/register-customer")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status409Conflict)]
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

        var detailedResult = await _accountService.RegisterCustomerForTenantAsync(registerDto, tenant.Id);

        switch (detailedResult.Outcome)
        {
            case RegistrationOutcome.UserCreated:
                return Ok(new { message = "Registration successful.", status = "UserCreated", userId = detailedResult.UserId });

            case RegistrationOutcome.MembershipCreated:
                return Ok(new { message = "Existing account linked successfully to this bakery.", status = "Linked", userId = detailedResult.UserId });

            case RegistrationOutcome.AlreadyMember:
                ModelState.AddModelError("AlreadyMember", detailedResult.IdentityResult.Errors.FirstOrDefault()?.Description ?? "You are already registered with this bakery.");
                return Conflict(new ValidationProblemDetails(ModelState) { Title = "Already Registered", Status = StatusCodes.Status409Conflict });

            case RegistrationOutcome.AdminConflict:
                ModelState.AddModelError("AdminConflict", detailedResult.IdentityResult.Errors.FirstOrDefault()?.Description ?? "This email address belongs to an administrator.");
                return Conflict(new ValidationProblemDetails(ModelState) { Title = "Registration Forbidden", Status = StatusCodes.Status409Conflict });

            case RegistrationOutcome.TenantNotFound:
                return NotFound(new { message = "Bakery not found." });

            case RegistrationOutcome.RoleAssignmentFailed:
            case RegistrationOutcome.Failed:
            case RegistrationOutcome.UnknownError:
            default:
                foreach (var error in detailedResult.IdentityResult.Errors)
                {
                    ModelState.AddModelError(error.Code ?? string.Empty, error.Description);
                }
                return ValidationProblem(ModelState);
        }
    }

    [HttpPost("{subdomain}/link-customer")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> LinkExistingCustomer(string subdomain, [FromBody] LinkCustomerDto linkDto)
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

        var linkResult = await _accountService.LinkExistingCustomerToTenantAsync(linkDto.Email, tenant.Id);

        switch (linkResult.Outcome)
        {
            case LinkAccountOutcome.Linked:
                return Ok(new { message = "Account linked successfully.", status = "Linked" });

            case LinkAccountOutcome.AlreadyMember:
                ModelState.AddModelError("AlreadyMember", linkResult.IdentityResult.Errors.FirstOrDefault()?.Description ?? "Account is already linked to this bakery.");
                return Conflict(new ValidationProblemDetails(ModelState) { Title = "Already Linked", Status = StatusCodes.Status409Conflict });

            case LinkAccountOutcome.AdminConflict:
                ModelState.AddModelError("AdminConflict", linkResult.IdentityResult.Errors.FirstOrDefault()?.Description ?? "This email belongs to an administrator and cannot be linked.");
                return Conflict(new ValidationProblemDetails(ModelState) { Title = "Linking Forbidden", Status = StatusCodes.Status409Conflict });

            case LinkAccountOutcome.UserNotFound:
                return NotFound(new { message = linkResult.IdentityResult.Errors.FirstOrDefault()?.Description ?? "Email address not found." });

            case LinkAccountOutcome.UserNotCustomer:
                ModelState.AddModelError("UserNotCustomer", linkResult.IdentityResult.Errors.FirstOrDefault()?.Description ?? "This account cannot be linked as a customer.");
                return BadRequest(new ValidationProblemDetails(ModelState) { Title = "Account Type Invalid" });

            case LinkAccountOutcome.TenantNotFound:
                return NotFound(new { message = linkResult.IdentityResult.Errors.FirstOrDefault()?.Description ?? "Bakery not found." });

            case LinkAccountOutcome.DbError:
            case LinkAccountOutcome.Failed:
            default:
                ModelState.AddModelError("LinkingFailed", linkResult.IdentityResult.Errors.FirstOrDefault()?.Description ?? "Failed to link the account due to an unexpected error.");
                return BadRequest(new ValidationProblemDetails(ModelState) { Title = "Linking Failed" });
        }
    }

    [HttpGet("{subdomain}/recommendations")]
    [Authorize]
    [ProducesResponseType(typeof(IEnumerable<ProductDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<IEnumerable<ProductDto>>> GetRecommendations(string subdomain)
    {

        var tenant = await _tenantRepository.GetBySubdomainAsync(subdomain.ToLowerInvariant());
        if (tenant == null)
        {
            return NotFound($"Tenant '{subdomain}' not found.");
        }

        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdString, out Guid userId))
        {
            return Unauthorized("Cannot identify logged in user.");
        }

        try
        {
            var recommendations = await _recommendationService.GetRecommendationsAsync(userId, tenant.Id, 10);

            if (recommendations == null)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, "Recommendation service is not ready.");
            }

            return Ok(recommendations);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error getting recommendations for user {userId} in tenant {tenant.Id}: {ex}");
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while generating recommendations.");
        }
    }

    [HttpGet("{subdomain}/search")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<ProductDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<ProductDto>>> SearchProductsByName(
        string subdomain,
        [FromQuery] string q,
        [FromQuery] Guid? categoryId = null,
        [FromQuery] decimal? minPrice = null,
        [FromQuery] decimal? maxPrice = null
        )
    {
        if (string.IsNullOrWhiteSpace(subdomain))
            return BadRequest("Subdomain cannot be empty.");
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest("Search query 'q' cannot be empty.");

        if (minPrice.HasValue && minPrice < 0)
            return BadRequest("Minimum price cannot be negative.");
        if (maxPrice.HasValue && maxPrice < 0)
            return BadRequest("Maximum price cannot be negative.");
        if (minPrice.HasValue && maxPrice.HasValue && minPrice > maxPrice)
            return BadRequest("Minimum price cannot be greater than maximum price.");


        var tenant = await _tenantRepository.GetBySubdomainAsync(subdomain.ToLowerInvariant());
        if (tenant == null)
            return NotFound($"Tenant '{subdomain}' not found.");

        var productDtos = await _productService.SearchPublicProductsByNameAsync(
            tenant.Id, q, categoryId, minPrice, maxPrice);

        return Ok(productDtos);
    }

    [HttpPost("{subdomain}/orders")]
    [Authorize(Roles = "Customer")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDto>> CreateOrder(
        string subdomain,
        [FromBody] CreateOrderDto createOrderDto)
    {
        if (string.IsNullOrWhiteSpace(subdomain)) return BadRequest("Subdomain no puede estar vacío.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenant = await _tenantRepository.GetBySubdomainAsync(subdomain.ToLowerInvariant());
        if (tenant == null) return NotFound($"Tenant '{subdomain}' no encontrado.");

        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdString, out Guid applicationUserId)) return Unauthorized("Usuario no identificado.");

        var createdOrderDto = await _orderService.CreateOrderAsync(tenant.Id, applicationUserId, createOrderDto);
        if (createdOrderDto == null) return BadRequest("No se pudo crear el pedido. Verifique los items o intente más tarde.");

        return CreatedAtAction(nameof(GetOrderDetailsForCustomer),
            new { subdomain = subdomain, orderId = createdOrderDto.Id },
            createdOrderDto);
    }

    [HttpGet("{subdomain}/orders")]
    [Authorize(Roles = "Customer")]
    [ProducesResponseType(typeof(IEnumerable<OrderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<OrderDto>>> GetMyOrderHistory(string subdomain)
    {
        if (string.IsNullOrWhiteSpace(subdomain)) return BadRequest("Subdomain no puede estar vacío.");

        var tenant = await _tenantRepository.GetBySubdomainAsync(subdomain.ToLowerInvariant());
        if (tenant == null) return NotFound($"Tenant '{subdomain}' no encontrado.");

        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdString, out Guid applicationUserId)) return Unauthorized("Usuario no identificado.");

        var orders = await _orderService.GetOrderHistoryForCustomerAsync(applicationUserId, tenant.Id);
        return Ok(orders);
    }

    [HttpGet("{subdomain}/orders/{orderId:guid}", Name = "GetOrderDetailsForCustomer")]
    [Authorize(Roles = "Customer")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDto>> GetOrderDetailsForCustomer(string subdomain, Guid orderId)
    {
        if (string.IsNullOrWhiteSpace(subdomain)) return BadRequest("Subdomain no puede estar vacío.");

        var tenant = await _tenantRepository.GetBySubdomainAsync(subdomain.ToLowerInvariant());
        if (tenant == null) return NotFound($"Tenant '{subdomain}' no encontrado.");

        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdString, out Guid applicationUserId)) return Unauthorized("Usuario no identificado.");

        var orderDto = await _orderService.GetOrderDetailsForCustomerAsync(orderId, applicationUserId, tenant.Id);
        if (orderDto == null) return NotFound($"Pedido con ID '{orderId}' no encontrado o no pertenece a este usuario/tienda.");

        return Ok(orderDto);
    }

    [HttpGet("{subdomain}/categories/preferred")]
    [Authorize(Roles = "Customer")]
    [ProducesResponseType(typeof(IEnumerable<CategoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<CategoryDto>>> GetPreferredCategories(string subdomain)
    {
        var tenant = await _tenantRepository.GetBySubdomainAsync(subdomain.ToLowerInvariant());
        if (tenant == null)
        {
            return NotFound($"Tenant '{subdomain}' not found.");
        }

        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdString, out Guid userId))
        {
            return Unauthorized("User ID could not be determined.");
        }

        var preferredCategories = await _categoryService.GetPreferredCategoriesForCustomerAsync(tenant.Id, userId);
        return Ok(preferredCategories);
    }
}
