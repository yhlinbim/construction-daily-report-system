using FluentAssertions;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CDRS.Tests.Integration
{
    /// <summary>
    /// Integration tests for the GraphQL endpoint.
    /// Guards against the CDRA-66 regression where adding [Authorize] to
    /// ReportMutation broke schema construction (every /graphql request
    /// returned HTTP 500) because AddAuthorization() was never registered
    /// on the GraphQL server.
    /// </summary>
    public class GraphQLIntegrationTests
        : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;

        public GraphQLIntegrationTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private static StringContent GraphQLBody(string query)
            => new(
                JsonSerializer.Serialize(new { query }),
                Encoding.UTF8,
                "application/json");

        private const string CreateReportMutation = @"
            mutation {
              createReport(input: {
                projectId: ""PROJ-001""
                siteWorkerId: ""EMP001""
                reportDate: ""2026-01-01T00:00:00Z""
                workDescription: ""Foundation work on the north wing.""
                workerCount: 3
                weatherCondition: ""Fine""
              }) { id status }
            }";

        [Fact]
        public async Task Introspection_Anonymous_ShouldReturn200_SchemaLoads()
        {
            var client = _factory.CreateClient();

            var response = await client.PostAsync(
                "/graphql",
                GraphQLBody("{ __schema { queryType { name } mutationType { name } } }"));

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var body = await response.Content.ReadAsStringAsync();
            body.Should().NotContain("SchemaException");
            body.Should().Contain("mutationType");
        }

        [Fact]
        public async Task CreateReport_WithoutToken_ShouldReturnAuthorizationError()
        {
            var client = _factory.CreateClient();

            var response = await client.PostAsync("/graphql", GraphQLBody(CreateReportMutation));

            var body = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);

            doc.RootElement.TryGetProperty("errors", out var errors).Should().BeTrue(
                "an unauthenticated mutation must be rejected");
            errors.GetArrayLength().Should().BeGreaterThan(0);

            // The failure must be an authorization denial, not a schema build error.
            body.Should().NotContain("SchemaException");
            var code = errors[0].GetProperty("extensions").GetProperty("code").GetString();
            code.Should().Be("AUTH_NOT_AUTHORIZED");
        }

        [Fact]
        public async Task CreateReport_WithWorkerToken_ShouldBeAccepted_NotSchemaError()
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", JwtTestHelper.GenerateToken("Worker"));

            var response = await client.PostAsync("/graphql", GraphQLBody(CreateReportMutation));

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var body = await response.Content.ReadAsStringAsync();
            body.Should().NotContain("SchemaException");

            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("errors", out var errors)
                && errors.GetArrayLength() > 0)
            {
                // A Worker is authorized, so any error must not be an auth denial.
                var code = errors[0].GetProperty("extensions").GetProperty("code").GetString();
                code.Should().NotBe("AUTH_NOT_AUTHORIZED");
            }
        }
    }
}
