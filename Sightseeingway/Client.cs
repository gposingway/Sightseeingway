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

        public static void PrintMessage(string message)
        {
            var payloads = new List<Payload> { new TextPayload(message) };
            PrintPayloads(payloads);
        }

        public static void PrintPayloads(IEnumerable<Payload> payloads)
        {
            Plugin.ChatGui.Print(new XivChatEntry
            {
                Message = CreateSeString(Plugin.PluginInterface.InternalName, payloads),
                Type = XivChatType.Debug,
            });
        }

        private static SeString CreateSeString(string? pluginName, IEnumerable<Payload> payloads)
        {
            var seStringBuilder = new SeStringBuilder();
            seStringBuilder.AddUiForeground(548);
            seStringBuilder.AddText($"[{pluginName}] ");
            seStringBuilder.AddUiForegroundOff();
            seStringBuilder.Append(payloads);

            return seStringBuilder.BuiltString;
        }

        public static unsafe string GetCurrentWeatherName()
        {
            var currentWeatherId = WeatherManager.Instance()->GetCurrentWeather();
            var weatherSheet = Plugin.DataManager.GetExcelSheet<Weather>();

            var currentWeatherName = weatherSheet.GetRow(currentWeatherId).Name.ExtractText();

            return currentWeatherName;
        }

        public static unsafe DateTime GetCurrentEorzeaDateTime()
        {
            var eorzeaDateTime = DateTimeOffset.FromUnixTimeSeconds(FFXIVClientStructs.FFXIV.Client.System.Framework.Framework.Instance()->ClientTime.EorzeaTime);
            return eorzeaDateTime.DateTime;
        }
    }
}
