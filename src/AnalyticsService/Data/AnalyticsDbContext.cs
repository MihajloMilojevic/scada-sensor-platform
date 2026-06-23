using AnalyticsService.Models;
using Microsoft.EntityFrameworkCore;

namespace AnalyticsService.Data;

public class AnalyticsDbContext(DbContextOptions<AnalyticsDbContext> options) : DbContext(options)
{
    public DbSet<ReportCache> ReportCache => Set<ReportCache>();

    protected override void OnModelCreating(ModelBuilder mb)
        => mb.Entity<ReportCache>().ToTable("report_cache");
}
