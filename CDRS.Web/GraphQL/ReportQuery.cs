using CDRS.Application.Interfaces;
using CDRS.Domain.Entities;

namespace CDRS.Web.GraphQL
{
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
        /// </summary>
        public async Task<IEnumerable<DailyReport>> GetPendingReviews(
            [Service] IDailyReportService reportService,
            CancellationToken cancellationToken = default)
        {
            return await reportService.GetPendingReviewsAsync(cancellationToken);
        }
    }
}
