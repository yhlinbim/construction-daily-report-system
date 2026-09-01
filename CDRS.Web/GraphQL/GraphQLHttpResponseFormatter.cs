using System.Net;
using HotChocolate.AspNetCore.Serialization;
using HotChocolate.Execution;

namespace CDRS.Web.GraphQL
{
    /// <summary>
    /// Returns HTTP 200 for any well-formed GraphQL response (one that carries
    /// an <c>errors</c> array), rather than HotChocolate 13's default 500 for a
    /// request that produced only errors and no data.
    ///
    /// This matches the transport convention used by GitHub, Apollo Server and
    /// Hasura: GraphQL execution errors - including authorization failures -
    /// are a 200 with an <c>errors</c> array that the client inspects. An
    /// unauthenticated GraphQL call is therefore a 200 with an
    /// <c>AUTH_NOT_AUTHORIZED</c> error, not a 500.
    ///
    /// Trade-off: an unhandled resolver exception is also delivered as a 200
    /// error response, so it will not show up in HTTP 5xx metrics. Resolver
    /// failures are monitored through Serilog / Application Insights instead.
    /// Genuine transport failures (no <c>errors</c> array) still return 500.
    /// </summary>
    public sealed class GraphQLHttpResponseFormatter : DefaultHttpResponseFormatter
    {
        protected override HttpStatusCode OnDetermineStatusCode(
            IQueryResult result, FormatInfo format, HttpStatusCode? proposedStatusCode)
        {
            var statusCode = base.OnDetermineStatusCode(result, format, proposedStatusCode);

            if (statusCode == HttpStatusCode.InternalServerError
                && result.Errors is { Count: > 0 })
            {
                return HttpStatusCode.OK;
            }

            return statusCode;
        }
    }
}
