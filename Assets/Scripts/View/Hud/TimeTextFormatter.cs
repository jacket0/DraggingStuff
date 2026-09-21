using System;

public static class TimeTextFormatter
{
    public static string FormatMilliseconds(long milliseconds)
    {
        if (milliseconds < 0)
            throw new ArgumentOutOfRangeException(nameof(milliseconds));

        long totalSeconds = (milliseconds + 999) / 1000;
        return FormatSeconds(totalSeconds);
    }

    public static string FormatSeconds(float seconds)
    {
        if (seconds < 0f || float.IsNaN(seconds) || float.IsInfinity(seconds))
            throw new ArgumentOutOfRangeException(nameof(seconds));

        return FormatSeconds((long)Math.Ceiling(seconds));
    }

    private static string FormatSeconds(long totalSeconds)
    {
        long minutes = totalSeconds / 60;
        long seconds = totalSeconds % 60;
        return $"{minutes:00}:{seconds:00}";
    }
}
