using CDRS.Application.Interfaces;
using CDRS.Application.Services;
using CDRS.Domain.Entities;
using CDRS.Domain.Enums;
using CDRS.Domain.Exceptions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CDRS.UnitTests.Application
{
    /// <summary>
    /// Tests for DailyReportService.
    ///
    /// All tests use Moq to mock IDailyReportRepository.
    /// This means tests run without a database — fast, isolated, deterministic.
    ///
    /// Key principle being tested: the Service layer orchestrates domain objects
    /// and repository calls correctly, without containing business logic itself.
    /// Business rules live in DailyReport (domain entity).
    /// </summary>
    public class DailyReportServiceTests
    {
        private readonly Mock<IDailyReportRepository> _repositoryMock;
        private readonly DailyReportService _service;

        public DailyReportServiceTests()
        {
            _repositoryMock = new Mock<IDailyReportRepository>();

            // NullLogger: real logger interface, but discards all output
            // Using this avoids needing to mock ILogger in every test
            _service = new DailyReportService(
                _repositoryMock.Object,
                NullLogger<DailyReportService>.Instance);
        }

        [Fact]
        public async Task CreateReportAsync_WithValidParameters_ShouldReturnDraftReport()
        {
            var result = await _service.CreateReportAsync(
                "PROJ-001", "EMP001", DateTime.Today,
                "Foundation formwork completed.", 5, "Fine");

            result.Should().NotBeNull();
            result.Status.Should().Be(ReportStatus.Draft);
            result.ProjectId.Should().Be("PROJ-001");
        }

        [Fact]
        public async Task CreateReportAsync_ShouldCallAddAsyncAndSaveChangesOnce()
        {
            await _service.CreateReportAsync(
                "PROJ-001", "EMP001", DateTime.Today,
                "Foundation formwork completed.", 5, "Fine");

            _repositoryMock.Verify(
                r => r.AddAsync(It.IsAny<DailyReport>(), It.IsAny<CancellationToken>()),
                Times.Once,
                "AddAsync should be called exactly once when creating a report");

            _repositoryMock.Verify(
                r => r.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Once,
                "SaveChangesAsync should be called exactly once");
        }

        [Fact]
        public async Task CreateReportAsync_WithEmptyProjectId_ShouldThrowDomainException()
        {
            var act = async () => await _service.CreateReportAsync(
                "", "EMP001", DateTime.Today,
                "Foundation formwork.", 5, "Fine");

            await act.Should().ThrowAsync<DomainException>();

            _repositoryMock.Verify(
                r => r.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Never,
                "SaveChangesAsync should NOT be called when validation fails");
        }

        [Fact]
        public async Task SubmitReportAsync_WhenReportExists_ShouldCallSaveChanges()
        {
            var existingReport = DailyReport.Create(
                "PROJ-001", "EMP001", DateTime.Today,
                "Foundation work.", 3, "Cloudy");

            _repositoryMock
                .Setup(r => r.GetByIdAsync(existingReport.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingReport);

            await _service.SubmitReportAsync(existingReport.Id);

            existingReport.Status.Should().Be(ReportStatus.Submitted);

            _repositoryMock.Verify(
                r => r.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task SubmitReportAsync_WhenReportNotFound_ShouldThrowKeyNotFoundException()
        {
            var nonExistentId = Guid.NewGuid();

            _repositoryMock
                .Setup(r => r.GetByIdAsync(nonExistentId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((DailyReport?)null);

            var act = async () => await _service.SubmitReportAsync(nonExistentId);

            await act.Should().ThrowAsync<KeyNotFoundException>()
                .WithMessage($"*{nonExistentId}*");
        }

        [Fact]
        public async Task ApproveReportAsync_WhenUnderReview_ShouldCallSaveChanges()
        {
            var report = DailyReport.Create(
                "PROJ-001", "EMP001", DateTime.Today, "Work.", 2, "Fine");
            report.Submit();
            report.StartReview();

            _repositoryMock
                .Setup(r => r.GetByIdAsync(report.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(report);

            await _service.ApproveReportAsync(report.Id);

            report.Status.Should().Be(ReportStatus.Approved);

            _repositoryMock.Verify(
                r => r.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task ApproveReportAsync_WhenDraft_ShouldThrowDomainException()
        {
            var report = DailyReport.Create(
                "PROJ-001", "EMP001", DateTime.Today, "Work.", 2, "Fine");

            _repositoryMock
                .Setup(r => r.GetByIdAsync(report.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(report);

            var act = async () => await _service.ApproveReportAsync(report.Id);

            await act.Should().ThrowAsync<DomainException>();

            _repositoryMock.Verify(
                r => r.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task RejectReportAsync_WhenUnderReview_ShouldSaveRejectionReason()
        {
            var report = DailyReport.Create(
                "PROJ-001", "EMP001", DateTime.Today, "Work.", 2, "Fine");
            report.Submit();
            report.StartReview();

            _repositoryMock
                .Setup(r => r.GetByIdAsync(report.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(report);

            await _service.RejectReportAsync(report.Id, "Insufficient safety documentation.");

            report.Status.Should().Be(ReportStatus.Rejected);
            report.RejectionReason.Should().Be("Insufficient safety documentation.");

            _repositoryMock.Verify(
                r => r.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task RejectReportAsync_WithEmptyReason_ShouldThrowDomainException()
        {
            var report = DailyReport.Create(
                "PROJ-001", "EMP001", DateTime.Today, "Work.", 2, "Fine");
            report.Submit();
            report.StartReview();

            _repositoryMock
                .Setup(r => r.GetByIdAsync(report.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(report);

            var act = async () => await _service.RejectReportAsync(report.Id, "");

            await act.Should().ThrowAsync<DomainException>();
        }

        [Fact]
        public async Task GetProjectReportsAsync_ShouldReturnReportsForSpecificProject()
        {
            var reports = new List<DailyReport>
        {
            DailyReport.Create("PROJ-001", "EMP001", DateTime.Today, "Work A.", 3, "Fine"),
            DailyReport.Create("PROJ-001", "EMP002", DateTime.Today.AddDays(-1), "Work B.", 4, "Cloudy")
        };

            _repositoryMock
                .Setup(r => r.GetByProjectIdAsync("PROJ-001", It.IsAny<CancellationToken>()))
                .ReturnsAsync(reports);

            var result = await _service.GetProjectReportsAsync("PROJ-001");

            result.Should().HaveCount(2);
            result.Should().AllSatisfy(r => r.ProjectId.Should().Be("PROJ-001"));
        }
    }
}
