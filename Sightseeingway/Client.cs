using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;

namespace Sightseeingway
{
    public static class Client
    {
        // Reference: https://github.com/Infiziert90/PlayerTrack/blob/a1d4748abf0a33d71e156e0d380382b613b7a4f4/PlayerTrack.Plugin/Extensions/ChatGuiExtension.cs#L32

        /// <summary>
        /// Print a message to the game chat.
        /// For logging purposes, use Plugin.Logger methods instead.
        /// This is primarily for backward compatibility.
        /// </summary>
        public static void PrintMessage(string message)
        {
            // SAFELY access the logger or fall back to direct ChatGui if Logger is not available
            if (Plugin.Logger != null)
            {
                Plugin.Logger?.UserMessage(message);
            }
            else if (Plugin.ChatGui != null)
            {
                // Fall back to direct chat print if logger isn't initialized yet
                PrintPayloads(new List<Payload> { new TextPayload(message) });
            }
        }

        /// <summary>
        /// Print payloads to game chat.
        /// This method is kept for specialized formatting needs.
        /// </summary>
        public static void PrintPayloads(IEnumerable<Payload> payloads)
        {
            if (Plugin.ChatGui != null)
            {
                try
                {
                    Plugin.ChatGui.Print(new XivChatEntry
                    {
                        Message = CreateSeString(Plugin.PluginInterface?.InternalName, payloads),
                        Type = XivChatType.Debug,
                    });
                }
                catch (Exception)
                {
                    // Silently fail if we can't print during initialization
                }
            }
        }

        private static SeString CreateSeString(string? pluginName, IEnumerable<Payload> payloads)
        {
            var seStringBuilder = new SeStringBuilder();
            seStringBuilder.AddUiForeground(548);
            seStringBuilder.AddText($"[{pluginName ?? "Sightseeingway"}] ");
            seStringBuilder.AddUiForegroundOff();
            seStringBuilder.Append(payloads);

            return seStringBuilder.BuiltString;
        }

        public static unsafe string GetCurrentWeatherName()
        {
            var weatherManager = WeatherManager.Instance();
            if (weatherManager == null)
            {
                return "Unknown Weather";
            }

            var currentWeatherId = weatherManager->GetCurrentWeather();
            var weatherSheet = Plugin.DataManager?.GetExcelSheet<Weather>();
            if (weatherSheet == null)
            {
                return "Unknown Weather";
            }

            // Fix for Weather struct vs null comparison
            var currentWeatherRow = weatherSheet.GetRow(currentWeatherId);
                        
            var currentWeatherName = currentWeatherRow.Name.ExtractText();
            return string.IsNullOrEmpty(currentWeatherName) ? "Unknown Weather" : currentWeatherName;
        }

        public static DateTime GetCurrentEorzeaDateTime()
        {
            // Use our own calculation since IFramework doesn't expose EorzeaDateTime directly
            try
            {
                if (Plugin.ClientState?.LocalPlayer == null)
                {
                    return DateTime.MinValue;
                }

                // Calculate Eorzea time manually based on Unix timestamp
                // 1 Eorzean day = 70 minutes real time (factor of 20.571...)
                const double EORZEA_MULTIPLIER = 20.571428571428573;
                var unixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var eorzeaTimestamp = unixTimestamp * EORZEA_MULTIPLIER;

                return DateTimeOffset.FromUnixTimeSeconds((long)eorzeaTimestamp).DateTime;
            }
            catch
            {
                return DateTime.MinValue;
            }
        }
    }
}
