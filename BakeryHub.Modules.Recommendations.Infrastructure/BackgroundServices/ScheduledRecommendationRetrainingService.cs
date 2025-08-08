using BakeryHub.Application.Interfaces.BackgroundServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BakeryHub.Modules.Recommendations.Infrastructure.BackgroundServices;
public class ScheduledRecommendationRetrainingService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private Timer? _timer;

    public ScheduledRecommendationRetrainingService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var dueTime = CalculateDueTime();
        _timer = new Timer(DoWork, null, dueTime, Timeout.InfiniteTimeSpan);
        return Task.CompletedTask;
    }

    private TimeSpan CalculateDueTime()
    {
        var now = DateTime.Now;
        var today = now.DayOfWeek;
        int daysUntilSunday = ((int)DayOfWeek.Sunday - (int)today + 7) % 7;

        if (daysUntilSunday == 0 && now.Hour >= 3)
        {
            daysUntilSunday = 7;
        }

        var nextSunday = now.Date.AddDays(daysUntilSunday).AddHours(3);
        var dueTime = nextSunday - now;

        if (dueTime < TimeSpan.Zero)
        {
            nextSunday = nextSunday.AddDays(7);
            dueTime = nextSunday - now;
        }

        return dueTime;
    }

    private async void DoWork(object? state)
    {
        try
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var retrainingService = scope.ServiceProvider.GetRequiredService<IModelRetrainingService>();
                await retrainingService.RetrainAllTenantModelsAsync();
            }
        }
        finally
        {
            var nextDueTime = CalculateDueTime();
            _timer?.Change(nextDueTime, Timeout.InfiniteTimeSpan);
        }
    }

    public override Task StopAsync(CancellationToken stoppingToken)
    {
        _timer?.Change(Timeout.Infinite, 0);
        return base.StopAsync(stoppingToken);
    }

    public override void Dispose()
    {
        _timer?.Dispose();
        GC.SuppressFinalize(this);
        base.Dispose();
    }
}
