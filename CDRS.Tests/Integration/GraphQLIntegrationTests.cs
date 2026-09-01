using FluentAssertions;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CDRS.Tests.Integration
{
    /// <summary>
    /// Integration tests for the GraphQL endpoint.
    ///
    /// Covers:
    /// 1. Schema construction — a regression guard for CDRA-66, where adding
    ///    [Authorize] to ReportMutation broke the schema (every /graphql
    ///    request returned HTTP 500) because AddAuthorization() was never
    ///    registered on the GraphQL server.
    /// 2. Authorization parity with the REST API (CDRA-67): queries and
    ///    mutations must enforce the same rules as the equivalent REST
    ///    endpoints — no anonymous data access, reviewer-only pending queue,
    ///    worker-only writes.
    /// 3. HTTP transport (CDRA-75): a well-formed GraphQL response, including
    ///    an authorization failure, is 200 with an errors array — not 500.
    /// </summary>
    public class GraphQLIntegrationTests
        : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;

        public GraphQLIntegrationTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private HttpClient Client(string? role = null)
        {
            var client = _factory.CreateClient();
            if (role is not null)
            {
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", JwtTestHelper.GenerateToken(role));
            }
            return client;
        }

        private static StringContent GraphQLBody(string query)
            => new(
                JsonSerializer.Serialize(new { query }),
                Encoding.UTF8,
                "application/json");

        private static async Task<(HttpStatusCode status, JsonDocument body)> PostAsync(
            HttpClient client, string query)
        {
            var response = await client.PostAsync("/graphql", GraphQLBody(query));
            var raw = await response.Content.ReadAsStringAsync();

            raw.Should().NotContain("SchemaException",
                "the GraphQL schema must always build (CDRA-66 regression guard)");

            // A well-formed GraphQL response - success or a plain errors array,
            // including an authorization failure - is HTTP 200 (CDRA-75).
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            return (response.StatusCode, JsonDocument.Parse(raw));
        }

        private static string? FirstErrorCode(JsonDocument doc)
            => doc.RootElement.TryGetProperty("errors", out var errors) && errors.GetArrayLength() > 0
                ? errors[0].GetProperty("extensions").GetProperty("code").GetString()
                : null;

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

        private const string PendingReviewsQuery = "{ pendingReviews { id status } }";
        private const string ReportsByProjectQuery = @"{ reportsByProject(projectId: ""PROJ-001"") { id } }";

        // ---- schema construction ------------------------------------------------

        [Fact]
        public async Task Introspection_Anonymous_ShouldReturn200_SchemaLoads()
        {
            var (status, body) = await PostAsync(Client(),
                "{ __schema { queryType { name } mutationType { name } } }");

            status.Should().Be(HttpStatusCode.OK);
            body.RootElement.GetProperty("data").GetProperty("__schema")
                .GetProperty("mutationType").GetProperty("name")
                .GetString().Should().NotBeNullOrEmpty();
        }

        // ---- query authorization (CDRA-67) ------------------------------------

        [Fact]
        public async Task ReportsByProject_Anonymous_ShouldBeDenied()
        {
            var (_, body) = await PostAsync(Client(), ReportsByProjectQuery);
            FirstErrorCode(body).Should().Be("AUTH_NOT_AUTHORIZED");
        }

        [Fact]
        public async Task ReportsByProject_WithWorkerToken_ShouldSucceed()
        {
            var (_, body) = await PostAsync(Client("Worker"), ReportsByProjectQuery);

            FirstErrorCode(body).Should().BeNull();
            body.RootElement.GetProperty("data").GetProperty("reportsByProject")
                .ValueKind.Should().Be(JsonValueKind.Array);
        }

        [Fact]
        public async Task PendingReviews_Anonymous_ShouldBeDenied()
        {
            var (_, body) = await PostAsync(Client(), PendingReviewsQuery);
            FirstErrorCode(body).Should().Be("AUTH_NOT_AUTHORIZED");
        }

        [Fact]
        public async Task PendingReviews_WithWorkerToken_ShouldBeDenied()
        {
            var (_, body) = await PostAsync(Client("Worker"), PendingReviewsQuery);
            FirstErrorCode(body).Should().Be("AUTH_NOT_AUTHORIZED");
        }

        [Fact]
        public async Task PendingReviews_WithSupervisorToken_ShouldSucceed()
        {
            var (_, body) = await PostAsync(Client("Supervisor"), PendingReviewsQuery);

            FirstErrorCode(body).Should().BeNull();
            body.RootElement.GetProperty("data").GetProperty("pendingReviews")
                .ValueKind.Should().Be(JsonValueKind.Array);
        }

        // ---- mutation authorization -----------------------------------------

        [Fact]
        public async Task CreateReport_Anonymous_ShouldBeDenied()
        {
            var (_, body) = await PostAsync(Client(), CreateReportMutation);
            FirstErrorCode(body).Should().Be("AUTH_NOT_AUTHORIZED");
        }

        [Fact]
        public async Task CreateReport_WithSupervisorToken_ShouldBeDenied()
        {
            var (_, body) = await PostAsync(Client("Supervisor"), CreateReportMutation);
            FirstErrorCode(body).Should().Be("AUTH_NOT_AUTHORIZED");
        }

        [Fact]
        public async Task CreateReport_WithWorkerToken_ShouldBeAccepted()
        {
            var (status, body) = await PostAsync(Client("Worker"), CreateReportMutation);

            status.Should().Be(HttpStatusCode.OK);
            // A Worker is authorized — any error must not be an auth denial.
            FirstErrorCode(body).Should().NotBe("AUTH_NOT_AUTHORIZED");
        }
    }
}
