namespace CDRS.Web.Models
{
    /// <summary>
    /// V2 response format for DailyReport.
    /// Breaking change from v1: status is now a string instead of an integer.
    /// </summary>
    public class ReportV2Response
    {
        public Guid Id { get; set; }
        public string ProjectId { get; set; } = string.Empty;
        public string SiteWorkerId { get; set; } = string.Empty;
        public DateTime ReportDate { get; set; }
        public string WorkDescription { get; set; } = string.Empty;
        public int WorkerCount { get; set; }
        public string WeatherCondition { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;    // v2：字串
        public int StatusCode { get; set; }                    // v2：新增欄位
        public DateTime CreatedAtUtc { get; set; }
        public string? RejectionReason { get; set; }
    }
}
