using System;

public static class DateTimeExtensions
{
    public static DateTime ConvertToEorzeaTime(this DateTime currentDateTime)
    {
        const double EORZEA_MULTIPLIER = 3600D / 175D;

        var epochTicks = currentDateTime.ToUniversalTime().Ticks - new DateTime(1970, 1, 1).Ticks;
        var eorzeaTicks = (long)Math.Round(epochTicks * EORZEA_MULTIPLIER);

        return new DateTime(eorzeaTicks);
    }

    public static string DetermineDayPeriod(this DateTime currentDateTime, bool useShorthand = false)
    {
        var hour = currentDateTime.Hour;
        var isGoldenHour = (hour >= 17 && hour < 19) || (hour >= 6 && hour < 8);
        var dayPeriod = "";

        if (isGoldenHour)
        {
            dayPeriod = "Golden Hour";
        }
        else if (hour == 12)
        {
            dayPeriod = "Noon";
        }
        else if (hour == 0)
        {
            dayPeriod = "Midnight";
        }
        else if (hour >= 5 && hour < 12)
        {
            dayPeriod = "Morning";
        }
        else if (hour >= 12 && hour < 17)
        {
            dayPeriod = "Afternoon";
        }
        else if (hour >= 17 && hour < 21)
        {
            dayPeriod = "Evening";
        }
        else if (hour >= 21 || hour < 5)
        {
            dayPeriod = "Night";
        }
        else
        {
            dayPeriod = "Unknown";
        }

        return useShorthand ? GetShorthand(dayPeriod) : dayPeriod;
    }

    private static string GetShorthand(string dayPeriod)
    {
        switch (dayPeriod)
        {
            case "Morning": return "Morn";
            case "Afternoon": return "Aftn";
            case "Evening": return "Evng";
            case "Night": return "Night";
            case "Noon": return "Noon";
            case "Midnight": return "Midnt";
            case "Golden Hour": return "GoldH";
            default: return dayPeriod;
        }
    }
}
