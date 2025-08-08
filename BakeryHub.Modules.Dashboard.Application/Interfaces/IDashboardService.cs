
using BakeryHub.Modules.Dashboard.Application.Dtos.Dashboard;

namespace BakeryHub.Modules.Dashboard.Application.Interfaces;

public interface IDashboardService
{
    Task<DashboardResponseDto> GetDashboardStatisticsAsync(Guid tenantId, DashboardQueryParametersDto queryParams);
}
