using Serilog.Context;

namespace CDRS.Web.Middleware
{
    /// <summary>
    /// Assigns a unique Correlation ID to each HTTP request.
    /// This ID is included in all log entries for the request,
    /// making it possible to trace a single request across all log lines.
    /// </summary>
    public class CorrelationIdMiddleware
    {
        private const string CorrelationIdHeader = "X-Correlation-ID";
        private readonly RequestDelegate _next;

        public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

        public async Task InvokeAsync(HttpContext context)
        {
            if (!context.Request.Headers.TryGetValue(
                CorrelationIdHeader, out var correlationId))
            {
                correlationId = Guid.NewGuid().ToString();
            }

            context.Response.Headers.Append(CorrelationIdHeader, correlationId.ToString());

            using (LogContext.PushProperty("CorrelationId", correlationId.ToString()))
            {
                await _next(context);
            }
        }
    }
}
