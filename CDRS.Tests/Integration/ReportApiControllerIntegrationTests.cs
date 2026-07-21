using CDRS.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CDRS.Tests.Integration
{
    /// <summary>
    /// Integration tests for ReportApiController.
    /// Tests verify HTTP routing, JWT authentication, and response codes
    /// against the full ASP.NET Core pipeline including versioned routes.
    /// </summary>
    public class ReportApiControllerIntegrationTests
        : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;

        public ReportApiControllerIntegrationTests(
            CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private HttpClient CreateClientWithToken(string role)
        {
            var client = _factory.CreateClient();
            var token = JwtTestHelper.GenerateToken(role);
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
            return client;
        }

        private HttpClient CreateClientWithoutToken()
            => _factory.CreateClient();

        // =============================================
        // Authentication tests (401)
        // =============================================

        [Fact]
        public async Task GetByProject_V1_WithoutToken_ShouldReturn401()
        {
            var client = CreateClientWithoutToken();
            var response = await client.GetAsync("/api/v1/reports/PROJ-001");
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task CreateReport_V1_WithoutToken_ShouldReturn401()
        {
            var client = CreateClientWithoutToken();
            var content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    projectId = "PROJ-001",
                    siteWorkerId = "EMP001",
                    reportDate = DateTime.Today,
                    workDescription = "Foundation work.",
                    workerCount = 3,
                    weatherCondition = "Fine"
                }),
                Encoding.UTF8, "application/json");

            var response = await client.PostAsync("/api/v1/reports", content);
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        // =============================================
        // Authorisation tests (403)
        // =============================================

        [Fact]
        public async Task GetPendingReviews_V1_WithWorkerRole_ShouldReturn403()
        {
            var client = CreateClientWithToken("Worker");
            var response = await client.GetAsync("/api/v1/reports/pending");
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task ApproveReport_V1_WithWorkerRole_ShouldReturn403()
        {
            var client = CreateClientWithToken("Worker");
            var response = await client.PostAsync(
                $"/api/v1/reports/{Guid.NewGuid()}/approve",
                new StringContent("", Encoding.UTF8, "application/json"));
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        // =============================================
        // Authenticated requests (2xx)
        // =============================================

        [Fact]
        public async Task GetByProject_V1_WithWorkerToken_ShouldReturn200()
        {
            var client = CreateClientWithToken("Worker");
            var response = await client.GetAsync("/api/v1/reports/PROJ-001");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task GetByProject_V1_ResponseShouldBeJsonArray()
        {
            var client = CreateClientWithToken("Worker");
            var response = await client.GetAsync("/api/v1/reports/PROJ-001");

            response.Content.Headers.ContentType?.MediaType
                .Should().Be("application/json");
            var content = await response.Content.ReadAsStringAsync();
            var reports = JsonSerializer.Deserialize<List<JsonElement>>(content);
            reports.Should().NotBeNull();
        }

        [Fact]
        public async Task CreateReport_V1_WithValidData_ShouldReturn201()
        {
            var client = CreateClientWithToken("Worker");
            var content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    projectId = "PROJ-001",
                    siteWorkerId = "EMP001",
                    reportDate = DateTime.Today,
                    workDescription = "Foundation formwork completed.",
                    workerCount = 5,
                    weatherCondition = "Fine"
                }),
                Encoding.UTF8, "application/json");

            var response = await client.PostAsync("/api/v1/reports", content);
            response.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        [Fact]
        public async Task GetPendingReviews_V1_WithSupervisorToken_ShouldReturn200()
        {
            var client = CreateClientWithToken("Supervisor");
            var response = await client.GetAsync("/api/v1/reports/pending");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task GetPendingReviews_V1_WithProjectManagerToken_ShouldReturn200()
        {
            var client = CreateClientWithToken("ProjectManager");
            var response = await client.GetAsync("/api/v1/reports/pending");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // =============================================
        // Version header test
        // =============================================

        [Fact]
        public async Task GetByProject_V1_ResponseShouldContainVersionHeader()
        {
            var client = CreateClientWithToken("Worker");
            var response = await client.GetAsync("/api/v1/reports/PROJ-001");

            //response.Headers.Should().ContainKey("api-supported-versions");
            //response.Headers.GetValues("api-supported-versions")
            //    .First().Should().Be("1.0");

            // v1 is deprecated, so it appears in api-deprecated-versions
            // api-supported-versions contains all active versions (v1 and v2)
            response.Headers.Should().ContainKey("api-deprecated-versions");
            response.Headers.GetValues("api-deprecated-versions")
                .First().Should().Be("1.0");
        }

        // =============================================
        // Error response tests (4xx)
        // =============================================

        [Fact]
        public async Task Submit_V1_WithNonExistentId_ShouldReturn404()
        {
            var client = CreateClientWithToken("Worker");
            var response = await client.PostAsync(
                $"/api/v1/reports/{Guid.NewGuid()}/submit",
                new StringContent("", Encoding.UTF8, "application/json"));
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task CreateReport_V1_WithInvalidData_ShouldReturn400()
        {
            var client = CreateClientWithToken("Worker");
            var content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    projectId = "",
                    siteWorkerId = "EMP001",
                    reportDate = DateTime.Today,
                    workDescription = "Work.",
                    workerCount = -1,
                    weatherCondition = "Fine"
                }),
                Encoding.UTF8, "application/json");

            var response = await client.PostAsync("/api/v1/reports", content);
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task CreateReport_V1_ResponseShouldContainCreatedReport()
        {
            var client = CreateClientWithToken("Worker");
            var content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    projectId = "PROJ-001",
                    siteWorkerId = "EMP001",
                    reportDate = DateTime.Today,
                    workDescription = "Foundation formwork completed.",
                    workerCount = 5,
                    weatherCondition = "Fine"
                }),
                Encoding.UTF8, "application/json");

            var response = await client.PostAsync("/api/v1/reports", content);
            var body = await response.Content.ReadAsStringAsync();
            var report = JsonSerializer.Deserialize<JsonElement>(body);

            report.GetProperty("projectId").GetString().Should().Be("PROJ-001");
            report.GetProperty("status").GetInt32().Should().Be(0); // Draft
            report.GetProperty("workerCount").GetInt32().Should().Be(5);
        }
    }
}
