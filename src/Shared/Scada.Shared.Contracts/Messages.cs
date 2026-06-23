using System;

namespace Scada.Shared.Contracts;

// Full schemas populated per-phase; placeholders for compile-time references

public record SensorDataMessage
{
    public string SensorId { get; init; } = "";
    public double Value { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public int AlarmPriority { get; init; }
    public SensorQuality Quality { get; init; }
}

public record SensorStatusMessage
{
    public string SensorId { get; init; } = "";
    public SensorStatus Status { get; init; }
    public SensorStatus PreviousStatus { get; init; }
    public string Reason { get; init; } = "";
    public DateTimeOffset Timestamp { get; init; }
}

public record ConsensusResultMessage
{
    public string SensorId { get; init; } = "";
    public SensorQuality PreviousQuality { get; init; }
    public SensorQuality NewQuality { get; init; }
    public double ConsensusValue { get; init; }
    public double SensorValue { get; init; }
    public double DeviationSigma { get; init; }
    public DateTimeOffset Timestamp { get; init; }
}