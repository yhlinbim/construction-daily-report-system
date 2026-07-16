using CDRS.Domain.Entities;
using CDRS.Domain.Enums;
using CDRS.Domain.Exceptions;
using FluentAssertions;

namespace CDRS.Tests.Domain
{
    /// <summary>
    /// Tests for DailyReport domain entity.
    ///
    /// Design decisions documented here:
    /// - [Theory] with [InlineData] is used where the same behaviour must hold
    ///   across multiple inputs — avoids duplicating test code.
    /// - Private setters on DailyReport mean we use reflection in ForceStatus()
    ///   to test guard clauses from non-default starting states. This is intentional:
    ///   in production code, state is only changed through domain methods.
    /// - Test method names follow the pattern:
    ///   MethodName_Condition_ExpectedBehaviour
    /// </summary>
    public class DailyReportTests
    {
        // =============================================
        // Factory method tests
        // =============================================

        [Fact]
        public void Create_WithValidParameters_ShouldInitialiseInDraftStatus()
        {
            var report = CreateValidReport();

            report.Status.Should().Be(ReportStatus.Draft);
            report.Id.Should().NotBe(Guid.Empty);
            report.ProjectId.Should().Be("PROJ-001");
            report.WorkerCount.Should().Be(5);
        }

        [Fact]
        public void Create_ShouldTrimWhitespaceFromStringFields()
        {
            var report = DailyReport.Create(
                "  PROJ-001  ",
                "  EMP001  ",
                DateTime.Today,
                "  Foundation works  ",
                5,
                "Fine");

            report.ProjectId.Should().Be("PROJ-001");
            report.SiteWorkerId.Should().Be("EMP001");
            report.WorkDescription.Should().Be("Foundation works");
        }

        [Theory]
        [InlineData("", "EMP001", "Foundation works", 5)]
        [InlineData("PROJ-001", "EMP001", "", 5)]
        [InlineData("PROJ-001", "EMP001", "Foundation works", 0)]
        [InlineData("PROJ-001", "EMP001", "Foundation works", -1)]
        public void Create_WithInvalidParameters_ShouldThrowDomainException(
            string projectId, string workerId, string description, int workerCount)
        {
            var act = () => DailyReport.Create(
                projectId, workerId, DateTime.Today,
                description, workerCount, "Fine");

            act.Should().Throw<DomainException>();
        }

        // =============================================
        // State machine: Submit
        // =============================================

        [Fact]
        public void Submit_WhenDraft_ShouldTransitionToSubmitted()
        {
            var report = CreateValidReport();
            report.Submit();
            report.Status.Should().Be(ReportStatus.Submitted);
        }

        [Fact]
        public void Submit_WhenAlreadySubmitted_ShouldThrowDomainException()
        {
            var report = CreateValidReport();
            report.Submit();
            var act = () => report.Submit();
            act.Should().Throw<DomainException>().WithMessage("*submitted*");
        }

        [Theory]
        [InlineData(ReportStatus.Approved)]
        [InlineData(ReportStatus.Rejected)]
        [InlineData(ReportStatus.UnderReview)]
        public void Submit_WhenNotDraft_ShouldThrowDomainException(ReportStatus invalidStatus)
        {
            var report = CreateValidReport();
            ForceStatus(report, invalidStatus);
            var act = () => report.Submit();
            act.Should().Throw<DomainException>();
        }

        // =============================================
        // State machine: Approve
        // =============================================

        [Fact]
        public void Approve_WhenUnderReview_ShouldTransitionToApproved()
        {
            var report = CreateValidReport();
            report.Submit();
            report.StartReview();
            report.Approve();
            report.Status.Should().Be(ReportStatus.Approved);
        }

        [Fact]
        public void Approve_WhenSubmittedButNotUnderReview_ShouldThrowDomainException()
        {
            var report = CreateValidReport();
            report.Submit();
            var act = () => report.Approve();
            //act.Should().Throw<DomainException>().WithMessage("*reviewed*");
            // 改成這樣，匹配實際訊息
            act.Should().Throw<DomainException>().WithMessage("*under review*");
        }

        [Fact]
        public void Approve_WhenAlreadyApproved_ShouldThrowDomainException()
        {
            var report = CreateValidReport();
            report.Submit();
            report.StartReview();
            report.Approve();
            var act = () => report.Approve();
            act.Should().Throw<DomainException>();
        }

        // =============================================
        // State machine: Reject
        // =============================================

        [Fact]
        public void Reject_WhenUnderReview_ShouldTransitionToRejectedWithReason()
        {
            var report = CreateValidReport();
            report.Submit();
            report.StartReview();
            report.Reject("Insufficient detail on foundation works.");
            report.Status.Should().Be(ReportStatus.Rejected);
            report.RejectionReason.Should().Be("Insufficient detail on foundation works.");
        }

        [Fact]
        public void Reject_WithEmptyReason_ShouldThrowDomainException()
        {
            var report = CreateValidReport();
            report.Submit();
            report.StartReview();
            var act = () => report.Reject("");
            act.Should().Throw<DomainException>().WithMessage("*reason*");
        }

        [Fact]
        public void Reject_WhenApproved_ShouldThrowDomainException()
        {
            var report = CreateValidReport();
            report.Submit();
            report.StartReview();
            report.Approve();
            var act = () => report.Reject("Changed my mind.");
            act.Should().Throw<DomainException>();
        }

        [Fact]
        public void Reject_WhenDraft_ShouldThrowDomainException()
        {
            var report = CreateValidReport();
            var act = () => report.Reject("Not relevant.");
            act.Should().Throw<DomainException>();
        }

        // =============================================
        // Helper methods
        // =============================================

        private static DailyReport CreateValidReport() =>
            DailyReport.Create(
                "PROJ-001",
                "EMP001",
                DateTime.Today,
                "Completed foundation formwork on Grid A.",
                5,
                "Fine");

        private static void ForceStatus(DailyReport report, ReportStatus status)
        {
            var prop = typeof(DailyReport)
                .GetProperty(nameof(DailyReport.Status),
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.Instance);
            prop?.SetValue(report, status);
        }
    }
}
