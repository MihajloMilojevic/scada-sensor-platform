using ConsensusService.Models;
using Microsoft.EntityFrameworkCore;

namespace ConsensusService.Data;

public class ConsensusDbContext(DbContextOptions<ConsensusDbContext> options) : DbContext(options)
{
    public DbSet<ConsensusResult> ConsensusResults => Set<ConsensusResult>();
    public DbSet<QualityChange>   QualityChanges   => Set<QualityChange>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<ConsensusResult>().ToTable("consensus_results");
        mb.Entity<QualityChange>().ToTable("quality_changes");
    }
}
