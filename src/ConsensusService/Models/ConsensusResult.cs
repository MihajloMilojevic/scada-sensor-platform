using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ConsensusService.Models;

public class ConsensusResult
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public long Id { get; set; }

    [Column("window_start")] public DateTimeOffset WindowStart { get; set; }
    [Column("window_end")]   public DateTimeOffset WindowEnd   { get; set; }
    [Column("consensus_value")] public double ConsensusValue { get; set; }
    [Column("contributing_sensors")] public int ContributingSensors { get; set; }
    [Column("created_at")] public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
