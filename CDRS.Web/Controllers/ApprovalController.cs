using CDRS.Application.Interfaces;
using CDRS.Domain.Exceptions;
using CDRS.Web.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CDRS.Web.Controllers
{
    /// <summary>
    /// MVC controller for the supervisor approval workflow.
    /// Restricted to Supervisor and ProjectManager roles — mirrors the
    /// authorization applied to the equivalent REST API endpoints in
    /// ReportApiController and ReportApiV2Controller.
    /// </summary>
    [Authorize(Roles = $"{Roles.Supervisor},{Roles.ProjectManager}")]
    public class ApprovalController : Controller
    {
        private readonly IDailyReportService _reportService;
        private readonly ILogger<ApprovalController> _logger;

        public ApprovalController(
            IDailyReportService reportService,
            ILogger<ApprovalController> logger)
        {
            _reportService = reportService;
            _logger = logger;
        }

        // Displays the pending report queue for supervisors and project managers.
        public async Task<IActionResult> Queue()
        {
            var pendingReports = await _reportService.GetPendingReviewsAsync();
            return View(pendingReports);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(Guid id)
        {
            try
            {
                // Move to UnderReview first if still in Submitted state,
                // then approve — matches the state machine in DailyReport.
                var reports = await _reportService.GetPendingReviewsAsync();
                var report = reports.FirstOrDefault(r => r.Id == id);
                if (report?.Status == CDRS.Domain.Enums.ReportStatus.Submitted)
                    await _reportService.StartReviewAsync(id);

                await _reportService.ApproveReportAsync(id);
                TempData["Success"] = "Report approved successfully.";
            }
            catch (DomainException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(nameof(Queue));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(Guid id, string reason)
        {
            try
            {
                await _reportService.RejectReportAsync(id, reason);
                TempData["Success"] = "Report rejected.";
            }
            catch (DomainException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(nameof(Queue));
        }
    }
}