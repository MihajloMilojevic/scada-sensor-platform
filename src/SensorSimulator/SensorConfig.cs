namespace SensorSimulator;

public class SensorConfig
{
    public string SensorId { get; set; } = "sensor-01";
    public double ValueMin { get; set; } = 250;
    public double ValueMax { get; set; } = 400;
    public double AlarmP1Threshold { get; set; } = 350;
    public double AlarmP2Threshold { get; set; } = 370;
    public double AlarmP3Threshold { get; set; } = 390;

    public int GetAlarmPriority(double value)
    {
        if (value >= AlarmP3Threshold) return 3;
        if (value >= AlarmP2Threshold) return 2;
        if (value >= AlarmP1Threshold) return 1;
        return 0;
    }
}
