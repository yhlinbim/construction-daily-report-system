using CDRS.Domain.Entities;
using CDRS.Domain.Enums;
using CDRS.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CDRS.Tests.Integration
{
    /// <summary>
    /// Integration tests for DailyReportRepository.
    ///
    /// Unlike unit tests, these tests use a real EF Core DbContext
    /// with an InMemory database. This verifies that LINQ queries
    /// produce correct results — something Moq-based unit tests cannot do.
    ///
    /// Trade-off: slower than unit tests, but catches query logic errors
    /// that mock-based tests miss.
    /// </summary>
    public class DailyReportRepositoryTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly DailyReportRepository _repository;

        public DailyReportRepositoryTests()
        {
            // Each test gets a fresh InMemory database
            // Guid.NewGuid() ensures test isolation — no shared state
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new AppDbContext(options);
            _repository = new DailyReportRepository(_context);
        }

        public void Dispose()
        {
            _context.Dispose();
        }

        // =============================================
        // GetByIdAsync tests
        // =============================================

        [Fact]
        public async Task GetByIdAsync_WhenReportExists_ShouldReturnReport()
        {
            // Arrange
            var report = DailyReport.Create(
                "PROJ-001", "EMP001", DateTime.Today,
                "Foundation work.", 3, "Fine");

            await _repository.AddAsync(report);
            await _repository.SaveChangesAsync();

            // Act
            var result = await _repository.GetByIdAsync(report.Id);

            // Assert
            result.Should().NotBeNull();
            result!.Id.Should().Be(report.Id);
            result.ProjectId.Should().Be("PROJ-001");
        }

        [Fact]
        public async Task GetByIdAsync_WhenReportDoesNotExist_ShouldReturnNull()
        {
            // Arrange — empty database
            var nonExistentId = Guid.NewGuid();

            // Act
            var result = await _repository.GetByIdAsync(nonExistentId);

            // Assert
            result.Should().BeNull();
        }

        // =============================================
        // GetByProjectIdAsync tests
        // =============================================

        [Fact]
        public async Task GetByProjectIdAsync_ShouldReturnOnlyMatchingProject()
        {
            // Arrange — add reports for two different projects
            var report1 = DailyReport.Create("PROJ-001", "EMP001", DateTime.Today, "Work A.", 3, "Fine");
            var report2 = DailyReport.Create("PROJ-001", "EMP002", DateTime.Today.AddDays(-1), "Work B.", 2, "Cloudy");
            var report3 = DailyReport.Create("PROJ-002", "EMP003", DateTime.Today, "Work C.", 5, "Fine");

            await _repository.AddAsync(report1);
            await _repository.AddAsync(report2);
            await _repository.AddAsync(report3);
            await _repository.SaveChangesAsync();

            // Act
            var result = await _repository.GetByProjectIdAsync("PROJ-001");

            // Assert — only PROJ-001 reports should be returned
            result.Should().HaveCount(2);
            result.Should().AllSatisfy(r => r.ProjectId.Should().Be("PROJ-001"));
        }

        [Fact]
        public async Task GetByProjectIdAsync_ShouldReturnReportsOrderedByDateDescending()
        {
            // Arrange
            var olderReport = DailyReport.Create(
                "PROJ-001", "EMP001", DateTime.Today.AddDays(-2), "Old work.", 3, "Fine");
            var newerReport = DailyReport.Create(
                "PROJ-001", "EMP001", DateTime.Today, "New work.", 3, "Fine");

            await _repository.AddAsync(olderReport);
            await _repository.AddAsync(newerReport);
            await _repository.SaveChangesAsync();

            // Act
            var result = await _repository.GetByProjectIdAsync("PROJ-001");

            // Assert — newer report should come first
            result[0].ReportDate.Should().BeAfter(result[1].ReportDate);
        }

        // =============================================
        // GetPendingReviewAsync tests
        // =============================================

        [Fact]
        public async Task GetPendingReviewAsync_ShouldReturnSubmittedAndUnderReviewOnly()
        {
            // Arrange — create reports in different states
            var draftReport = DailyReport.Create(
                "PROJ-001", "EMP001", DateTime.Today, "Draft.", 1, "Fine");

            var submittedReport = DailyReport.Create(
                "PROJ-001", "EMP001", DateTime.Today, "Submitted.", 2, "Fine");
            submittedReport.Submit();

            var underReviewReport = DailyReport.Create(
                "PROJ-001", "EMP001", DateTime.Today, "Under review.", 3, "Fine");
            underReviewReport.Submit();
            underReviewReport.StartReview();

            var approvedReport = DailyReport.Create(
                "PROJ-001", "EMP001", DateTime.Today, "Approved.", 4, "Fine");
            approvedReport.Submit();
            approvedReport.StartReview();
            approvedReport.Approve();

            await _repository.AddAsync(draftReport);
            await _repository.AddAsync(submittedReport);
            await _repository.AddAsync(underReviewReport);
            await _repository.AddAsync(approvedReport);
            await _repository.SaveChangesAsync();

            // Act
            var result = await _repository.GetPendingReviewAsync();

            // Assert — only Submitted and UnderReview should be returned
            result.Should().HaveCount(2);
            result.Should().AllSatisfy(r =>
                r.Status.Should().BeOneOf(ReportStatus.Submitted, ReportStatus.UnderReview));
        }

        // =============================================
        // AddAsync and SaveChangesAsync tests
        // =============================================

        [Fact]
        public async Task AddAsync_AndSaveChanges_ShouldPersistReport()
        {
            // Arrange
            var report = DailyReport.Create(
                "PROJ-001", "EMP001", DateTime.Today,
                "Foundation work.", 3, "Fine");

            // Act
            await _repository.AddAsync(report);
            await _repository.SaveChangesAsync();

            // Assert — verify by querying directly from context
            var saved = await _context.DailyReports.FindAsync(report.Id);
            saved.Should().NotBeNull();
            saved!.ProjectId.Should().Be("PROJ-001");
            saved.Status.Should().Be(ReportStatus.Draft);
        }
    }
}
