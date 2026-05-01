using System;

public static class DateTimeExtensions
{
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
        return dayPeriod switch
        {
            "Morning" => "Morn",
            "Afternoon" => "Aftn",
            "Evening" => "Evng",
            "Night" => "Night",
            "Noon" => "Noon",
            "Midnight" => "Midnt",
            "Golden Hour" => "GoldH",
            _ => dayPeriod,
        };
    }
}
