using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using System.Collections.Generic;

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
    }
}
