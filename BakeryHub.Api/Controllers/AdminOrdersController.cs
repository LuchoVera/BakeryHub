using BakeryHub.Application.Dtos;
using BakeryHub.Application.Interfaces;
using BakeryHub.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BakeryHub.Api.Controllers;

[ApiController]
[Route("api/admin/orders")]
[Authorize(Roles = "Admin")]
public class AdminOrdersController : AdminControllerBase
{
    private readonly IOrderService _orderService;

    public AdminOrdersController(IOrderService orderService, UserManager<ApplicationUser> userManager)
        : base(userManager)
    {
        _orderService = orderService;
    }

    [HttpGet("my")]
    [ProducesResponseType(typeof(IEnumerable<OrderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<OrderDto>>> GetMyTenantOrders()
    {
        var tenantId = await GetCurrentAdminTenantIdAsync();
        if (tenantId == null)
        {
            return Forbid();
        }

        var orders = await _orderService.GetOrdersForAdminAsync(tenantId.Value);
        return Ok(orders);
    }

    [HttpGet("{orderId:guid}")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDto>> GetOrderDetails(Guid orderId)
    {
        var tenantId = await GetCurrentAdminTenantIdAsync();
        if (tenantId == null)
        {
            return Forbid();
        }

        var orderDto = await _orderService.GetOrderDetailsForAdminAsync(orderId, tenantId.Value);

        if (orderDto == null)
        {
            return NotFound();
        }

        return Ok(orderDto);
    }

    [HttpPut("{orderId:guid}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateOrderStatus(Guid orderId, [FromBody] UpdateOrderStatusDto statusUpdateDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var tenantId = await GetCurrentAdminTenantIdAsync();
        if (tenantId == null)
        {
            return Forbid();
        }

        var adminUserId = GetCurrentAdminUserId();

        var success = await _orderService.UpdateOrderStatusAsync(
            orderId,
            tenantId.Value,
            statusUpdateDto.NewStatus,
            adminUserId
        );

        if (!success)
        {
            return NotFound();
        }

        return NoContent();
    }

    [HttpPost("manual")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<OrderDto>> CreateManualOrder([FromBody] CreateManualOrderDto createManualOrderDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var tenantId = await GetCurrentAdminTenantIdAsync();
        if (tenantId == null)
        {
            return Forbid();
        }

        var createdOrder = await _orderService.CreateManualOrderForAdminAsync(tenantId.Value, createManualOrderDto);
        if (createdOrder == null)
        {
            return BadRequest("Could not create order. Check product availability or details.");
        }

        return CreatedAtAction(nameof(GetOrderDetails), new { orderId = createdOrder.Id }, createdOrder);
    }
}
