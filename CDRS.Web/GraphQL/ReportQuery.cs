using CDRS.Application.Interfaces;
using CDRS.Domain.Entities;
using CDRS.Web.Auth;
using HotChocolate.Authorization;

namespace CDRS.Web.GraphQL
{
    /// <summary>
    /// GraphQL read API for daily reports.
    /// Authorization matches the REST endpoints in ReportApiController:
    /// every field requires an authenticated caller, and the pending-review
    /// queue is restricted to Supervisor and ProjectManager.
    /// </summary>
    [Authorize]
    public class ReportQuery
    {
        /// <summary>
        /// Get all reports for a specific project.
        /// Demonstrates GraphQL's field selection — clients request only the fields they need.
        /// </summary>
        public async Task<IEnumerable<DailyReport>> GetReportsByProject(
            string projectId,
            [Service] IDailyReportService reportService,
            CancellationToken cancellationToken = default)
        {
            return await reportService.GetProjectReportsAsync(
                projectId, cancellationToken);
        }

        /// <summary>
        /// Get all reports pending review.
        /// Restricted to reviewers — mirrors GET /api/v1/reports/pending.
        /// </summary>
        [Authorize(Roles = new[] { Roles.Supervisor, Roles.ProjectManager })]
        public async Task<IEnumerable<DailyReport>> GetPendingReviews(
            [Service] IDailyReportService reportService,
            CancellationToken cancellationToken = default)
        {
            return await reportService.GetPendingReviewsAsync(cancellationToken);
        }
    }
}
