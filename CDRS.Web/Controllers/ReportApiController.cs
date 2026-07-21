using Asp.Versioning;
using CDRS.Application.Interfaces;
using CDRS.Domain.Entities;
using CDRS.Domain.Exceptions;
using CDRS.Web.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CDRS.Web.Controllers
{
    [ApiController]
    [ApiVersion("1.0", Deprecated = true)]
    [Route("api/v{version:apiVersion}/reports")]
    [Authorize]
    public class ReportApiController : ControllerBase
    {
        private readonly IDailyReportService _reportService;
        private readonly ILogger<ReportApiController> _logger;

        public ReportApiController(
            IDailyReportService reportService,
            ILogger<ReportApiController> logger)
        {
            _reportService = reportService;
            _logger = logger;
        }

        /// <summary>
        /// Get all reports for a project
        /// </summary>
        [HttpGet("{projectId}")]
        public async Task<ActionResult<IEnumerable<DailyReport>>> GetByProject(string projectId)
        {
            var reports = await _reportService.GetProjectReportsAsync(projectId);
            return Ok(reports);
        }

        /// <summary>
        /// Get all reports pending review
        /// </summary>
        [HttpGet("pending")]
        [Authorize(Roles = $"{Roles.Supervisor},{Roles.ProjectManager}")]
        public async Task<ActionResult<IEnumerable<DailyReport>>> GetPendingReviews()
        {
            var reports = await _reportService.GetPendingReviewsAsync();
            return Ok(reports);
        }

        /// <summary>
        /// Create a new daily report
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<DailyReport>> Create([FromBody] CreateReportRequest request)
        {
            var report = await _reportService.CreateReportAsync(
                    request.ProjectId,
                    request.SiteWorkerId,
                    request.ReportDate,
                    request.WorkDescription,
                    request.WorkerCount,
                    request.WeatherCondition);

            return CreatedAtAction(nameof(GetByProject),
                new { projectId = report.ProjectId }, report);
        }

        /// <summary>
        /// Submit a report for review
        /// </summary>
        [HttpPost("{id}/submit")]
        public async Task<IActionResult> Submit(Guid id)
        {
            await _reportService.SubmitReportAsync(id);
            return NoContent();
        }

        /// <summary>
        /// Approve a report
        /// </summary>
        [HttpPost("{id}/approve")]
        [Authorize(Roles = $"{Roles.Supervisor},{Roles.ProjectManager}")]
        public async Task<IActionResult> Approve(Guid id)
        {
            await _reportService.ApproveReportAsync(id);
            return NoContent();
        }

        /// <summary>
        /// Reject a report
        /// </summary>
        [HttpPost("{id}/reject")]
        [Authorize(Roles = $"{Roles.Supervisor},{Roles.ProjectManager}")]
        public async Task<IActionResult> Reject(Guid id, [FromBody] RejectReportRequest request)
        {
            await _reportService.RejectReportAsync(id, request.Reason);
            return NoContent();
        }
    }

    public record CreateReportRequest(
        string ProjectId,
        string SiteWorkerId,
        DateTime ReportDate,
        string WorkDescription,
        int WorkerCount,
        string WeatherCondition);

    public record RejectReportRequest(string Reason);
}
