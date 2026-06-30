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

        /// <summary>
        /// Creates a new daily report in Draft status.
        /// The report must be explicitly submitted via SubmitReportAsync
        /// before it enters the approval workflow.
        /// </summary>
        public async Task<DailyReport> CreateReportAsync(
            string projectId, string siteWorkerId, DateTime reportDate,
            string workDescription, int workerCount, string weatherCondition,
            CancellationToken ct = default)
        {
            _logger.LogInformation(
                "Creating daily report. ProjectId: {ProjectId}, WorkerId: {WorkerId}, Date: {ReportDate}",
                projectId, siteWorkerId, reportDate);

            var report = DailyReport.Create(
                projectId, siteWorkerId, reportDate,
                workDescription, workerCount, weatherCondition);

            await _repository.AddAsync(report, ct);
            await _repository.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Daily report created. ReportId: {ReportId}, ProjectId: {ProjectId}",
                report.Id, projectId);

            return report;
        }

        /// <summary>
        /// Transitions a report from Draft to Submitted status.
        /// </summary>
        public async Task SubmitReportAsync(Guid reportId, CancellationToken ct = default)
        {
            var report = await GetReportOrThrowAsync(reportId, ct);

            _logger.LogInformation(
                "Submitting report. ReportId: {ReportId}, ProjectId: {ProjectId}",
                reportId, report.ProjectId);

            report.Submit();
            await _repository.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Report submitted. ReportId: {ReportId}, ProjectId: {ProjectId}",
                reportId, report.ProjectId);
        }

        /// <summary>
        /// Approves a report that is currently under review.
        /// Throws DomainException if the report has not been reviewed yet.
        /// </summary>
        public async Task ApproveReportAsync(Guid reportId, CancellationToken ct = default)
        {
            var report = await GetReportOrThrowAsync(reportId, ct);

            _logger.LogInformation(
                "Approving report. ReportId: {ReportId}, ProjectId: {ProjectId}",
                reportId, report.ProjectId);

            report.Approve();
            await _repository.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Report approved. ReportId: {ReportId}, ProjectId: {ProjectId}, ApprovedAtUtc: {ApprovedAt}",
                reportId, report.ProjectId, DateTime.UtcNow);
        }

        /// <summary>
        /// Rejects a report that is currently under review, with a required reason.
        /// </summary>
        public async Task RejectReportAsync(Guid reportId, string reason, CancellationToken ct = default)
        {
            var report = await GetReportOrThrowAsync(reportId, ct);

            _logger.LogInformation(
                "Rejecting report. ReportId: {ReportId}, ProjectId: {ProjectId}",
                reportId, report.ProjectId);

            report.Reject(reason);
            await _repository.SaveChangesAsync(ct);

            _logger.LogWarning(
                "Report rejected. ReportId: {ReportId}, ProjectId: {ProjectId}, Reason: {Reason}",
                reportId, report.ProjectId, reason);
        }

        /// <summary>
        /// Retrieves all reports for a specific project, ordered by date descending.
        /// </summary>
        public async Task<List<DailyReport>> GetProjectReportsAsync(
            string projectId, CancellationToken ct = default)
            => await _repository.GetByProjectIdAsync(projectId, ct);

        /// <summary>
        /// Retrieves all reports currently pending review (Submitted or UnderReview status).
        /// </summary>
        public async Task<List<DailyReport>> GetPendingReviewsAsync(CancellationToken ct = default)
            => await _repository.GetPendingReviewAsync(ct);

        private async Task<DailyReport> GetReportOrThrowAsync(Guid reportId, CancellationToken ct)
        {
            var report = await _repository.GetByIdAsync(reportId, ct);
            if (report == null)
                throw new KeyNotFoundException($"Daily report with ID {reportId} was not found.");
            return report;
        }

        /// <summary>
        /// Transitions a report from Submitted to UnderReview status.
        /// </summary>
        public async Task StartReviewAsync(Guid reportId, CancellationToken ct = default)
        {
            var report = await GetReportOrThrowAsync(reportId, ct);
            report.StartReview();
            await _repository.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Report moved to under review. ReportId: {ReportId}, ProjectId: {ProjectId}",
                reportId, report.ProjectId);
        }
    }
}
