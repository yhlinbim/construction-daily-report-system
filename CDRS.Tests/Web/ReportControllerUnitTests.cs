using CDRS.Application.Interfaces;
using CDRS.Domain.Exceptions;
using CDRS.Web.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CDRS.Tests.Web
{
    /// <summary>
    /// Unit tests for the ReportController (MVC) actions in isolation.
    /// </summary>
    public class ReportControllerUnitTests
    {
        private readonly Mock<IDailyReportService> _service = new();
        private readonly ReportController _controller;

        public ReportControllerUnitTests()
        {
            _controller = new ReportController(
                _service.Object, NullLogger<ReportController>.Instance)
            {
                TempData = new TempDataDictionary(
                    new DefaultHttpContext(), Mock.Of<ITempDataProvider>())
            };
        }

        [Fact]
        public async Task Submit_WhenServiceThrowsDomainException_RedirectsWithError()
        {
            _service
                .Setup(s => s.SubmitReportAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new DomainException("Only draft reports can be submitted."));

            var result = await _controller.Submit(Guid.NewGuid(), "PROJ-001");

            result.Should().BeOfType<RedirectToActionResult>()
                .Which.ActionName.Should().Be(nameof(ReportController.Index));
            _controller.TempData["Error"].Should().Be("Only draft reports can be submitted.");
            _controller.TempData.ContainsKey("Success").Should().BeFalse();
        }

        [Fact]
        public async Task Submit_WhenSuccessful_RedirectsWithSuccess()
        {
            var result = await _controller.Submit(Guid.NewGuid(), "PROJ-001");

            result.Should().BeOfType<RedirectToActionResult>()
                .Which.ActionName.Should().Be(nameof(ReportController.Index));
            _controller.TempData["Success"].Should().Be("Report submitted for review.");
            _controller.TempData.ContainsKey("Error").Should().BeFalse();
        }
    }
}
