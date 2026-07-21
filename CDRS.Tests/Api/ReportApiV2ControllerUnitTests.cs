using CDRS.Application.Interfaces;
using CDRS.Domain.Entities;
using CDRS.Domain.Enums;
using CDRS.Web.Controllers;
using CDRS.Web.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CDRS.Tests.Api
{
    /// <summary>
    /// Unit tests for ReportApiV2Controller.
    ///
    /// Key difference from v1: verifies that status is returned as a string
    /// and statusCode is included in the response.
    ///
    /// Authentication and routing are tested in
    /// ReportApiControllerIntegrationTests.
    /// </summary>
    public class ReportApiV2ControllerUnitTests
    {
        private readonly Mock<IDailyReportService> _serviceMock;
        private readonly ReportApiV2Controller _controller;

        public ReportApiV2ControllerUnitTests()
        {
            _serviceMock = new Mock<IDailyReportService>();
            _controller = new ReportApiV2Controller(
                _serviceMock.Object,
                NullLogger<ReportApiV2Controller>.Instance);
        }

        // =============================================
        // GET /api/v2/reports/{projectId}
        // =============================================

        [Fact]
        public async Task GetByProject_ShouldReturn200WithV2Response()
        {
            // Arrange
            var reports = new List<DailyReport>
        {
            DailyReport.Create("PROJ-001", "EMP001", DateTime.Today,
                "Foundation work.", 3, "Fine")
        };

            _serviceMock
                .Setup(s => s.GetProjectReportsAsync(
                    "PROJ-001", It.IsAny<CancellationToken>()))
                .ReturnsAsync(reports);

            // Act
            var result = await _controller.GetByProject("PROJ-001");

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var response = okResult.Value.Should()
                .BeAssignableTo<IEnumerable<ReportV2Response>>().Subject;
            response.Should().HaveCount(1);
        }

        [Fact]
        public async Task GetByProject_StatusShouldBeString()
        {
            // Arrange — this is the key difference from v1
            var report = DailyReport.Create("PROJ-001", "EMP001", DateTime.Today,
                "Foundation work.", 3, "Fine");

            _serviceMock
                .Setup(s => s.GetProjectReportsAsync(
                    "PROJ-001", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<DailyReport> { report });

            // Act
            var result = await _controller.GetByProject("PROJ-001");

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var response = okResult.Value.Should()
                .BeAssignableTo<IEnumerable<ReportV2Response>>().Subject;

            var first = response.First();
            first.Status.Should().Be("Draft");         // 字串，不是數字
            first.StatusCode.Should().Be(0);           // 數字保留在 StatusCode
        }

        [Fact]
        public async Task GetByProject_SubmittedReport_StatusShouldBeSubmittedString()
        {
            // Arrange
            var report = DailyReport.Create("PROJ-001", "EMP001", DateTime.Today,
                "Foundation work.", 3, "Fine");
            report.Submit();  // 改變狀態到 Submitted

            _serviceMock
                .Setup(s => s.GetProjectReportsAsync(
                    "PROJ-001", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<DailyReport> { report });

            // Act
            var result = await _controller.GetByProject("PROJ-001");

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var response = okResult.Value.Should()
                .BeAssignableTo<IEnumerable<ReportV2Response>>().Subject;

            var first = response.First();
            first.Status.Should().Be("Submitted");
            first.StatusCode.Should().Be(1);
        }

        [Fact]
        public async Task GetByProject_ShouldCallServiceWithCorrectProjectId()
        {
            // Arrange
            _serviceMock
                .Setup(s => s.GetProjectReportsAsync(
                    "PROJ-001", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<DailyReport>());

            // Act
            await _controller.GetByProject("PROJ-001");

            // Assert
            _serviceMock.Verify(
                s => s.GetProjectReportsAsync(
                    "PROJ-001", It.IsAny<CancellationToken>()),
                Times.Once);
        }

        // =============================================
        // GET /api/v2/reports/pending
        // =============================================

        [Fact]
        public async Task GetPendingReviews_ShouldReturn200WithV2Response()
        {
            // Arrange
            var report = DailyReport.Create("PROJ-001", "EMP001", DateTime.Today,
                "Foundation work.", 3, "Fine");
            report.Submit();

            _serviceMock
                .Setup(s => s.GetPendingReviewsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<DailyReport> { report });

            // Act
            var result = await _controller.GetPendingReviews();

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var response = okResult.Value.Should()
                .BeAssignableTo<IEnumerable<ReportV2Response>>().Subject;
            response.Should().HaveCount(1);
            response.First().Status.Should().Be("Submitted");
        }

        [Fact]
        public async Task GetPendingReviews_AllFieldsShouldBeMappedCorrectly()
        {
            // Arrange — verify the complete mapping
            var report = DailyReport.Create(
                "PROJ-001", "EMP001", DateTime.Today,
                "Foundation work.", 5, "Sunny");
            report.Submit();

            _serviceMock
                .Setup(s => s.GetPendingReviewsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<DailyReport> { report });

            // Act
            var result = await _controller.GetPendingReviews();

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var response = okResult.Value.Should()
                .BeAssignableTo<IEnumerable<ReportV2Response>>().Subject;

            var first = response.First();
            first.ProjectId.Should().Be("PROJ-001");
            first.SiteWorkerId.Should().Be("EMP001");
            first.WorkDescription.Should().Be("Foundation work.");
            first.WorkerCount.Should().Be(5);
            first.WeatherCondition.Should().Be("Sunny");
            first.Status.Should().Be("Submitted");
            first.StatusCode.Should().Be(1);
            first.RejectionReason.Should().BeNull();
        }
    }
}
