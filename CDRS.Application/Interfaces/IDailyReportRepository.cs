using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CDRS.Domain.Entities;

namespace CDRS.Application.Interfaces
{
    public interface IDailyReportRepository
    {
        Task<DailyReport?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<List<DailyReport>> GetByProjectIdAsync(string projectId, CancellationToken ct = default);
        Task<List<DailyReport>> GetPendingReviewAsync(CancellationToken ct = default);

        /// <summary>
        /// Retrieves all daily reports in the system, with no filtering.
        /// Intended for background/administrative scans, not for
        /// user-facing list views (which should filter by project or status).
        /// </summary>
        Task<List<DailyReport>> GetAllAsync(CancellationToken ct = default);

        Task AddAsync(DailyReport report, CancellationToken ct = default);
        Task SaveChangesAsync(CancellationToken ct = default);
    }
}
