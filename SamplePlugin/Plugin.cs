using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using System.IO;
using Dalamud.Plugin.Services;
using SamplePlugin;
using System;
using System.Numerics;

namespace Sightseeingway
{
    public sealed class Sightseeingway : IDalamudPlugin
    {
        [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
        [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
        [PluginService] internal static IFramework Framework { get; private set; } = null!;
        [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
        [PluginService] internal static IClientState ClientState { get; private set; } = null!;
        [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
        [PluginService] internal static IPluginLog Log { get; private set; } = null!;

        private const string CommandName = "/sightseeingway";

        public Configuration Configuration { get; init; }

        public Sightseeingway()
        {
            Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            

            Framework.OnScreenshotTaken += OnScreenshotTaken;

            CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "A useful message to display in /xlhelp"
            });
        }

        public void Dispose()
        {
            PluginInterface.Framework.OnScreenshotTaken -= OnScreenshotTaken;
            CommandManager.RemoveHandler(CommandName);
        }

        private void OnCommand(string command, string args)
        {
            // Implement your command logic here
            // For example, you could toggle the visibility of your main window
        }

        private void OnScreenshotTaken(XivScreenshot screenshot)
        {
            // Get necessary information from the screenshot
            string characterName = ClientState.LocalPlayer?.Name.TextValue;
            string mapName = ClientState.TerritoryType.Name;
            Vector3 position = ClientState.LocalPlayer.Position;

            // Format the filename
            string timestamp = DateTime.Now.ToString("yyyyMMddHHmmssfff");
            string filename = $"{timestamp}_{characterName}_{mapName}_X{position.X:0.00}_Y{position.Y:0.00}_Z{position.Z:0.00}.png";

            // Construct the new file path
            string newFilePath = Path.Combine(screenshot.DirectoryPath, filename);
        }
    }
}
