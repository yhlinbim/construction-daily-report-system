using CDRS.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestPlatform.TestHost;
using System.Net;

namespace CDRS.UnitTests.Integration
{
    /// <summary>
    /// Integration tests for ReportController and ApprovalController.
    ///
    /// Uses WebApplicationFactory to spin up the full ASP.NET Core pipeline.
    /// Tests verify HTTP routing and response codes.
    /// InMemory database replaces real SQL connection for test isolation.
    /// </summary>
    public class ReportControllerTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;

        public ReportControllerTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    var descriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                    if (descriptor != null)
                        services.Remove(descriptor);

                    services.AddDbContext<AppDbContext>(options =>
                        options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
                });
            });

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
