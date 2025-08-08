using BakeryHub.Infrastructure.Persistence;
using BakeryHub.Modules.Catalog.Domain.Models;
using BakeryHub.Modules.Orders.Domain.Enums;
using BakeryHub.Modules.Orders.Domain.Interfaces;
using BakeryHub.Modules.Orders.Domain.Models;
using BakeryHub.Modules.Orders.Domain.Projections;
using Microsoft.EntityFrameworkCore;

namespace BakeryHub.Modules.Orders.Infrastructure.Persistence.Repositories;

public class OrderRepository : IOrderRepository
{
    private readonly ApplicationDbContext _context;

    public OrderRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddOrderAsync(Order order)
    {
        await _context.Set<Order>().AddAsync(order);
    }

    public async Task<Order?> GetOrderByIdAndTenantAsync(Guid orderId, Guid tenantId)
    {
        return await _context.Set<Order>()
            .Include(o => o.OrderItems)
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == orderId && o.TenantId == tenantId);
    }

    public async Task<Order?> GetOrderByIdAndUserAsync(Guid orderId, Guid userId, Guid tenantId)
    {
        return await _context.Set<Order>()
            .Include(o => o.OrderItems)
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == orderId && o.ApplicationUserId == userId && o.TenantId == tenantId);
    }

    public async Task<IEnumerable<Order>> GetOrdersByUserIdAndTenantAsync(Guid userId, Guid tenantId)
    {
        return await _context.Set<Order>()
            .Where(o => o.ApplicationUserId == userId && o.TenantId == tenantId)
            .Include(o => o.OrderItems)
            .OrderByDescending(o => o.OrderDate)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<IEnumerable<Order>> GetOrdersByTenantIdAsync(Guid tenantId)
    {
        return await _context.Set<Order>()
            .Where(o => o.TenantId == tenantId)
            .Include(o => o.OrderItems)
            .OrderByDescending(o => o.OrderDate)
            .AsNoTracking()
            .IgnoreQueryFilters()
            .ToListAsync();
    }

    public void UpdateOrder(Order order)
    {
        order.UpdatedAt = DateTimeOffset.UtcNow;
        _context.Set<Order>().Update(order);
    }

    public async Task<IEnumerable<Order>> GetOrdersWithItemsAndProductCategoriesAsync(Guid userId, Guid tenantId)
    {
        return await _context.Set<Order>()
            .Where(o => o.ApplicationUserId == userId && o.TenantId == tenantId && o.OrderItems.Any())
            .Include(o => o.OrderItems)
            .AsNoTracking()
            .ToListAsync();
    }

    public IQueryable<Order> GetFilteredOrdersQuery(
        Guid tenantId, DateTimeOffset startDate, DateTimeOffset endDate,
        string? filterDimension = null, string? filterValue = null)
    {
        var query = _context.Set<Order>().AsNoTracking()
            .Where(o => o.TenantId == tenantId &&
                        o.Status == OrderStatus.Received &&
                        o.OrderDate >= startDate &&
                        o.OrderDate < endDate);

        if (string.IsNullOrEmpty(filterDimension) || string.IsNullOrEmpty(filterValue))
        {
            return query;
        }

        switch (filterDimension.ToLowerInvariant())
        {
            case "dayofweek":
                if (int.TryParse(filterValue, out int dayOfWeekValue) && dayOfWeekValue >= 0 && dayOfWeekValue <= 6)
                {
                    DayOfWeek dayOfWeek = (DayOfWeek)dayOfWeekValue;
                    query = query.Where(o => o.OrderDate.DayOfWeek == dayOfWeek);
                }
                break;
            case "category":
                if (Guid.TryParse(filterValue, out Guid categoryId))
                {
                    query = query.Where(o => o.OrderItems.Any(oi => oi.Product != null && oi.Product.CategoryId == categoryId));
                }
                break;
            case "product":
                if (Guid.TryParse(filterValue, out Guid productId))
                {
                    query = query.Where(o => o.OrderItems.Any(oi => oi.ProductId == productId));
                }
                break;
            case "status":
                if (Enum.TryParse<OrderStatus>(filterValue, true, out OrderStatus status))
                {
                    query = query.Where(o => o.Status == status);
                }
                break;
            case "customer":
                if (Guid.TryParse(filterValue, out Guid customerId))
                {
                    query = query.Where(o => o.ApplicationUserId == customerId);
                }
                break;
        }
        return query;
    }

    public async Task<(decimal TotalRevenue, int TotalOrders, int TotalUniqueCustomers)> GetOverallSummaryAsync(
        Guid tenantId, DateTimeOffset startDate, DateTimeOffset endDate, IQueryable<Order> preFilteredOrdersQuery)
    {
        var summary = await preFilteredOrdersQuery
            .GroupBy(o => 1)
            .Select(g => new
            {
                TotalOrders = g.Count(),
                TotalRevenue = g.Sum(o => o.TotalAmount),
                TotalUniqueCustomers = g.Select(o => o.ApplicationUserId).Distinct().Count()
            })
            .FirstOrDefaultAsync();

        return summary != null ? (summary.TotalRevenue, summary.TotalOrders, summary.TotalUniqueCustomers) : (0, 0, 0);
    }

    public async Task<List<OrderAggregationByTime>> GetOrdersAggregatedByTimeDimensionAsync(
        Guid tenantId, DateTimeOffset startDate, DateTimeOffset endDate, string timeDimension, IQueryable<Order> preFilteredOrdersQuery)
    {
        var filteredOrders = await preFilteredOrdersQuery
            .Select(o => new { o.OrderDate, o.TotalAmount })
            .ToListAsync();

        switch (timeDimension.ToLowerInvariant())
        {
            case "day":
                return filteredOrders
                    .GroupBy(o => o.OrderDate.Date)
                    .Select(g => new OrderAggregationByTime(g.Key.ToString("yyyy-MM-dd"), g.Sum(o => o.TotalAmount), g.Count()))
                    .OrderBy(r => r.PeriodLabel)
                    .ToList();
            case "month":
                return filteredOrders
                    .GroupBy(o => new { o.OrderDate.Year, o.OrderDate.Month })
                    .Select(g => new OrderAggregationByTime($"{g.Key.Year}-{g.Key.Month:D2}", g.Sum(o => o.TotalAmount), g.Count()))
                    .OrderBy(r => r.PeriodLabel)
                    .ToList();
            case "year":
                return filteredOrders
                    .GroupBy(o => o.OrderDate.Year)
                    .Select(g => new OrderAggregationByTime(g.Key.ToString(), g.Sum(o => o.TotalAmount), g.Count()))
                    .OrderBy(r => r.PeriodLabel)
                    .ToList();
            case "dayofweek":
                return Enum.GetValues(typeof(DayOfWeek))
                    .Cast<DayOfWeek>()
                    .Select(day =>
                    {
                        var salesOnDay = filteredOrders.Where(o => o.OrderDate.DayOfWeek == day);
                        return new OrderAggregationByTime(
                            day.ToString(),
                            salesOnDay.Sum(o => o.TotalAmount),
                            salesOnDay.Count()
                        );
                    })
                    .ToList();
            default:
                return new List<OrderAggregationByTime>();
        }
    }

    public async Task<List<OrderAggregationByEntity>> GetOrdersAggregatedByCategoryAsync(
        Guid tenantId, DateTimeOffset startDate, DateTimeOffset endDate, IQueryable<Order> preFilteredOrdersQuery)
    {
        var ordersInPeriod = await preFilteredOrdersQuery.Select(o => o.Id).ToListAsync();
        if (!ordersInPeriod.Any()) return new List<OrderAggregationByEntity>();

        var itemsData = await _context.Set<OrderItem>()
            .AsNoTracking()
            .Where(oi => ordersInPeriod.Contains(oi.OrderId))
            .Select(oi => new
            {
                oi.OrderId,
                CategoryExists = oi.Product != null && oi.Product.Category != null,
                CategoryId = oi.Product != null ? oi.Product.CategoryId : Guid.Empty,
                CategoryName = oi.Product != null && oi.Product.Category != null ? oi.Product.Category.Name : "Sin Categoría",
                Subtotal = oi.Quantity * oi.UnitPrice
            })
            .ToListAsync();

        return itemsData
            .Where(d => d.CategoryExists)
            .GroupBy(d => new { d.CategoryId, Name = d.CategoryName })
            .Select(g => new OrderAggregationByEntity(
                g.Key.CategoryId,
                g.Key.Name,
                g.Sum(d => d.Subtotal),
                g.Select(d => d.OrderId).Distinct().Count()
            ))
            .OrderByDescending(r => r.TotalAmount)
            .ToList();
    }

    public async Task<List<OrderAggregationByEntity>> GetOrdersAggregatedByProductAsync(
        Guid tenantId, DateTimeOffset startDate, DateTimeOffset endDate, IQueryable<Order> preFilteredOrdersQuery, bool includeProductsWithNoSales)
    {
        var ordersInPeriod = await preFilteredOrdersQuery.Select(o => o.Id).ToListAsync();

        if (!includeProductsWithNoSales)
        {
            if (!ordersInPeriod.Any()) return new List<OrderAggregationByEntity>();

            var itemsData = await _context.Set<OrderItem>()
                .AsNoTracking()
                .Where(oi => ordersInPeriod.Contains(oi.OrderId) && oi.Product != null)
                .Select(oi => new
                {
                    oi.ProductId,
                    ProductName = oi.Product!.Name,
                    Subtotal = oi.Quantity * oi.UnitPrice,
                    oi.OrderId
                })
                .ToListAsync();

            return itemsData
                .GroupBy(d => new { d.ProductId, d.ProductName })
                .Select(g => new OrderAggregationByEntity(
                    g.Key.ProductId,
                    g.Key.ProductName,
                    g.Sum(d => d.Subtotal),
                    g.Select(d => d.OrderId).Distinct().Count()
                ))
                .OrderByDescending(r => r.TotalAmount)
                .ToList();
        }
        else
        {
            var salesData = new Dictionary<Guid, (decimal TotalAmount, int OrderCount)>();
            if (ordersInPeriod.Any())
            {
                var soldProductsData = await _context.Set<OrderItem>()
                    .AsNoTracking()
                    .Where(oi => ordersInPeriod.Contains(oi.OrderId))
                    .GroupBy(oi => oi.ProductId)
                    .Select(g => new
                    {
                        ProductId = g.Key,
                        TotalAmount = g.Sum(oi => oi.Quantity * oi.UnitPrice),
                        OrderCount = g.Select(oi => oi.OrderId).Distinct().Count()
                    })
                    .ToListAsync();

                salesData = soldProductsData.ToDictionary(d => d.ProductId, d => (d.TotalAmount, d.OrderCount));
            }

            var allProducts = await _context.Set<Product>()
                .AsNoTracking()
                .Where(p => p.TenantId == tenantId)
                .ToListAsync();

            return allProducts
                .Select(p => new OrderAggregationByEntity(
                    p.Id,
                    p.Name,
                    salesData.GetValueOrDefault(p.Id, (TotalAmount: 0m, OrderCount: 0)).TotalAmount,
                    salesData.GetValueOrDefault(p.Id, (TotalAmount: 0m, OrderCount: 0)).OrderCount
                ))
                .OrderByDescending(r => r.TotalAmount)
                .ToList();
        }
    }

    public async Task<List<OrderAggregationByStatus>> GetOrdersAggregatedByStatusAsync(
        Guid tenantId, DateTimeOffset startDate, DateTimeOffset endDate, IQueryable<Order> preFilteredOrdersQuery)
    {
        var filteredOrders = await preFilteredOrdersQuery
            .Select(o => new { o.Status, o.TotalAmount })
            .ToListAsync();

        return filteredOrders
            .GroupBy(o => o.Status)
            .Select(g => new OrderAggregationByStatus(
                g.Key,
                g.Sum(o => o.TotalAmount),
                g.Count()
            ))
            .OrderBy(r => r.Status)
            .ToList();
    }

    public async Task<List<OrderAggregationByEntity>> GetOrdersAggregatedByCustomerAsync(
    Guid tenantId, DateTimeOffset startDate, DateTimeOffset endDate, IQueryable<Order> preFilteredOrdersQuery)
    {
        var ordersData = await preFilteredOrdersQuery
            .Where(o => o.ApplicationUserId.HasValue)
            .Select(o => new
            {
                CustomerId = o.ApplicationUserId!.Value,
                CustomerName = o.CustomerName ?? "Customer",
                o.TotalAmount
            })
            .ToListAsync();

        return ordersData
            .GroupBy(d => new { d.CustomerId, Name = d.CustomerName })
            .Select(g => new OrderAggregationByEntity(
                g.Key.CustomerId,
                g.Key.Name,
                g.Sum(d => d.TotalAmount),
                g.Count()
            ))
            .OrderByDescending(r => r.TotalAmount)
            .ToList();
    }

    public async Task<int> GetDailySequenceNumberAsync(Guid tenantId, Guid orderId, DateTimeOffset orderDate)
    {
        var dayStart = new DateTimeOffset(orderDate.Date, TimeSpan.Zero);
        var dayEnd = dayStart.AddDays(1);

        var countBefore = await _context.Set<Order>()
                                .CountAsync(o => o.TenantId == tenantId &&
                                                 o.OrderDate >= dayStart &&
                                                 o.OrderDate < dayEnd &&
                                                 o.OrderDate < orderDate);
        return countBefore + 1;
    }

    public async Task<bool> IsProductInActiveOrderAsync(Guid productId)
    {
        var activeStatuses = new[] { OrderStatus.Pending, OrderStatus.Confirmed, OrderStatus.Preparing };
        return await _context.Set<OrderItem>()
            .AnyAsync(oi => oi.ProductId == productId && activeStatuses.Contains(oi.Order.Status));
    }
}
