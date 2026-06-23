using System;

namespace SensorSimulator;

public static class ConsoleDisplay
{
    public static void Print(string sensorId, double value, DateTimeOffset timestamp, int alarmPriority, int httpStatus)
    {
        var color = alarmPriority switch
        {
            1 => ConsoleColor.Yellow,
            2 => ConsoleColor.DarkYellow,   // orange approximation
            3 => ConsoleColor.Red,
            _ => ConsoleColor.White
        };

        var label = alarmPriority switch
        {
            1 => "[P1]",
            2 => "[P2]",
            3 => "[P3]",
            _ => "    "
        };

        Console.ForegroundColor = color;
        Console.WriteLine($"{timestamp:HH:mm:ss.fff} {sensorId} {label} value={value:F2}  HTTP {httpStatus}");
        Console.ResetColor();
    }
}