using CDRS.Application.Interfaces;
using CDRS.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace CDRS.Web.Controllers
{
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

        // Supervisor review queue
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
                // First move to UnderReview if Submitted, then Approve
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
