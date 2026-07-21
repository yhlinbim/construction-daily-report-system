using Asp.Versioning;
using CDRS.Application.Interfaces;
using CDRS.Domain.Entities;
using CDRS.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CDRS.Web.Controllers
{
    /// <summary>
    /// API v2 for Daily Reports.
    /// Breaking change from v1: status field is now a string ("Draft", "Submitted", etc.)
    /// instead of an integer. StatusCode field added for backward reference.
    /// </summary>
    [ApiController]
    [ApiVersion("2.0")]
    [Route("api/v{version:apiVersion}/reports")]
    [Authorize]
    public class ReportApiV2Controller : ControllerBase
    {
        private readonly IDailyReportService _reportService;
        private readonly ILogger<ReportApiV2Controller> _logger;

        public ReportApiV2Controller(
            IDailyReportService reportService,
            ILogger<ReportApiV2Controller> logger)
        {
            _reportService = reportService;
            _logger = logger;
        }

        /// <summary>
        /// Get all reports for a project (v2 — status as string)
        /// </summary>
        [HttpGet("{projectId}")]
        public async Task<ActionResult<IEnumerable<ReportV2Response>>> GetByProject(
            string projectId,
            CancellationToken ct = default)
        {
            var reports = await _reportService.GetProjectReportsAsync(projectId, ct);
            return Ok(reports.Select(MapToV2Response));
        }

        /// <summary>
        /// Get pending reviews (v2 — status as string)
        /// </summary>
        [HttpGet("pending")]
        [Authorize(Roles = $"{CDRS.Web.Auth.Roles.Supervisor},{CDRS.Web.Auth.Roles.ProjectManager}")]
        public async Task<ActionResult<IEnumerable<ReportV2Response>>> GetPendingReviews(
            CancellationToken ct = default)
        {
            var reports = await _reportService.GetPendingReviewsAsync(ct);
            return Ok(reports.Select(MapToV2Response));
        }

        private static ReportV2Response MapToV2Response(DailyReport report) =>
            new()
            {
                Id = report.Id,
                ProjectId = report.ProjectId,
                SiteWorkerId = report.SiteWorkerId,
                ReportDate = report.ReportDate,
                WorkDescription = report.WorkDescription,
                WorkerCount = report.WorkerCount,
                WeatherCondition = report.WeatherCondition,
                Status = report.Status.ToString(),  // 字串，不是數字
                StatusCode = (int)report.Status,    // 數字，向後參考用
                CreatedAtUtc = report.CreatedAtUtc,
                RejectionReason = report.RejectionReason
            };
    }
}
