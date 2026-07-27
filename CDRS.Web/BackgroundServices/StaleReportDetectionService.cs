using CDRS.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CDRS.Web.BackgroundServices
{
    /// <summary>
    /// Background service that periodically detects stale daily reports.
    /// Reports that remain in Draft status for more than 30 days are logged
    /// as warnings — a common requirement in construction site systems to
    /// prevent workers from forgetting to submit their reports.
    /// </summary>
    public class StaleReportDetectionService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<StaleReportDetectionService> _logger;
        private readonly TimeSpan _interval = TimeSpan.FromHours(24);

        public StaleReportDetectionService(
            IServiceScopeFactory scopeFactory,
            ILogger<StaleReportDetectionService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "StaleReportDetectionService started. Checking every {Interval} hours.",
                _interval.TotalHours);

            while (!stoppingToken.IsCancellationRequested)
            {
                await DetectStaleReportsAsync(stoppingToken);
                await Task.Delay(_interval, stoppingToken);
            }
        }

        private async Task DetectStaleReportsAsync(CancellationToken ct)
        {
            try
            {
                // BackgroundService is a singleton, but IDailyReportService is scoped.
                // A new scope must be created for each execution to resolve scoped services.
                using var scope = _scopeFactory.CreateScope();
                var reportService = scope.ServiceProvider
                    .GetRequiredService<IDailyReportService>();

                var allReports = await reportService.GetAllReportsAsync(ct);
                var cutoff = DateTime.UtcNow.AddDays(-30);

                var staleReports = allReports
                    .Where(r => r.Status == CDRS.Domain.Enums.ReportStatus.Draft
                             && r.CreatedAtUtc < cutoff)
                    .ToList();

                if (staleReports.Count == 0)
                {
                    _logger.LogInformation(
                        "StaleReportDetectionService: no stale reports found.");
                    return;
                }

                foreach (var report in staleReports)
                {
                    _logger.LogWarning(
                        "Stale report detected: ReportId={ReportId}, ProjectId={ProjectId}, " +
                        "CreatedAt={CreatedAt}, DaysOld={DaysOld}",
                        report.Id,
                        report.ProjectId,
                        report.CreatedAtUtc,
                        (DateTime.UtcNow - report.CreatedAtUtc).Days);
                }

                _logger.LogInformation(
                    "StaleReportDetectionService: {Count} stale report(s) detected.",
                    staleReports.Count);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Log but do not rethrow — a failure in one cycle should not
                // stop the background service from running in the next cycle.
                _logger.LogError(ex,
                    "StaleReportDetectionService: unhandled error during detection cycle.");
            }
        }
    }
}
