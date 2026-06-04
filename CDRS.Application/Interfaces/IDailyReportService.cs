using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CDRS.Domain.Entities;
using CDRS.Domain.Enums;

namespace CDRS.Application.Interfaces
{
    public interface IDailyReportService
    {
        Task<DailyReport> CreateReportAsync(
            string projectId, string siteWorkerId, DateTime reportDate,
            string workDescription, int workerCount, string weatherCondition,
            CancellationToken ct = default);

        Task SubmitReportAsync(Guid reportId, CancellationToken ct = default);
        Task ApproveReportAsync(Guid reportId, CancellationToken ct = default);
        Task RejectReportAsync(Guid reportId, string reason, CancellationToken ct = default);
        Task<List<DailyReport>> GetProjectReportsAsync(string projectId, CancellationToken ct = default);
        Task<List<DailyReport>> GetPendingReviewsAsync(CancellationToken ct = default);
    }
}
