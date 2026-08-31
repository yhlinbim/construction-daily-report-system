using CDRS.Domain.Entities;

namespace CDRS.Infrastructure.Persistence
{
    /// <summary>
    /// Seeds a small set of sample reports for local development so the API
    /// returns something on first run. Only touches an empty database and is
    /// only invoked from the Development startup path.
    /// </summary>
    public static class DevelopmentDataSeeder
    {
        public static void Seed(AppDbContext context)
        {
            if (context.DailyReports.Any())
                return;

            var today = DateTime.UtcNow.Date;

            var draft = DailyReport.Create(
                "PROJ-001", "EMP001", today,
                "Curing of the level 2 slab and general site cleanup.", 4, "Rain");

            var submitted = DailyReport.Create(
                "PROJ-001", "EMP002", today.AddDays(-1),
                "Rebar fixing for columns C1-C6, north elevation.", 6, "Overcast");
            submitted.Submit();

            var underReview = DailyReport.Create(
                "PROJ-002", "EMP003", today.AddDays(-1),
                "Concrete pour, level 2 slab, grids A-D.", 12, "Fine");
            underReview.Submit();
            underReview.StartReview();

            var approved = DailyReport.Create(
                "PROJ-001", "EMP001", today.AddDays(-2),
                "Foundation formwork, grids A-C.", 8, "Fine");
            approved.Submit();
            approved.StartReview();
            approved.Approve();

            context.DailyReports.AddRange(draft, submitted, underReview, approved);
            context.SaveChanges();
        }
    }
}
