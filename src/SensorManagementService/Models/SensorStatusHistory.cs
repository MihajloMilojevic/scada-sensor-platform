using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SensorManagementService.Models;

public class SensorStatusHistory
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    [Column("sensor_id")]
    public string SensorId { get; set; } = "";

    [Column("old_status")]
    public string? OldStatus { get; set; }

    [Column("new_status")]
    public string NewStatus { get; set; } = "";

    [Column("reason")]
    public string Reason { get; set; } = "";

    [Column("changed_at")]
    public DateTimeOffset ChangedAt { get; set; } = DateTimeOffset.UtcNow;
}
