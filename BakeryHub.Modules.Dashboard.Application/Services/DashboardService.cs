using BakeryHub.Modules.Accounts.Domain.Models;
using BakeryHub.Modules.Catalog.Domain.Interfaces;
using BakeryHub.Modules.Dashboard.Application.Dtos.Dashboard;
using BakeryHub.Modules.Dashboard.Application.Interfaces;
using BakeryHub.Modules.Orders.Domain.Interfaces;
using Microsoft.AspNetCore.Identity;
using System.Globalization;

namespace BakeryHub.Modules.Dashboard.Application.Services;

public class DashboardService : IDashboardService
{
    private readonly IOrderRepository _orderRepository;
    private readonly IProductRepository _productRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly UserManager<ApplicationUser> _userManager;

    public DashboardService(
        IOrderRepository orderRepository,
        IProductRepository productRepository,
        ICategoryRepository categoryRepository,
        UserManager<ApplicationUser> userManager)
    {
        _orderRepository = orderRepository;
        _productRepository = productRepository;
        _categoryRepository = categoryRepository;
        _userManager = userManager;
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
        string effectiveBreakdownDimension = (queryParams.BreakdownDimension ?? queryParams.Granularity)?.ToLowerInvariant() ?? "none";
        string metric = queryParams.Metric?.ToLowerInvariant() ?? "revenue";

        if (!string.IsNullOrEmpty(effectiveBreakdownDimension) && effectiveBreakdownDimension.ToLowerInvariant() != "none")
        {
            switch (effectiveBreakdownDimension.ToLowerInvariant())
            {
                case "day":
                case "month":
                case "year":
                case "dayofweek":
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
                    var productAggregations = await _orderRepository.GetOrdersAggregatedByProductAsync(tenantId, startDate, endDate, filteredOrdersQuery, queryParams.IncludeProductsWithNoSales);
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
        var culture = CultureInfo.InvariantCulture;
        string metricName = queryParams.Metric.ToLowerInvariant() switch
        {
            "revenue" => "Revenue",
            "ordercount" => "Order Count",
            _ => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(queryParams.Metric)
        };

        string title = metricName;
        string effectiveBreakdownDimension = (queryParams.BreakdownDimension ?? queryParams.Granularity)?.ToLowerInvariant() ?? "none";

        if (!string.IsNullOrEmpty(effectiveBreakdownDimension) && effectiveBreakdownDimension.ToLowerInvariant() != "none")
        {
            string breakdownName = effectiveBreakdownDimension.ToLowerInvariant() switch
            {
                "day" => "by Day",
                "month" => "by Month",
                "year" => "by Year",
                "dayofweek" => "by Day of Week",
                "category" => "by Category",
                "product" => "by Product",
                "status" => "by Status",
                "customer" => "by Customer",
                _ => $"by {culture.TextInfo.ToTitleCase(effectiveBreakdownDimension)}"
            };
            title += $" {breakdownName}";
        }

        if (!string.IsNullOrEmpty(queryParams.FilterDimension) && !string.IsNullOrEmpty(queryParams.FilterValue))
        {
            string filterDimDisplay = queryParams.FilterDimension.ToLowerInvariant() switch
            {
                "category" => "Category",
                "product" => "Product",
                "status" => "Status",
                "customer" => "Customer",
                "year" => "Year",
                "month" => "Month",
                "day" => "Day",
                "dayofweek" => "Day of Week",
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
            }
            else if (queryParams.FilterDimension.ToLowerInvariant() == "month")
            {
                if (DateTimeOffset.TryParseExact(queryParams.FilterValue, "yyyy-MM", culture, DateTimeStyles.None, out var monthDate))
                {
                    filterValueDisplay = monthDate.ToString("MMMM yyyy", culture);
                }
            }
            else if (queryParams.FilterDimension.ToLowerInvariant() == "day")
            {
                if (DateTimeOffset.TryParseExact(queryParams.FilterValue, "yyyy-MM-dd", culture, DateTimeStyles.None, out var dayDate))
                {
                    filterValueDisplay = dayDate.ToString("MMMM dd, yyyy", culture);
                }
            }
            title += $" (Filter: {filterDimDisplay} - {filterValueDisplay})";
        }
        return title;
    }

    private List<AvailableDrillOptionDto> GenerateNextDrillOptions(DashboardQueryParametersDto currentParams)
    {
        var options = new List<AvailableDrillOptionDto>();
        string currentBreakdownDim = (currentParams.BreakdownDimension ?? currentParams.Granularity)?.ToLowerInvariant() ?? string.Empty;
        string currentFilterDim = currentParams.FilterDimension?.ToLowerInvariant() ?? string.Empty;

        if (currentBreakdownDim == "year")
            options.Add(new AvailableDrillOptionDto { DimensionName = "month", DisplayName = "View by Month", TargetGranularity = "monthly" });
        else if (currentBreakdownDim == "month")
            options.Add(new AvailableDrillOptionDto { DimensionName = "day", DisplayName = "View by Day", TargetGranularity = "daily" });
        else if (currentBreakdownDim == "dayofweek")
            options.Add(new AvailableDrillOptionDto { DimensionName = "day", DisplayName = "View Specific Days", TargetGranularity = "daily" });
        if (currentBreakdownDim != "category" && currentFilterDim != "category" && currentBreakdownDim != "product" && currentFilterDim != "product")
            options.Add(new AvailableDrillOptionDto { DimensionName = "category", DisplayName = "Breakdown by Category" });

        if (currentBreakdownDim == "category" && currentFilterDim != "product")
            options.Add(new AvailableDrillOptionDto { DimensionName = "product", DisplayName = "Breakdown by product" });

        if (currentBreakdownDim != "status" && currentFilterDim != "status")
            options.Add(new AvailableDrillOptionDto { DimensionName = "status", DisplayName = "Breakdown by status" });

        if (currentBreakdownDim != "customer" && currentFilterDim != "customer")
            options.Add(new AvailableDrillOptionDto { DimensionName = "customer", DisplayName = "Breakdown by customer" });

        bool noEntityBreakdown = string.IsNullOrEmpty(currentParams.BreakdownDimension) ||
                                 currentBreakdownDim == "day" || currentBreakdownDim == "month" || currentBreakdownDim == "year" || currentBreakdownDim == "none";
        bool noEntityFilter = string.IsNullOrEmpty(currentFilterDim) ||
                              !(new[] { "category", "product", "status", "customer" }.Contains(currentFilterDim));

        if (noEntityBreakdown && noEntityFilter)
        {
            string currentGranularityLower = currentParams.Granularity?.ToLowerInvariant() ?? "none";
            if (currentGranularityLower != "yearly" && currentGranularityLower != "monthly" && currentGranularityLower != "daily")
            {
                if (!options.Any(o => o.DimensionName == "year")) options.Add(new AvailableDrillOptionDto { DimensionName = "year", DisplayName = "View by Year", TargetGranularity = "yearly" });
            }
        }
        return options.DistinctBy(o => o.DimensionName).ToList();
    }

    private (DateTimeOffset startDate, DateTimeOffset endDate, string periodDescription) CalculateDateRangeAndDescription(DashboardQueryParametersDto queryParams)
    {
        DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
        TimeZoneInfo systemTimeZone;
        try
        {
            systemTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SA Western Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            systemTimeZone = TimeZoneInfo.Local;
        }
        DateTimeOffset nowInLocalTime = TimeZoneInfo.ConvertTime(nowUtc, systemTimeZone);

        DateTimeOffset startDate = DateTimeOffset.MinValue;
        DateTimeOffset endDate = DateTimeOffset.MaxValue;
        string periodDesc = "";
        bool useTimePeriodForRangeCalculation = true;

        var culture = CultureInfo.InvariantCulture;
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
                var localMonthStart = new DateTime(monthDateVal.Year, monthDateVal.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
                startDate = new DateTimeOffset(localMonthStart, systemTimeZone.GetUtcOffset(localMonthStart));
                endDate = startDate.AddMonths(1);
                periodDesc = startDate.ToString("MMMM yyyy", CultureInfo.CreateSpecificCulture("es-ES"));
                useTimePeriodForRangeCalculation = false;
            }
            else if (filterDimLower == "day" && DateTimeOffset.TryParseExact(queryParams.FilterValue, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dayDateVal))
            {
                var localDayStart = new DateTime(dayDateVal.Year, dayDateVal.Month, dayDateVal.Day, 0, 0, 0, DateTimeKind.Unspecified);
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
                    periodDesc = "Last 7 Days";
                    break;
                case "last30days":
                    endDate = new DateTimeOffset(todayLocal, systemTimeZone.GetUtcOffset(nowInLocalTime)).AddDays(1);
                    startDate = endDate.AddDays(-30);
                    periodDesc = "Last 30 Days";
                    break;
                case "currentmonth":
                    var firstDayCurrentMonth = new DateTime(todayLocal.Year, todayLocal.Month, 1);
                    startDate = new DateTimeOffset(firstDayCurrentMonth, systemTimeZone.GetUtcOffset(nowInLocalTime));
                    endDate = startDate.AddMonths(1);
                    periodDesc = startDate.ToString("MMMM yyyy", culture);
                    break;
                case "lastmonth":
                    var currentMonthStart = new DateTime(todayLocal.Year, todayLocal.Month, 1);
                    var firstDayLastMonth = new DateTimeOffset(currentMonthStart, systemTimeZone.GetUtcOffset(nowInLocalTime)).AddMonths(-1);
                    startDate = firstDayLastMonth;
                    endDate = firstDayLastMonth.AddMonths(1);
                    periodDesc = startDate.ToString("MMMM yyyy", culture);
                    break;
                case "yeartodate":
                    var firstDayYear = new DateTime(todayLocal.Year, 1, 1);
                    startDate = new DateTimeOffset(firstDayYear, systemTimeZone.GetUtcOffset(nowInLocalTime));
                    endDate = new DateTimeOffset(todayLocal, systemTimeZone.GetUtcOffset(nowInLocalTime)).AddDays(1);
                    periodDesc = $"Year to Date ({todayLocal.Year})";
                    break;
                case "customrange":
                    if (!queryParams.CustomStartDate.HasValue || !queryParams.CustomEndDate.HasValue)
                    {
                        endDate = new DateTimeOffset(todayLocal, systemTimeZone.GetUtcOffset(nowInLocalTime)).AddDays(1);
                        startDate = endDate.AddDays(-7);
                        periodDesc = "Last 7 Days (Invalid Custom Range)";
                    }
                    else
                    {
                        var customStartLocal = queryParams.CustomStartDate.Value.Date;
                        var customEndLocal = queryParams.CustomEndDate.Value.Date;
                        startDate = new DateTimeOffset(customStartLocal, systemTimeZone.GetUtcOffset(customStartLocal));
                        endDate = new DateTimeOffset(customEndLocal, systemTimeZone.GetUtcOffset(customEndLocal)).AddDays(1);
                        periodDesc = $"From {startDate.ToString("d/MM/yy", culture)} to {queryParams.CustomEndDate.Value.ToString("d/MM/yy", culture)}";
                    }
                    break;
                default:
                    endDate = new DateTimeOffset(todayLocal, systemTimeZone.GetUtcOffset(nowInLocalTime)).AddDays(1);
                    startDate = endDate.AddDays(-7);
                    periodDesc = "Last 7 Days (Default)";
                    break;
            }
        }
        return (startDate.ToUniversalTime(), endDate.ToUniversalTime(), periodDesc);
    }
}
