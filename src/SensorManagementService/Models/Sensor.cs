using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SensorManagementService.Models;

public class Sensor
{
    [Key]
    [Column("sensor_id")]
    public string SensorId { get; set; } = "";

    [Column("status")]
    public string Status { get; set; } = "ACTIVE";

    [Column("quality")]
    public string Quality { get; set; } = "GOOD";

    [Column("value_min")]
    public double? ValueMin { get; set; }

    [Column("value_max")]
    public double? ValueMax { get; set; }

    [Column("alarm_thresholds", TypeName = "jsonb")]
    public string? AlarmThresholds { get; set; }

    [Column("last_seen_at")]
    public DateTimeOffset? LastSeenAt { get; set; }

    [Column("blocked_until_at")]
    public DateTimeOffset? BlockedUntilAt { get; set; }

    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
