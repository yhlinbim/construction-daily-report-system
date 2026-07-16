using CDRS.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestPlatform.TestHost;
using System.Net;

namespace CDRS.Tests.Integration
{
    /// <summary>
    /// Integration tests for ReportController and ApprovalController.
    ///
    /// Uses WebApplicationFactory to spin up the full ASP.NET Core pipeline.
    /// Tests verify HTTP routing and response codes.
    /// InMemory database replaces real SQL connection for test isolation.
    /// </summary>
    public class ReportControllerTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;
        private readonly HttpClient _client;

        public ReportControllerTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
            _client = _factory.CreateClient();
        }

        [Fact]
        public async Task Index_ShouldReturn200OK()
        {
            var response = await _client.GetAsync("/Report/Index");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Index_WithProjectId_ShouldReturn200OK()
        {
            var response = await _client.GetAsync("/Report/Index?projectId=PROJ-001");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Create_Get_ShouldReturn200OK()
        {
            var response = await _client.GetAsync("/Report/Create?projectId=PROJ-001");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Queue_ShouldReturn200OK()
        {
            var response = await _client.GetAsync("/Approval/Queue");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Swagger_ShouldReturn200OK()
        {
            var response = await _client.GetAsync("/swagger/index.html");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }
}
