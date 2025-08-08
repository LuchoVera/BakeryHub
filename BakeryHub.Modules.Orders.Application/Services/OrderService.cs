using BakeryHub.Application.Interfaces;
using BakeryHub.Domain.Interfaces;
using BakeryHub.Modules.Accounts.Domain.Models;
using BakeryHub.Modules.Catalog.Domain.Interfaces;
using BakeryHub.Modules.Orders.Application.Dtos.Order;
using BakeryHub.Modules.Orders.Domain.Enums;
using BakeryHub.Modules.Orders.Domain.Interfaces;
using BakeryHub.Modules.Orders.Domain.Models;
using BakeryHub.Shared.Kernel.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace BakeryHub.Modules.Orders.Application.Services;

public class OrderService : IOrderService, IOrderChecker
{
    private readonly IOrderRepository _orderRepository;
    private readonly IProductRepository _productRepository;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUnitOfWork _unitOfWork;

    public OrderService(
        IOrderRepository orderRepository,
        IProductRepository productRepository,
        UserManager<ApplicationUser> userManager,
        IUnitOfWork unitOfWork)
    {
        _orderRepository = orderRepository;
        _productRepository = productRepository;
        _userManager = userManager;
        _unitOfWork = unitOfWork;
    }

    public async Task<OrderDto?> CreateOrderAsync(Guid tenantId, Guid applicationUserId, CreateOrderDto createOrderDto)
    {
        var user = await _userManager.FindByIdAsync(applicationUserId.ToString());
        if (user == null) return null;

        decimal verifiedTotalAmount = 0;
        var orderItems = new List<OrderItem>();

        foreach (var itemDto in createOrderDto.Items)
        {
            var product = await _productRepository.GetByIdAsync(itemDto.ProductId);
            if (product == null || !product.IsAvailable || product.TenantId != tenantId) return null;

            orderItems.Add(new OrderItem
            {
                Id = Guid.NewGuid(),
                ProductId = product.Id,
                ProductName = product.Name,
                Quantity = itemDto.Quantity,
                UnitPrice = product.Price
            });
            verifiedTotalAmount += product.Price * itemDto.Quantity;
        }

        if (Math.Abs(verifiedTotalAmount - createOrderDto.TotalAmount) > 0.001m) return null;

        var now = DateTimeOffset.UtcNow;
        var order = new Order
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ApplicationUserId = applicationUserId,
            OrderDate = now,
            DeliveryDate = createOrderDto.DeliveryDate,
            TotalAmount = verifiedTotalAmount,
            Status = OrderStatus.Pending,
            OrderItems = orderItems,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _orderRepository.AddOrderAsync(order);
        await _unitOfWork.SaveChangesAsync();

        return await MapOrderToDtoAsync(order);
    }

    public async Task<OrderDto?> CreateManualOrderForAdminAsync(Guid tenantId, CreateManualOrderDto createDto)
    {
        decimal verifiedTotalAmount = 0;
        var orderItems = new List<OrderItem>();
        foreach (var itemDto in createDto.Items)
        {
            var product = await _productRepository.GetByIdAsync(itemDto.ProductId);
            if (product == null || !product.IsAvailable || product.TenantId != tenantId) return null;

            orderItems.Add(new OrderItem
            {
                Id = Guid.NewGuid(),
                ProductId = product.Id,
                ProductName = product.Name,
                Quantity = itemDto.Quantity,
                UnitPrice = product.Price
            });
            verifiedTotalAmount += product.Price * itemDto.Quantity;
        }

        var order = new Order
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ApplicationUserId = null,
            CustomerName = createDto.CustomerName,
            CustomerPhoneNumber = createDto.CustomerPhoneNumber,
            OrderDate = DateTimeOffset.UtcNow,
            DeliveryDate = createDto.DeliveryDate,
            TotalAmount = verifiedTotalAmount,
            Status = OrderStatus.Pending,
            OrderItems = orderItems,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await _orderRepository.AddOrderAsync(order);
        await _unitOfWork.SaveChangesAsync();

        return await MapOrderToDtoAsync(order);
    }

