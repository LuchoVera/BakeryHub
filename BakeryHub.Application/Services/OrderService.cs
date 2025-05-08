using BakeryHub.Application.Dtos;
using BakeryHub.Application.Interfaces;
using BakeryHub.Domain.Entities;
using BakeryHub.Domain.Enums;
using BakeryHub.Domain.Interfaces;
using BakeryHub.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BakeryHub.Application.Services;

public class OrderService : IOrderService
{
    private readonly IOrderRepository _orderRepository;
    private readonly IProductRepository _productRepository;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _context;

    public OrderService(
        IOrderRepository orderRepository,
        IProductRepository productRepository,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext context)
    {
        _orderRepository = orderRepository;
        _productRepository = productRepository;
        _userManager = userManager;
        _context = context;
    }

    public async Task<OrderDto?> CreateOrderAsync(Guid tenantId, Guid applicationUserId, CreateOrderDto createOrderDto)
    {
        var tenantExists = await _context.Tenants.AnyAsync(t => t.Id == tenantId);
        if (!tenantExists)
        {
            Console.WriteLine($"Tenant not found with ID: {tenantId}");
            return null;
        }

        var user = await _userManager.FindByIdAsync(applicationUserId.ToString());
        if (user == null)
        {
            Console.WriteLine($"User not found with ID: {applicationUserId}");
            return null;
        }

        decimal verifiedTotalAmount = 0;
        var orderItems = new List<OrderItem>();

        foreach (var itemDto in createOrderDto.Items)
        {
            var product = await _productRepository.GetByIdAsync(itemDto.ProductId);
            if (product == null || !product.IsAvailable || product.TenantId != tenantId)
            {
                Console.WriteLine($"Product validation failed for ID: {itemDto.ProductId}. Found: {product != null}, Available: {product?.IsAvailable}, Belongs to Tenant: {product?.TenantId == tenantId}");
                return null;
            }

            if (product.Price != itemDto.UnitPrice)
            {
                Console.WriteLine($"Price discrepancy for product {product.Name}. Frontend: {itemDto.UnitPrice}, DB: {product.Price}. Using DB price.");
            }

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

        if (Math.Abs(verifiedTotalAmount - createOrderDto.TotalAmount) > 0.001m)
        {
            Console.WriteLine($"Total amount discrepancy. Frontend: {createOrderDto.TotalAmount}, Calculated: {verifiedTotalAmount}");
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        var todayStart = new DateTimeOffset(now.Date, TimeSpan.Zero);

        int ordersTodayCount = await _context.Orders
                                     .CountAsync(o => o.TenantId == tenantId && o.OrderDate >= todayStart);

        int nextSequenceNumber = ordersTodayCount + 1;

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
        await _context.SaveChangesAsync();

        return MapOrderToDto(order, user.Name, nextSequenceNumber);
    }

    public async Task<OrderDto?> GetOrderDetailsForCustomerAsync(Guid orderId, Guid userId, Guid tenantId)
    {
        var order = await _orderRepository.GetOrderByIdAndUserAsync(orderId, userId, tenantId);
        if (order == null) return null;
        int sequence = await GetDailySequenceNumber(order.TenantId, order.Id, order.OrderDate);
        return MapOrderToDto(order, order.User?.Name, sequence);
    }
    public async Task<OrderDto?> GetOrderDetailsForAdminAsync(Guid orderId, Guid tenantId)
    {
        var order = await _orderRepository.GetOrderByIdAndTenantAsync(orderId, tenantId);
        if (order == null) return null;
        int sequence = await GetDailySequenceNumber(order.TenantId, order.Id, order.OrderDate);
        return MapOrderToDto(order, null, sequence);
    }

    public async Task<IEnumerable<OrderDto>> GetOrderHistoryForCustomerAsync(Guid userId, Guid tenantId)
    {
        var orders = await _orderRepository.GetOrdersByUserIdAndTenantAsync(userId, tenantId);
        var user = await _userManager.FindByIdAsync(userId.ToString());
        var dtos = new List<OrderDto>();
        foreach (var order in orders)
        {
            int sequence = await GetDailySequenceNumber(order.TenantId, order.Id, order.OrderDate);
            dtos.Add(MapOrderToDto(order, user?.Name, sequence));
        }
        return dtos;
    }
    public async Task<IEnumerable<OrderDto>> GetOrdersForAdminAsync(Guid tenantId)
    {
        var orders = await _orderRepository.GetOrdersByTenantIdAsync(tenantId);
        var dtos = new List<OrderDto>();
        foreach (var order in orders)
        {
            int sequence = await GetDailySequenceNumber(order.TenantId, order.Id, order.OrderDate);
            dtos.Add(MapOrderToDto(order, order.User?.Name, sequence));
        }
        return dtos;
    }

    public async Task<bool> UpdateOrderStatusAsync(Guid orderId, Guid tenantId, OrderStatus newStatus, Guid adminUserId)
    {
        var order = await _orderRepository.GetOrderByIdAndTenantAsync(orderId, tenantId);
        if (order == null)
        {
            return false;
        }

        order.Status = newStatus;
        _orderRepository.UpdateOrder(order);
        await _context.SaveChangesAsync();
        return true;
    }

    private OrderDto MapOrderToDto(Order order, string? customerName = null, int? dailySequenceNumber = null)
    {
        var customer = order.User;

        return new OrderDto
        {
            Id = order.Id,
            TenantId = order.TenantId,
            ApplicationUserId = order.ApplicationUserId,
            OrderDate = order.OrderDate,
            DeliveryDate = order.DeliveryDate,
            TotalAmount = order.TotalAmount,
            Status = order.Status.ToString(),
            Items = order.OrderItems?.Select(oi => new OrderItemDto
            {
                ProductId = oi.ProductId,
                Quantity = oi.Quantity,
                UnitPrice = oi.UnitPrice,
                ProductName = oi.ProductName
            }).ToList() ?? new List<OrderItemDto>(),
            CustomerName = customerName ?? order.User?.Name,
            CustomerPhoneNumber = customer?.PhoneNumber,

            OrderNumber = dailySequenceNumber.HasValue
                          ? GenerateOrderNumber(dailySequenceNumber.Value, order.Id)
                          : $"ORD-{order.Id.ToString().Substring(0, 8).ToUpper()}"
        };
    }

    private string GenerateOrderNumber(int dailySequenceNumber, Guid orderId)
    {
        var shortGuid = orderId.ToString().Substring(orderId.ToString().Length - 4).ToUpper();
        return $"ORD-{dailySequenceNumber}-{shortGuid}";
    }

    private async Task<int> GetDailySequenceNumber(Guid tenantId, Guid orderId, DateTimeOffset orderDate)
    {
        var dayStart = new DateTimeOffset(orderDate.Date, TimeSpan.Zero);
        var dayEnd = dayStart.AddDays(1);

        var countBefore = await _context.Orders
                                .CountAsync(o => o.TenantId == tenantId &&
                                                 o.OrderDate >= dayStart &&
                                                 o.OrderDate < dayEnd &&
                                                 o.OrderDate < orderDate);

        return countBefore + 1;
    }
}
