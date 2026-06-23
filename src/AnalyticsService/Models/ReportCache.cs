using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AnalyticsService.Models;

public class ReportCache
{
    [Key]
    [Column("cache_key")]
    public string CacheKey { get; set; } = "";

    [Column("payload", TypeName = "jsonb")]
    public string Payload { get; set; } = "";

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("expires_at")]
    public DateTimeOffset ExpiresAt { get; set; }
}
