using CDRS.Application.Interfaces;
using CDRS.Domain.Entities;
using CDRS.Domain.Enums;
using CDRS.Domain.Exceptions;
using CDRS.Web.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CDRS.Tests.Api
{
    /// <summary>
    /// Unit tests for ReportApiController.
    ///
    /// These tests verify controller behaviour in isolation:
    /// - Correct HTTP status codes are returned
    /// - Service methods are called with correct parameters
    /// - Exceptions propagate correctly (handled by GlobalExceptionMiddleware in production)
    ///
    /// Note: JWT authentication and middleware pipeline are tested
    /// in integration tests (ReportApiControllerIntegrationTests).
    /// </summary>
    public class ReportApiControllerUnitTests
    {
        private readonly Mock<IDailyReportService> _serviceMock;
        private readonly ReportApiController _controller;

        public ReportApiControllerUnitTests()
        {
            _serviceMock = new Mock<IDailyReportService>();
            _controller = new ReportApiController(
                _serviceMock.Object,
                NullLogger<ReportApiController>.Instance);
        }

        // =============================================
        // GET /api/reports/{projectId}
        // =============================================

        [Fact]
        public async Task GetByProject_WhenReportsExist_ShouldReturn200WithReports()
        {
            // Arrange
            var reports = new List<DailyReport>
        {
            DailyReport.Create("PROJ-001", "EMP001", DateTime.Today,
                "Foundation work.", 3, "Fine"),
            DailyReport.Create("PROJ-001", "EMP002", DateTime.Today.AddDays(-1),
                "Concrete pour.", 5, "Cloudy")
        };

            _serviceMock
                .Setup(s => s.GetProjectReportsAsync("PROJ-001", It.IsAny<CancellationToken>()))
                .ReturnsAsync(reports);

            // Act
            var result = await _controller.GetByProject("PROJ-001");

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var returnedReports = okResult.Value.Should()
                .BeAssignableTo<IEnumerable<DailyReport>>().Subject;
            returnedReports.Should().HaveCount(2);
        }

        [Fact]
        public async Task GetByProject_ShouldCallServiceWithCorrectProjectId()
        {
            // Arrange
            _serviceMock
                .Setup(s => s.GetProjectReportsAsync("PROJ-001", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<DailyReport>());

            // Act
            await _controller.GetByProject("PROJ-001");

            // Assert
            _serviceMock.Verify(
                s => s.GetProjectReportsAsync("PROJ-001", It.IsAny<CancellationToken>()),
                Times.Once);
        }

        // =============================================
        // POST /api/reports
        // =============================================

        [Fact]
        public async Task Create_WithValidRequest_ShouldReturn201WithReport()
        {
            // Arrange
            var request = new CreateReportRequest(
                "PROJ-001", "EMP001", DateTime.Today,
                "Foundation formwork.", 5, "Fine");

            var createdReport = DailyReport.Create(
                request.ProjectId, request.SiteWorkerId, request.ReportDate,
                request.WorkDescription, request.WorkerCount, request.WeatherCondition);

            _serviceMock
                .Setup(s => s.CreateReportAsync(
                    request.ProjectId, request.SiteWorkerId, request.ReportDate,
                    request.WorkDescription, request.WorkerCount, request.WeatherCondition,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(createdReport);

            // Act
            var result = await _controller.Create(request);

            // Assert
            var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
            createdResult.StatusCode.Should().Be(201);
            createdResult.Value.Should().BeEquivalentTo(createdReport);
        }

        [Fact]
        public async Task Create_WhenDomainExceptionThrown_ShouldPropagateException()
        {
            // Arrange
            // In production, GlobalExceptionMiddleware catches this and returns 400
            // Unit test verifies the exception propagates from controller correctly
            var request = new CreateReportRequest(
                "", "EMP001", DateTime.Today, "Work.", 5, "Fine");

            _serviceMock
                .Setup(s => s.CreateReportAsync(
                    "", It.IsAny<string>(), It.IsAny<DateTime>(),
                    It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new DomainException("Project ID is required."));

            // Act
            var act = async () => await _controller.Create(request);

            // Assert
            await act.Should().ThrowAsync<DomainException>()
                .WithMessage("Project ID is required.");
        }

        // =============================================
        // POST /api/reports/{id}/submit
        // =============================================

        [Fact]
        public async Task Submit_WhenReportExists_ShouldReturn204()
        {
            // Arrange
            var reportId = Guid.NewGuid();
            _serviceMock
                .Setup(s => s.SubmitReportAsync(reportId, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.Submit(reportId);

            // Assert
            result.Should().BeOfType<NoContentResult>();
        }

        [Fact]
        public async Task Submit_WhenReportNotFound_ShouldPropagateKeyNotFoundException()
        {
            // Arrange
            var nonExistentId = Guid.NewGuid();
            _serviceMock
                .Setup(s => s.SubmitReportAsync(nonExistentId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new KeyNotFoundException($"Report {nonExistentId} not found."));

            // Act
            var act = async () => await _controller.Submit(nonExistentId);

            // Assert
            await act.Should().ThrowAsync<KeyNotFoundException>();
        }

        // =============================================
        // POST /api/reports/{id}/approve
        // =============================================

        [Fact]
        public async Task Approve_WhenUnderReview_ShouldReturn204()
        {
            // Arrange
            var reportId = Guid.NewGuid();
            _serviceMock
                .Setup(s => s.ApproveReportAsync(reportId, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.Approve(reportId);

            // Assert
            result.Should().BeOfType<NoContentResult>();
        }

        [Fact]
        public async Task Approve_WhenReportNotInReviewState_ShouldPropagateDomainException()
        {
            // Arrange
            var reportId = Guid.NewGuid();
            _serviceMock
                .Setup(s => s.ApproveReportAsync(reportId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new DomainException("Only reports under review can be approved."));

            // Act
            var act = async () => await _controller.Approve(reportId);

            // Assert
            await act.Should().ThrowAsync<DomainException>()
                .WithMessage("*review*");
        }

        // =============================================
        // POST /api/reports/{id}/reject
        // =============================================

        [Fact]
        public async Task Reject_WhenUnderReview_ShouldReturn204()
        {
            // Arrange
            var reportId = Guid.NewGuid();
            var request = new RejectReportRequest("Insufficient safety documentation.");

            _serviceMock
                .Setup(s => s.RejectReportAsync(reportId, request.Reason,
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.Reject(reportId, request);

            // Assert
            result.Should().BeOfType<NoContentResult>();
        }

        [Fact]
        public async Task Reject_WithEmptyReason_ShouldPropagateDomainException()
        {
            // Arrange
            var reportId = Guid.NewGuid();
            var request = new RejectReportRequest("");

            _serviceMock
                .Setup(s => s.RejectReportAsync(reportId, "",
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new DomainException("Rejection reason is required."));

            // Act
            var act = async () => await _controller.Reject(reportId, request);

            // Assert
            await act.Should().ThrowAsync<DomainException>()
                .WithMessage("*reason*");
        }

        // =============================================
        // GET /api/reports/pending
        // =============================================

        [Fact]
        public async Task GetPendingReviews_ShouldReturn200WithPendingReports()
        {
            // Arrange
            var pendingReports = new List<DailyReport>
        {
            DailyReport.Create("PROJ-001", "EMP001", DateTime.Today,
                "Work.", 3, "Fine")
        };

            // Force to Submitted status
            pendingReports[0].Submit();

            _serviceMock
                .Setup(s => s.GetPendingReviewsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(pendingReports);

            // Act
            var result = await _controller.GetPendingReviews();

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var reports = okResult.Value.Should()
                .BeAssignableTo<IEnumerable<DailyReport>>().Subject;
            reports.Should().HaveCount(1);
            reports.First().Status.Should().Be(ReportStatus.Submitted);
        }
    }
}