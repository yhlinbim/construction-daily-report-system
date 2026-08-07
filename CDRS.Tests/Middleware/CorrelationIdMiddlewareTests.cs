using CDRS.Web.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace CDRS.UnitTests.Middleware;

/// <summary>
/// Tests for CorrelationIdMiddleware.
///
/// These are pure unit tests against DefaultHttpContext — no real HTTP
/// pipeline or WebApplicationFactory is needed, since the middleware's
/// entire responsibility is reading/writing headers and pushing a
/// LogContext property. Keeping this fast and isolated matches the
/// project's existing testing philosophy (see DailyReportServiceTests).
///
/// Regression context: this middleware originally only wrote the
/// generated Correlation ID to Response.Headers. GlobalExceptionMiddleware
/// reads Request.Headers when building error responses, so any request
/// without an incoming X-Correlation-ID would show "none" in error
/// payloads — even though the response header and Serilog logs had the
/// real ID. [CDRA-61]
/// </summary>
public class CorrelationIdMiddlewareTests
{
    private const string CorrelationIdHeader = "X-Correlation-ID";

    // =============================================
    // No incoming correlation ID
    // =============================================

    [Fact]
    public async Task InvokeAsync_WhenNoIncomingCorrelationId_ShouldGenerateAndWriteToResponseHeader()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers.Should().ContainKey(CorrelationIdHeader);
        context.Response.Headers[CorrelationIdHeader].ToString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WhenNoIncomingCorrelationId_ShouldAlsoWriteGeneratedIdToRequestHeader()
    {
        // Regression test for CDRA-61.
        // Without this fix, GlobalExceptionMiddleware (which reads from
        // Request.Headers, not Response.Headers) would fall back to "none"
        // for any request that didn't already carry a correlation ID.

        // Arrange
        var context = new DefaultHttpContext();
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Request.Headers.Should().ContainKey(CorrelationIdHeader);

        var requestId = context.Request.Headers[CorrelationIdHeader].ToString();
        var responseId = context.Response.Headers[CorrelationIdHeader].ToString();

        requestId.Should().NotBeNullOrEmpty();
        requestId.Should().Be(responseId,
            "the same generated ID must be visible to both downstream " +
            "middleware (via Request.Headers) and the caller (via Response.Headers)");
    }

    [Fact]
    public async Task InvokeAsync_WhenNoIncomingCorrelationId_ShouldGenerateAValidGuid()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var generatedId = context.Request.Headers[CorrelationIdHeader].ToString();
        Guid.TryParse(generatedId, out _).Should().BeTrue(
            "the fallback ID should be a valid GUID, not an empty or malformed value");
    }

    // =============================================
    // Incoming correlation ID already present
    // =============================================

    [Fact]
    public async Task InvokeAsync_WhenIncomingCorrelationIdExists_ShouldPreserveItUnchanged()
    {
        // Arrange
        const string incomingId = "caller-supplied-id-12345";
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdHeader] = incomingId;

        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert — the middleware must not overwrite a caller-supplied ID
        context.Request.Headers[CorrelationIdHeader].ToString().Should().Be(incomingId);
        context.Response.Headers[CorrelationIdHeader].ToString().Should().Be(incomingId);
    }

    // =============================================
    // Pipeline continuation
    // =============================================

    [Fact]
    public async Task InvokeAsync_ShouldCallNextDelegateExactlyOnce()
    {
        // Arrange
        var nextCallCount = 0;
        var context = new DefaultHttpContext();
        var middleware = new CorrelationIdMiddleware(_ =>
        {
            nextCallCount++;
            return Task.CompletedTask;
        });

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCallCount.Should().Be(1);
    }

    [Fact]
    public async Task InvokeAsync_WhenNextThrows_ShouldStillHaveWrittenCorrelationIdBeforePropagating()
    {
        // This documents the ordering guarantee that makes CDRA-61's fix meaningful:
        // the correlation ID must be written to Request.Headers BEFORE calling
        // _next(), so that downstream exception handling (GlobalExceptionMiddleware)
        // can read it even when the request ultimately fails.

        // Arrange
        var context = new DefaultHttpContext();
        var middleware = new CorrelationIdMiddleware(_ =>
            throw new InvalidOperationException("Simulated downstream failure"));

        // Act
        var act = async () => await middleware.InvokeAsync(context);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        context.Request.Headers[CorrelationIdHeader].ToString().Should().NotBeNullOrEmpty();
    }
}