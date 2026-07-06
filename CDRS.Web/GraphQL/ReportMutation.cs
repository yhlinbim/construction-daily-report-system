using CDRS.Application.Interfaces;
using CDRS.Domain.Entities;
using CDRS.Domain.Exceptions;

namespace CDRS.Web.GraphQL
{
    public class ReportMutation
{
    /// <summary>
    /// Creates a new daily report via GraphQL Mutation.
    /// Demonstrates that GraphQL can handle write operations,
    /// not just queries.
    /// </summary>
    public async Task<DailyReport> CreateReport(
        CreateReportInput input,
        [Service] IDailyReportService reportService,
        CancellationToken cancellationToken = default)
    {
        return await reportService.CreateReportAsync(
            input.ProjectId,
            input.SiteWorkerId,
            input.ReportDate,
            input.WorkDescription,
            input.WorkerCount,
            input.WeatherCondition,
            cancellationToken);
    }

    /// <summary>
    /// Submits a report for review via GraphQL Mutation.
    /// </summary>
    public async Task<MutationResult> SubmitReport(
        Guid reportId,
        [Service] IDailyReportService reportService,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await reportService.SubmitReportAsync(reportId, cancellationToken);
            return new MutationResult(true, "Report submitted successfully.");
        }
        catch (KeyNotFoundException)
        {
            return new MutationResult(false, $"Report {reportId} not found.");
        }
        catch (DomainException ex)
        {
            return new MutationResult(false, ex.Message);
        }
    }
}

/// <summary>
/// Input type for creating a daily report.
/// GraphQL uses Input types for Mutation arguments.
/// </summary>
public record CreateReportInput(
    string ProjectId,
    string SiteWorkerId,
    DateTime ReportDate,
    string WorkDescription,
    int WorkerCount,
    string WeatherCondition);

/// <summary>
/// Result type for Mutations that don't return an entity.
/// Provides success status and message instead of throwing exceptions.
/// </summary>
public record MutationResult(bool Success, string Message);
}
