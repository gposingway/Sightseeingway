using System;

public static class DateTimeExtensions
{

    // Credit to Oli Trenouth: https://olitee.com/2015/01/c-convert-current-time-ffxivs-eorzea-time/
    // Source: https://github.com/codemonkey85/EorzeaTimes/blob/main/Eorzea.Shared/Extensions/EorzeaDateTimeExtensions.cs
    public static DateTime ToEorzeaTime(this DateTime date)
    {
        const double EORZEA_MULTIPLIER = 3600D / 175D;

        // Calculate how many ticks have elapsed since 1/1/1970
        var epochTicks = date.ToUniversalTime().Ticks - new DateTime(1970, 1, 1).Ticks;

        // Multiply those ticks by the Eorzea multipler (approx 20.5x)
        var eorzeaTicks = (long)Math.Round(epochTicks * EORZEA_MULTIPLIER);

        return new DateTime(eorzeaTicks);
    }

    public static string GetDayPeriod(this DateTime dateTime, bool useShorthand = false)
    {
        int hour = dateTime.Hour;
        string period = "";

        if (hour == 12)
        {
            period = "Noon";
        }
        else if (hour == 0)
        {
            period = "Midnight";
        }
        else if (hour >= 5 && hour < 12)
        {
            period = "Morning";
        }
        else if (hour >= 12 && hour < 17)
        {
            period = "Afternoon";
        }
        else if (hour >= 17 && hour < 21)
        {
            period = "Evening";
        }
        else if (hour >= 21 || hour < 5)
        {
            period = "Night";
        }
        else
        {
            period = "Unknown";
        }

        return useShorthand ? GetShorthand(period) : period;
    }

    public static string GetDayPeriodWithGoldenHour(this DateTime dateTime, bool useShorthand = false)
    {
        int hour = dateTime.Hour;
        bool isGoldenHour = (hour >= 17 && hour < 19) || (hour >= 6 && hour < 8); // Adjust Golden Hour times as needed
        string period = "";

        if (isGoldenHour)
        {
            period = "Golden Hour";
        }
        else if (hour == 12)
        {
            period = "Noon";
        }
        else if (hour == 0)
        {
            period = "Midnight";
        }
        else if (hour >= 5 && hour < 12)
        {
            period = "Morning";
        }
        else if (hour >= 12 && hour < 17)
        {
            period = "Afternoon";
        }
        else if (hour >= 17 && hour < 21)
        {
            period = "Evening";
        }
        else if (hour >= 21 || hour < 5)
        {
            period = "Night";
        }
        else
        {
            period = "Unknown";
        }

        return useShorthand ? GetShorthand(period) : period;
    }

    private static string GetShorthand(string period)
    {
        switch (period)
        {
            case "Morning": return "Morn";
            case "Afternoon": return "Aftn";
            case "Evening": return "Evng";
            case "Night": return "Night";
            case "Noon": return "Noon"; 
            case "Midnight": return "Midnt";
            case "Golden Hour": return "GoldH";
            default: return period;
        }
    }
}

public class Example
{
    public static void Main(string[] args)
    {
        DateTime now = DateTime.Now;

        string period = now.GetDayPeriod();
        Console.WriteLine($"Current period: {period}");

        string shorthandPeriod = now.GetDayPeriod(true); // Use shorthand
        Console.WriteLine($"Current period (shorthand): {shorthandPeriod}");

        DateTime goldenHourTime = new DateTime(2024, 10, 27, 18, 30, 0); // Example: 6:30 PM
        string goldenHourPeriod = goldenHourTime.GetDayPeriodWithGoldenHour();
        Console.WriteLine($"The period for {goldenHourTime} is: {goldenHourPeriod}");

        goldenHourPeriod = goldenHourTime.GetDayPeriodWithGoldenHour(true); // Shorthand
        Console.WriteLine($"The period for {goldenHourTime} is: {goldenHourPeriod}");

        DateTime noonTime = new DateTime(2024, 10, 27, 12, 0, 0);
        string noonPeriod = noonTime.GetDayPeriod();
        Console.WriteLine($"The period for {noonTime} is: {noonPeriod}");

        string noonShorthand = noonTime.GetDayPeriod(true);
        Console.WriteLine($"The period for {noonTime} is: {noonShorthand}");

        DateTime midnightTime = new DateTime(2024, 10, 27, 0, 0, 0);
        string midnightPeriod = midnightTime.GetDayPeriod();
        Console.WriteLine($"The period for {midnightTime} is: {midnightPeriod}");

        string midnightShorthand = midnightTime.GetDayPeriod(true);
        Console.WriteLine($"The period for {midnightTime} is: {midnightShorthand}");

    }
}