    public async Task<OrderDto?> GetOrderDetailsForCustomerAsync(Guid orderId, Guid userId, Guid tenantId)
    {
        var order = await _orderRepository.GetOrderByIdAndUserAsync(orderId, userId, tenantId);
        if (order == null) return null;
        return await MapOrderToDtoAsync(order);
    }

    public async Task<OrderDto?> GetOrderDetailsForAdminAsync(Guid orderId, Guid tenantId)
    {
        var order = await _orderRepository.GetOrderByIdAndTenantAsync(orderId, tenantId);
        if (order == null) return null;
        return await MapOrderToDtoAsync(order);
    }

    public async Task<IEnumerable<OrderDto>> GetOrderHistoryForCustomerAsync(Guid userId, Guid tenantId)
    {
        var orders = await _orderRepository.GetOrdersByUserIdAndTenantAsync(userId, tenantId);
        var dtos = new List<OrderDto>();
        foreach (var order in orders)
        {
            dtos.Add(await MapOrderToDtoAsync(order));
        }
        return dtos;
    }

    public async Task<IEnumerable<OrderDto>> GetOrdersForAdminAsync(Guid tenantId)
    {
        var orders = await _orderRepository.GetOrdersByTenantIdAsync(tenantId);
        var dtos = new List<OrderDto>();
        foreach (var order in orders)
        {
            dtos.Add(await MapOrderToDtoAsync(order));
        }
        return dtos;
    }

    public async Task<bool> UpdateOrderStatusAsync(Guid orderId, Guid tenantId, OrderStatus newStatus, Guid adminUserId)
    {
        var order = await _orderRepository.GetOrderByIdAndTenantAsync(orderId, tenantId);
        if (order == null) return false;

        order.Status = newStatus;
        _orderRepository.UpdateOrder(order);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    private async Task<OrderDto> MapOrderToDtoAsync(Order order)
    {
        string? customerName = order.CustomerName;
        string? customerPhoneNumber = order.CustomerPhoneNumber;

        if (order.ApplicationUserId.HasValue && string.IsNullOrEmpty(customerName))
        {
            var user = await _userManager.FindByIdAsync(order.ApplicationUserId.Value.ToString());
            if (user != null)
            {
                customerName = user.Name;
                customerPhoneNumber = user.PhoneNumber;
            }
        }

        var itemDtos = order.OrderItems?.Select(oi => new OrderItemDto
        {
            ProductId = oi.ProductId,
            Quantity = oi.Quantity,
            UnitPrice = oi.UnitPrice,
            ProductName = oi.ProductName
        }).ToList() ?? new List<OrderItemDto>();

        var sequence = await _orderRepository.GetDailySequenceNumberAsync(order.TenantId, order.Id, order.OrderDate);

        return new OrderDto
        {
            Id = order.Id,
            TenantId = order.TenantId,
            ApplicationUserId = order.ApplicationUserId,
            OrderDate = order.OrderDate,
            DeliveryDate = order.DeliveryDate,
            TotalAmount = order.TotalAmount,
            Status = order.Status.ToString(),
            Items = itemDtos,
            CustomerName = customerName,
            CustomerPhoneNumber = customerPhoneNumber,
            OrderNumber = GenerateOrderNumber(sequence, order.Id)
        };
    }

    private string GenerateOrderNumber(int dailySequenceNumber, Guid orderId)
    {
        var shortGuid = orderId.ToString().Substring(orderId.ToString().Length - 4).ToUpper();
        return $"ORD-{dailySequenceNumber}-{shortGuid}";
    }

    public async Task<bool> IsProductInActiveOrderAsync(Guid productId)
    {
        return await _orderRepository.IsProductInActiveOrderAsync(productId);
    }

}