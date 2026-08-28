using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Headers;

namespace CDRS.Tests.Integration
{
    /// <summary>
    /// Integration tests for ApprovalController (MVC).
    ///
    /// Verifies role-based access control:
    /// - Anonymous requests are rejected with 401
    /// - Worker role is rejected with 403
    /// - Supervisor and ProjectManager roles are granted access
    /// </summary>
    public class ApprovalControllerTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;

        public ApprovalControllerTests(CustomWebApplicationFactory factory)
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
        public async Task Queue_WithoutToken_ShouldReturn401()
        {
            var client = CreateClientWithoutToken();
            var response = await client.GetAsync("/Approval/Queue");
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task Queue_WithWorkerToken_ShouldReturn403()
        {
            var client = CreateClientWithToken("Worker");
            var response = await client.GetAsync("/Approval/Queue");
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task Queue_WithSupervisorToken_ShouldReturn200()
        {
            var client = CreateClientWithToken("Supervisor");
            var response = await client.GetAsync("/Approval/Queue");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Queue_WithProjectManagerToken_ShouldReturn200()
        {
            var client = CreateClientWithToken("ProjectManager");
            var response = await client.GetAsync("/Approval/Queue");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }
}