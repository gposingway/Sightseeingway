using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sightseeingway
{
    public static class Client
    {
        // Reference: https://github.com/Infiziert90/PlayerTrack/blob/a1d4748abf0a33d71e156e0d380382b613b7a4f4/PlayerTrack.Plugin/Extensions/ChatGuiExtension.cs#L32


        public static void Print(string message)
        {
            var payloads = new List<Payload> { new TextPayload(message) };
            Print(payloads);
        }

        public static void Print(IEnumerable<Payload> payloads)
        {
            Plugin.ChatGui.Print(new XivChatEntry
            {
                Message = BuildSeString(Plugin.PluginInterface.InternalName, payloads),
                Type = XivChatType.Debug,
            });
        }

        private static SeString BuildSeString(string? pluginName, IEnumerable<Payload> payloads)
        {
            var builder = new SeStringBuilder();
            builder.AddUiForeground(548);
            builder.AddText($"[{pluginName}] ");
            builder.AddUiForegroundOff();
            builder.Append(payloads);

            return builder.BuiltString;
        }

        public static unsafe string GetCurrentWeather()
        {
            var weatherId = WeatherManager.Instance()->GetCurrentWeather();
            var weatherSheet = Plugin.DataManager.GetExcelSheet<Weather>();

            var weatherName = weatherSheet.GetRow(weatherId).Name.ExtractText();

            return weatherName;
        }

        public static unsafe DateTime GetCurrentEorzeaTime()
        {
            var eorzeaTime = DateTimeOffset.FromUnixTimeSeconds(FFXIVClientStructs.FFXIV.Client.System.Framework.Framework.Instance()->ClientTime.EorzeaTime);
            return eorzeaTime.DateTime;
        }
    }
}
