using CDRS.Application.Interfaces;
using CDRS.Domain.Entities;
using CDRS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CDRS.Infrastructure.Persistence
{
    public class DailyReportRepository : IDailyReportRepository
    {
        private readonly AppDbContext _context;

        public DailyReportRepository(AppDbContext context) => _context = context;

        public async Task<DailyReport?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => await _context.DailyReports.FirstOrDefaultAsync(r => r.Id == id, ct);

        public async Task<List<DailyReport>> GetByProjectIdAsync(
            string projectId, CancellationToken ct = default)
            => await _context.DailyReports
                .AsNoTracking()
                .Where(r => r.ProjectId == projectId)
                .OrderByDescending(r => r.ReportDate)
                .ToListAsync(ct);

        public async Task<List<DailyReport>> GetPendingReviewAsync(CancellationToken ct = default)
            => await _context.DailyReports
                .AsNoTracking()
                .Where(r => r.Status == ReportStatus.Submitted ||
                            r.Status == ReportStatus.UnderReview)
                .OrderBy(r => r.CreatedAtUtc)
                .ToListAsync(ct);

        public async Task AddAsync(DailyReport report, CancellationToken ct = default)
            => await _context.DailyReports.AddAsync(report, ct);

        public async Task SaveChangesAsync(CancellationToken ct = default)
        {
            try
            {
                await _context.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                throw new InvalidOperationException(
                    "The record was modified by another user. Please refresh and try again.", ex);
            }
        }
    }
}
