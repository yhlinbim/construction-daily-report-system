using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Headers;

namespace CDRS.Tests.Integration
{
    /// <summary>
    /// Integration tests for ReportController (MVC).
    ///
    /// Verifies that all endpoints require authentication, that any
    /// authenticated role can view reports, and that creating a report
    /// is restricted to Worker (matching ReportApiController and the
    /// GraphQL mutations). The primary interface for this project is the
    /// Swagger REST API; these MVC controllers are protected but do not
    /// have a login UI.
    /// </summary>
    public class ReportControllerTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;

        public ReportControllerTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private HttpClient CreateClientWithToken(string role)
        {
            var client = _factory.CreateClient(
                new WebApplicationFactoryClientOptions
                {
                    AllowAutoRedirect = false
                });
            var token = JwtTestHelper.GenerateToken(role);
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
            return client;
        }

        private HttpClient CreateClientWithoutToken()
        {
            return _factory.CreateClient(
                new WebApplicationFactoryClientOptions
                {
                    AllowAutoRedirect = false
                });
        }

        [Fact]
        public async Task Index_WithoutToken_ShouldReturn401()
        {
            var client = CreateClientWithoutToken();
            var response = await client.GetAsync("/Report/Index");
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task Index_WithWorkerToken_ShouldReturn200()
        {
            var client = CreateClientWithToken("Worker");
            var response = await client.GetAsync("/Report/Index");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Index_WithProjectId_WithWorkerToken_ShouldReturn200()
        {
            var client = CreateClientWithToken("Worker");
            var response = await client.GetAsync("/Report/Index?projectId=PROJ-001");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Create_Get_WithoutToken_ShouldReturn401()
        {
            var client = CreateClientWithoutToken();
            var response = await client.GetAsync("/Report/Create?projectId=PROJ-001");
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task Create_Get_WithWorkerToken_ShouldReturn200()
        {
            var client = CreateClientWithToken("Worker");
            var response = await client.GetAsync("/Report/Create?projectId=PROJ-001");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Create_Get_WithSupervisorToken_ShouldReturn403()
        {
            var client = CreateClientWithToken("Supervisor");
            var response = await client.GetAsync("/Report/Create?projectId=PROJ-001");
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task Swagger_ShouldReturn200OK()
        {
            var client = CreateClientWithoutToken();
            var response = await client.GetAsync("/swagger/index.html");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }
}