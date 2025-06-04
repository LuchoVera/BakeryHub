using BakeryHub.Application.Dtos;
using BakeryHub.Application.Dtos.Dashboard;
using BakeryHub.Application.Interfaces;
using BakeryHub.Domain.Entities;
using BakeryHub.Domain.Enums;
using BakeryHub.Domain.Interfaces;
using BakeryHub.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace BakeryHub.Application.Services;

public class OrderService : IOrderService
{
    private readonly IOrderRepository _orderRepository;
    private readonly IProductRepository _productRepository;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _context;
    private readonly ICategoryRepository _categoryRepository;

    public OrderService(
        IOrderRepository orderRepository,
        IProductRepository productRepository,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext context,
        ICategoryRepository categoryRepository)
    {
        _orderRepository = orderRepository;
        _productRepository = productRepository;
        _userManager = userManager;
        _context = context;
        _categoryRepository = categoryRepository;
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
    public async Task<DashboardResponseDto> GetDashboardStatisticsAsync(Guid tenantId, DashboardQueryParametersDto queryParams)
    {
        (DateTimeOffset startDate, DateTimeOffset endDate, string periodDescription) = CalculateDateRangeAndDescription(queryParams);

        var filteredOrdersQuery = _orderRepository.GetFilteredOrdersQuery(tenantId, startDate, endDate, queryParams.FilterDimension, queryParams.FilterValue);

        var (totalRevenue, totalOrders, totalUniqueCustomers) = await _orderRepository.GetOverallSummaryAsync(tenantId, startDate, endDate, filteredOrdersQuery);
        var summaryDto = new AggregatedDataSummaryDto
        {
            TotalRevenue = totalRevenue,
            TotalOrders = totalOrders,
            TotalCustomers = totalUniqueCustomers
        };

        List<TimeSeriesDataPointDto> breakdownDto = new List<TimeSeriesDataPointDto>();
        string effectiveBreakdownDimension = queryParams.BreakdownDimension ?? queryParams.Granularity;
        string metric = queryParams.Metric?.ToLowerInvariant() ?? "revenue";


        if (!string.IsNullOrEmpty(effectiveBreakdownDimension) && effectiveBreakdownDimension.ToLowerInvariant() != "none")
        {
            switch (effectiveBreakdownDimension.ToLowerInvariant())
            {
                case "day":
                case "month":
                case "year":
                    var timeAggregations = await _orderRepository.GetOrdersAggregatedByTimeDimensionAsync(tenantId, startDate, endDate, effectiveBreakdownDimension.ToLowerInvariant(), filteredOrdersQuery);
                    breakdownDto = timeAggregations.Select(agg => new TimeSeriesDataPointDto
                    {
                        Label = agg.PeriodLabel,
                        Value = metric == "revenue" ? agg.TotalAmount : agg.OrderCount,
                        Count = agg.OrderCount
                    }).ToList();
                    break;
                case "category":
                    var categoryAggregations = await _orderRepository.GetOrdersAggregatedByCategoryAsync(tenantId, startDate, endDate, filteredOrdersQuery);
                    breakdownDto = categoryAggregations.Select(agg => new TimeSeriesDataPointDto
                    {
                        Id = agg.EntityId,
                        Label = agg.EntityName,
                        Value = metric == "revenue" ? agg.TotalAmount : agg.OrderCount,
                        Count = agg.OrderCount
                    }).ToList();
                    break;
                case "product":
                    var productAggregations = await _orderRepository.GetOrdersAggregatedByProductAsync(tenantId, startDate, endDate, filteredOrdersQuery);
                    breakdownDto = productAggregations.Select(agg => new TimeSeriesDataPointDto
                    {
                        Id = agg.EntityId,
                        Label = agg.EntityName,
                        Value = metric == "revenue" ? agg.TotalAmount : agg.OrderCount,
                        Count = agg.OrderCount
                    }).ToList();
                    break;
                case "status":
                    var statusAggregations = await _orderRepository.GetOrdersAggregatedByStatusAsync(tenantId, startDate, endDate, filteredOrdersQuery);
                    breakdownDto = statusAggregations.Select(agg => new TimeSeriesDataPointDto
                    {
                        Label = agg.Status.ToString(),
                        Value = metric == "revenue" ? agg.TotalAmount : agg.OrderCount,
                        Count = agg.OrderCount
                    }).ToList();
                    break;
                case "customer":
                    var customerAggregations = await _orderRepository.GetOrdersAggregatedByCustomerAsync(tenantId, startDate, endDate, filteredOrdersQuery);
                    breakdownDto = customerAggregations.Select(agg => new TimeSeriesDataPointDto
                    {
                        Id = agg.EntityId,
                        Label = agg.EntityName,
                        Value = metric == "revenue" ? agg.TotalAmount : agg.OrderCount,
                        Count = agg.OrderCount
                    }).ToList();
                    break;
            }
        }

        string title = await BuildResponseTitleAsync(queryParams, tenantId);
        var response = new DashboardResponseDto
        {
            Title = title,
            PeriodDescription = periodDescription,
            Summary = summaryDto,
            Breakdown = breakdownDto,
            NextDrillOptions = GenerateNextDrillOptions(queryParams)
        };

        return response;
    }

    private async Task<string> BuildResponseTitleAsync(DashboardQueryParametersDto queryParams, Guid tenantId)
    {
        var culture = CultureInfo.CurrentCulture;
        string metricName = queryParams.Metric.ToLowerInvariant() switch
        {
            "revenue" => "Ingresos",
            "ordercount" => "Cantidad de Órdenes",
            _ => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(queryParams.Metric)
        };

        string title = metricName;
        string effectiveBreakdownDimension = queryParams.BreakdownDimension ?? queryParams.Granularity;


        if (!string.IsNullOrEmpty(effectiveBreakdownDimension) && effectiveBreakdownDimension.ToLowerInvariant() != "none")
        {
            string breakdownName = effectiveBreakdownDimension.ToLowerInvariant() switch {
                "day" => "Diario", "month" => "Mensual", "year" => "Anual",
                "category" => "por Categoría", "product" => "por Producto",
                "status" => "por Estado", "customer" => "por Cliente",
                _ => $"por {culture.TextInfo.ToTitleCase(effectiveBreakdownDimension)}"
            };
            title += $" {breakdownName}";
        }

        if (!string.IsNullOrEmpty(queryParams.FilterDimension) && !string.IsNullOrEmpty(queryParams.FilterValue))
        {
            string filterDimDisplay = queryParams.FilterDimension.ToLowerInvariant() switch {
                 "category" => "Categoría", "product" => "Producto",
                 "status" => "Estado", "customer" => "Cliente",
                 "year" => "Año", "month" => "Mes", "day" => "Día",
                 _ => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(queryParams.FilterDimension)
            };

            string filterValueDisplay = queryParams.FilterValue;
            if (Guid.TryParse(queryParams.FilterValue, out Guid entityGuid))
            {
                filterValueDisplay = queryParams.FilterDimension.ToLowerInvariant() switch
                {
                    "category" => (await _categoryRepository.GetByIdAndTenantAsync(entityGuid, tenantId))?.Name ?? queryParams.FilterValue,
                    "product" => (await _productRepository.GetByIdAsync(entityGuid))?.Name ?? queryParams.FilterValue,
                    "customer" => (await _userManager.FindByIdAsync(queryParams.FilterValue))?.Name ?? queryParams.FilterValue,
                    _ => queryParams.FilterValue
                };
            } else if (queryParams.FilterDimension.ToLowerInvariant() == "month") {
                 if (DateTimeOffset.TryParseExact(queryParams.FilterValue, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var monthDate))
                 {
                    filterValueDisplay = monthDate.ToString("MMMM yyyy", CultureInfo.CreateSpecificCulture("es-ES")); // ej. "junio 2025"
                 }
            } else if (queryParams.FilterDimension.ToLowerInvariant() == "day") {
                 if (DateTimeOffset.TryParseExact(queryParams.FilterValue, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dayDate))
                 {
                    filterValueDisplay = dayDate.ToString("d 'de' MMMM 'de' yyyy", CultureInfo.CreateSpecificCulture("es-ES")); // ej. "3 de junio de 2025"
                 }
            }
            title += $" (Filtro: {filterDimDisplay} - {filterValueDisplay})";
        }
        return title;
    }

    private List<AvailableDrillOptionDto> GenerateNextDrillOptions(DashboardQueryParametersDto currentParams)
    {
        var options = new List<AvailableDrillOptionDto>();
        string currentBreakdownDim = (currentParams.BreakdownDimension ?? currentParams.Granularity)?.ToLowerInvariant() ?? string.Empty;
        string currentFilterDim = currentParams.FilterDimension?.ToLowerInvariant() ?? string.Empty;

        if (currentBreakdownDim == "year")
            options.Add(new AvailableDrillOptionDto { DimensionName = "month", DisplayName = "Ver por Mes", TargetGranularity = "monthly" });
        else if (currentBreakdownDim == "month")
            options.Add(new AvailableDrillOptionDto { DimensionName = "day", DisplayName = "Ver por Día", TargetGranularity = "daily" });

        if (currentBreakdownDim != "category" && currentFilterDim != "category" && currentBreakdownDim != "product" && currentFilterDim != "product")
            options.Add(new AvailableDrillOptionDto { DimensionName = "category", DisplayName = "Desglosar por Categoría" });

        if (currentBreakdownDim == "category" && currentFilterDim != "product")
            options.Add(new AvailableDrillOptionDto { DimensionName = "product", DisplayName = "Desglosar por Producto" });

        if (currentBreakdownDim != "status" && currentFilterDim != "status")
            options.Add(new AvailableDrillOptionDto { DimensionName = "status", DisplayName = "Desglosar por Estado" });

        if (currentBreakdownDim != "customer" && currentFilterDim != "customer")
            options.Add(new AvailableDrillOptionDto { DimensionName = "customer", DisplayName = "Desglosar por Cliente" });
        
        bool noEntityBreakdown = string.IsNullOrEmpty(currentParams.BreakdownDimension) || 
                                 currentBreakdownDim == "day" || currentBreakdownDim == "month" || currentBreakdownDim == "year" || currentBreakdownDim == "none";
        bool noEntityFilter = string.IsNullOrEmpty(currentFilterDim) || 
                              !(new[]{"category","product","status","customer"}.Contains(currentFilterDim));

        if (noEntityBreakdown && noEntityFilter) {
            string currentGranularityLower = currentParams.Granularity?.ToLowerInvariant() ?? "none";
            if (currentGranularityLower != "yearly" && currentGranularityLower != "monthly" && currentGranularityLower != "daily") {
                 if(!options.Any(o => o.DimensionName == "year")) options.Add(new AvailableDrillOptionDto { DimensionName = "year", DisplayName = "Ver por Año", TargetGranularity = "yearly" });
            }
        }
        return options.DistinctBy(o => o.DimensionName).ToList();
    }

    private (DateTimeOffset startDate, DateTimeOffset endDate, string periodDescription) CalculateDateRangeAndDescription(DashboardQueryParametersDto queryParams)
    {
        DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
        TimeZoneInfo systemTimeZone;
        try {
            systemTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SA Western Standard Time"); // Bolivia Time
        } catch (TimeZoneNotFoundException) {
            systemTimeZone = TimeZoneInfo.Local; // Fallback
        }
        DateTimeOffset nowInLocalTime = TimeZoneInfo.ConvertTime(nowUtc, systemTimeZone);

        DateTimeOffset startDate = DateTimeOffset.MinValue;
        DateTimeOffset endDate = DateTimeOffset.MaxValue;
        string periodDesc = "";
        bool useTimePeriodForRangeCalculation = true;

        if (!string.IsNullOrEmpty(queryParams.FilterDimension) && !string.IsNullOrEmpty(queryParams.FilterValue))
        {
            string filterDimLower = queryParams.FilterDimension.ToLowerInvariant();
            if (filterDimLower == "year" && int.TryParse(queryParams.FilterValue, out int year))
            {
                var localYearStart = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
                startDate = new DateTimeOffset(localYearStart, systemTimeZone.GetUtcOffset(localYearStart));
                endDate = startDate.AddYears(1);
                periodDesc = $"Año {year}";
                useTimePeriodForRangeCalculation = false;
            }
            else if (filterDimLower == "month" && DateTimeOffset.TryParseExact(queryParams.FilterValue, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var monthDateVal))
            {
                var localMonthStart = new DateTime(monthDateVal.Year, monthDateVal.Month, 1, 0,0,0, DateTimeKind.Unspecified);
                startDate = new DateTimeOffset(localMonthStart, systemTimeZone.GetUtcOffset(localMonthStart));
                endDate = startDate.AddMonths(1);
                periodDesc = startDate.ToString("MMMM yyyy", CultureInfo.CreateSpecificCulture("es-ES"));
                useTimePeriodForRangeCalculation = false;
            }
            else if (filterDimLower == "day" && DateTimeOffset.TryParseExact(queryParams.FilterValue, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dayDateVal))
            {
                var localDayStart = new DateTime(dayDateVal.Year, dayDateVal.Month, dayDateVal.Day, 0,0,0, DateTimeKind.Unspecified);
                startDate = new DateTimeOffset(localDayStart, systemTimeZone.GetUtcOffset(localDayStart));
                endDate = startDate.AddDays(1);
                periodDesc = startDate.ToString("d 'de' MMMM 'de' yyyy", CultureInfo.CreateSpecificCulture("es-ES"));
                useTimePeriodForRangeCalculation = false;
            }
        }

        if (useTimePeriodForRangeCalculation)
        {
            var todayLocal = nowInLocalTime.Date;
            switch (queryParams.TimePeriod?.ToLowerInvariant())
            {
                case "last7days":
                    endDate = new DateTimeOffset(todayLocal, systemTimeZone.GetUtcOffset(nowInLocalTime)).AddDays(1);
                    startDate = endDate.AddDays(-7);
                    periodDesc = "Últimos 7 Días";
                    break;
                case "last30days":
                    endDate = new DateTimeOffset(todayLocal, systemTimeZone.GetUtcOffset(nowInLocalTime)).AddDays(1);
                    startDate = endDate.AddDays(-30);
                    periodDesc = "Últimos 30 Días";
                    break;
                case "currentmonth":
                    var firstDayCurrentMonth = new DateTime(todayLocal.Year, todayLocal.Month, 1);
                    startDate = new DateTimeOffset(firstDayCurrentMonth, systemTimeZone.GetUtcOffset(nowInLocalTime));
                    endDate = startDate.AddMonths(1);
                    periodDesc = startDate.ToString("MMMM yyyy", CultureInfo.CreateSpecificCulture("es-ES"));
                    break;
                case "lastmonth":
                    var currentMonthStart = new DateTime(todayLocal.Year, todayLocal.Month, 1);
                    var firstDayLastMonth = new DateTimeOffset(currentMonthStart, systemTimeZone.GetUtcOffset(nowInLocalTime)).AddMonths(-1);
                    startDate = firstDayLastMonth;
                    endDate = firstDayLastMonth.AddMonths(1);
                    periodDesc = startDate.ToString("MMMM yyyy", CultureInfo.CreateSpecificCulture("es-ES"));
                    break;
                case "yeartodate":
                    var firstDayYear = new DateTime(todayLocal.Year, 1, 1);
                    startDate = new DateTimeOffset(firstDayYear, systemTimeZone.GetUtcOffset(nowInLocalTime));
                    endDate = new DateTimeOffset(todayLocal, systemTimeZone.GetUtcOffset(nowInLocalTime)).AddDays(1);
                    periodDesc = $"Año Actual ({todayLocal.Year})";
                    break;
                case "customrange":
                    if (!queryParams.CustomStartDate.HasValue || !queryParams.CustomEndDate.HasValue)
                    {
                        endDate = new DateTimeOffset(todayLocal, systemTimeZone.GetUtcOffset(nowInLocalTime)).AddDays(1);
                        startDate = endDate.AddDays(-7); 
                        periodDesc = "Últimos 7 Días (Rango Personalizado Inválido)";
                    }
                    else
                    {
                        var customStartLocal = queryParams.CustomStartDate.Value.Date;
                        var customEndLocal = queryParams.CustomEndDate.Value.Date;
                        startDate = new DateTimeOffset(customStartLocal, systemTimeZone.GetUtcOffset(customStartLocal));
                        endDate = new DateTimeOffset(customEndLocal, systemTimeZone.GetUtcOffset(customEndLocal)).AddDays(1);
                        periodDesc = $"Desde {startDate.ToString("d/MM/yy", CultureInfo.InvariantCulture)} hasta {queryParams.CustomEndDate.Value.ToString("d/MM/yy", CultureInfo.InvariantCulture)}";
                    }
                    break;
                default:
                    endDate = new DateTimeOffset(todayLocal, systemTimeZone.GetUtcOffset(nowInLocalTime)).AddDays(1);
                    startDate = endDate.AddDays(-7);
                    periodDesc = "Últimos 7 Días (Predeterminado)";
                    break;
            }
        }
        return (startDate.ToUniversalTime(), endDate.ToUniversalTime(), periodDesc);
    }
}
