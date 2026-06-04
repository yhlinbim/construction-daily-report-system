using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CDRS.Application.Interfaces;
using CDRS.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace CDRS.Application.Services
{
    public class DailyReportService : IDailyReportService
    {
        private readonly IDailyReportRepository _repository;
        private readonly ILogger<DailyReportService> _logger;

        public DailyReportService(
            IDailyReportRepository repository,
            ILogger<DailyReportService> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        public async Task<DailyReport> CreateReportAsync(
            string projectId, string siteWorkerId, DateTime reportDate,
            string workDescription, int workerCount, string weatherCondition,
            CancellationToken ct = default)
        {
            _logger.LogInformation(
                "Creating daily report for project {ProjectId} by worker {WorkerId}",
                projectId, siteWorkerId);

            var report = DailyReport.Create(
                projectId, siteWorkerId, reportDate,
                workDescription, workerCount, weatherCondition);

            await _repository.AddAsync(report, ct);
            await _repository.SaveChangesAsync(ct);

            return report;
        }

        public async Task SubmitReportAsync(Guid reportId, CancellationToken ct = default)
        {
            var report = await GetReportOrThrowAsync(reportId, ct);
            report.Submit();
            await _repository.SaveChangesAsync(ct);

            _logger.LogInformation("Report {ReportId} submitted successfully", reportId);
        }

        public async Task ApproveReportAsync(Guid reportId, CancellationToken ct = default)
        {
            var report = await GetReportOrThrowAsync(reportId, ct);
            report.Approve();
            await _repository.SaveChangesAsync(ct);

            _logger.LogInformation("Report {ReportId} approved", reportId);
        }

        public async Task RejectReportAsync(Guid reportId, string reason, CancellationToken ct = default)
        {
            var report = await GetReportOrThrowAsync(reportId, ct);
            report.Reject(reason);
            await _repository.SaveChangesAsync(ct);

            _logger.LogInformation("Report {ReportId} rejected. Reason: {Reason}", reportId, reason);
        }

        public async Task<List<DailyReport>> GetProjectReportsAsync(
            string projectId, CancellationToken ct = default)
            => await _repository.GetByProjectIdAsync(projectId, ct);

        public async Task<List<DailyReport>> GetPendingReviewsAsync(CancellationToken ct = default)
            => await _repository.GetPendingReviewAsync(ct);

        private async Task<DailyReport> GetReportOrThrowAsync(Guid reportId, CancellationToken ct)
        {
            var report = await _repository.GetByIdAsync(reportId, ct);
            if (report == null)
                throw new KeyNotFoundException($"Daily report with ID {reportId} was not found.");
            return report;
        }
    }
}
