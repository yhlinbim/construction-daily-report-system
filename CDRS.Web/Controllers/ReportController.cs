using CDRS.Application.Interfaces;
using CDRS.Domain.Exceptions;
using CDRS.Web.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;


namespace CDRS.Web.Controllers
{
    /// <summary>
    /// MVC controller for construction site daily report management.
    /// Any authenticated role can view reports; creating and submitting
    /// them is restricted to Worker, matching ReportApiController and the
    /// GraphQL mutations. There is no login UI — the primary interface is
    /// the Swagger REST API.
    /// </summary>
    [Authorize]
    public class ReportController : Controller
    {
        private readonly IDailyReportService _reportService;
        private readonly ILogger<ReportController> _logger;

        public ReportController(
            IDailyReportService reportService,
            ILogger<ReportController> logger)
        {
            _reportService = reportService;
            _logger = logger;
        }

        public async Task<IActionResult> Index(string projectId = "PROJ-001")
        {
            var reports = await _reportService.GetProjectReportsAsync(projectId);
            ViewBag.ProjectId = projectId;
            return View(reports);
        }

        [Authorize(Roles = Roles.Worker)]
        public IActionResult Create(string projectId)
        {
            ViewBag.ProjectId = projectId;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Roles.Worker)]
        public async Task<IActionResult> Create(
            string projectId, string siteWorkerId, DateTime reportDate,
            string workDescription, int workerCount, string weatherCondition)
        {
            try
            {
                await _reportService.CreateReportAsync(
                    projectId, siteWorkerId, reportDate,
                    workDescription, workerCount, weatherCondition);

                TempData["Success"] = "Daily report created successfully.";
                return RedirectToAction(nameof(Index), new { projectId });
            }
            catch (DomainException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                ViewBag.ProjectId = projectId;
                return View();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Roles.Worker)]
        public async Task<IActionResult> Submit(Guid id, string projectId)
        {
            await _reportService.SubmitReportAsync(id);
            TempData["Success"] = "Report submitted for review.";
            return RedirectToAction(nameof(Index), new { projectId });
        }
    }
}
