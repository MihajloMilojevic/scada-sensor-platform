using Microsoft.EntityFrameworkCore;
using SensorManagementService.Models;

namespace SensorManagementService.Data;

public class SensorMgmtDbContext(DbContextOptions<SensorMgmtDbContext> options)
    : DbContext(options)
{
    public DbSet<Sensor> Sensors => Set<Sensor>();
    public DbSet<SensorStatusHistory> SensorStatusHistory => Set<SensorStatusHistory>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Sensor>().ToTable("sensors");
        mb.Entity<SensorStatusHistory>().ToTable("sensor_status_history");
    }
}
