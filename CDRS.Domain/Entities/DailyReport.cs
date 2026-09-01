using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CDRS.Domain.Enums;
using CDRS.Domain.Exceptions;

namespace CDRS.Domain.Entities
{
    public class DailyReport
    {
        public Guid Id { get; private set; }
        public string ProjectId { get; private set; } = string.Empty;
        public string SiteWorkerId { get; private set; } = string.Empty;
        public DateTime ReportDate { get; private set; }
        public string WorkDescription { get; private set; } = string.Empty;
        public int WorkerCount { get; private set; }
        public string WeatherCondition { get; private set; } = string.Empty;
        public ReportStatus Status { get; private set; }
        public DateTime CreatedAtUtc { get; private set; }
        public string? RejectionReason { get; private set; }

        /// <summary>
        /// Optimistic-concurrency token. Incremented on every state transition
        /// and used by EF Core to detect a report that changed between load
        /// and save (e.g. two reviewers acting on it at once).
        /// </summary>
        public int Version { get; private set; }

        private DailyReport() { }

        public static DailyReport Create(
            string projectId,
            string siteWorkerId,
            DateTime reportDate,
            string workDescription,
            int workerCount,
            string weatherCondition)
        {
            if (string.IsNullOrWhiteSpace(projectId))
                throw new DomainException("Project ID is required.");
            if (string.IsNullOrWhiteSpace(workDescription))
                throw new DomainException("Work description is required.");
            if (workerCount <= 0)
                throw new DomainException("Worker count must be greater than zero.");

            return new DailyReport
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId.Trim(),
                SiteWorkerId = siteWorkerId.Trim(),
                ReportDate = reportDate.Date,
                WorkDescription = workDescription.Trim(),
                WorkerCount = workerCount,
                WeatherCondition = weatherCondition.Trim(),
                Status = ReportStatus.Draft,
                CreatedAtUtc = DateTime.UtcNow,
                Version = 1
            };
        }

        public void Submit()
        {
            if (Status != ReportStatus.Draft)
                throw new DomainException($"Only draft reports can be submitted. Current status: {Status}");
            Status = ReportStatus.Submitted;
            Version++;
        }

        public void StartReview()
        {
            if (Status != ReportStatus.Submitted)
                throw new DomainException($"Only submitted reports can be reviewed. Current status: {Status}");
            Status = ReportStatus.UnderReview;
            Version++;
        }

        public void Approve()
        {
            if (Status != ReportStatus.UnderReview)
                throw new DomainException($"Only reports under review can be approved. Current status: {Status}");
            Status = ReportStatus.Approved;
            Version++;
        }

        public void Reject(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
                throw new DomainException("Rejection reason is required.");
            if (Status is ReportStatus.Approved or ReportStatus.Draft)
                throw new DomainException($"Reports in '{Status}' status cannot be rejected.");
            Status = ReportStatus.Rejected;
            RejectionReason = reason.Trim();
            Version++;
        }
    }
}
