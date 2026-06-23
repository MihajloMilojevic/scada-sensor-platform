using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ConsensusService.Models;

public class QualityChange
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public long Id { get; set; }

    [Column("sensor_id")]        public string SensorId       { get; set; } = "";
    [Column("previous_quality")] public string PreviousQuality { get; set; } = "";
    [Column("new_quality")]      public string NewQuality      { get; set; } = "";
    [Column("sensor_value")]     public double SensorValue     { get; set; }
    [Column("consensus_value")]  public double ConsensusValue  { get; set; }
    [Column("deviation_sigma")]  public double DeviationSigma  { get; set; }
    [Column("changed_at")]       public DateTimeOffset ChangedAt { get; set; } = DateTimeOffset.UtcNow;
}
