using CDRS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CDRS.Infrastructure.Persistence.Configurations
{
    public class DailyReportConfiguration : IEntityTypeConfiguration<DailyReport>
    {
        public void Configure(EntityTypeBuilder<DailyReport> builder)
        {
            builder.ToTable("DailyReports");
            builder.HasKey(e => e.Id);

            builder.Property(e => e.ProjectId)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(e => e.SiteWorkerId)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(e => e.WorkDescription)
                .IsRequired()
                .HasMaxLength(1000);

            builder.Property(e => e.WeatherCondition)
                .HasMaxLength(50);

            builder.Property(e => e.Status)
                .HasConversion<string>()
                .HasMaxLength(20);

            builder.Property(e => e.RejectionReason)
                .HasMaxLength(500);

            // 常用查詢的索引
            builder.HasIndex(e => e.ProjectId);
            builder.HasIndex(e => new { e.ProjectId, e.ReportDate });
            builder.HasIndex(e => e.Status);
        }
    }
}
