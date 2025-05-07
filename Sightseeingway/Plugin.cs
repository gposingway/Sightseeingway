using Dalamud.IoC;
using Dalamud.Plugin;
using System.IO;
using Dalamud.Plugin.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Dalamud.Game.Config;
using Dalamud.Interface.Windowing;
using Dalamud.Game.Command;

namespace Sightseeingway
{
    public sealed class Plugin : IDalamudPlugin
    {
        // Static debug flag
        public static bool DebugMode = false;

        [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
        [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
        [PluginService] internal static IFramework Framework { get; private set; } = null!;
        [PluginService] internal static IClientState ClientState { get; private set; } = null!;
        [PluginService] internal static IGameConfig GameConfig { get; private set; } = null!;
        [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
        [PluginService] internal static IPluginLog Log { get; private set; } = null!;
        [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
        [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;

        private readonly List<FileSystemWatcher> fileWatchers = [];
        private readonly List<string> directoriesToMonitor = [];
        private readonly List<string> iniFilesToCheck = ["ReShade.ini", "GShade.ini"];
        private const string ShadingwayStateFileName = "shadingway.addon-state.json";

        // Configuration and UI
        public Configuration Config { get; private set; } = null!;
        private WindowSystem windowSystem = null!;
        private ConfigWindow configWindow = null!;

        // Hold the shadingway state
        public ShadingwayState? CurrentShadingwayState { get; private set; }

        public Plugin()
        {
            Log.Debug("Plugin constructor started.");
            SendMessage("Plugin Initializing...");

            // Load configuration
            Config = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            
            // Setup UI
            windowSystem = new WindowSystem("Sightseeingway");
            configWindow = new ConfigWindow(Config);
            windowSystem.AddWindow(configWindow);
            
            // Register UI events
            PluginInterface.UiBuilder.Draw += DrawUI;
            PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
            
            // Register commands
            CommandManager.AddHandler("/sightseeingway", new CommandInfo(OnCommand)
            {
                HelpMessage = "Opens the configuration window for Sightseeingway."
            });
            
            CommandManager.AddHandler("/sway", new CommandInfo(OnCommand)
            {
                HelpMessage = "Opens the configuration window for Sightseeingway."
            });

            Client.PrintMessage("Ready to help, friend!");

            InitializeDirectoriesToMonitor();
            IO.SetupWatchers(directoriesToMonitor, fileWatchers);
            SetupShadingwayWatcher();

            SetupConfigChangeWatcher();

            Log.Debug("Plugin constructor finished.");
            SendMessage("Plugin Initialized. Monitoring screenshot folders with filename caching.");
        }

        private void OnCommand(string command, string args)
        {
            // Toggle config window visibility
            configWindow.IsOpen = !configWindow.IsOpen;
        }
        
        private void DrawUI()
        {
            // Draw the configuration UI
            windowSystem.Draw();
        }
        
        private void DrawConfigUI()
        {
            // Open the configuration window
            configWindow.IsOpen = true;
        }

        private void SetupConfigChangeWatcher()
        {
            GameConfig.SystemChanged += (sender, args) =>
            {
                if (args.Option is SystemConfigOption option && option == SystemConfigOption.ScreenShotDir)
                {
                    Log.Debug("Screenshot directory setting changed, reinitializing directories to monitor.");
                    SendMessage("Debug: Screenshot folder changed, reinitializing directories to monitor.");
                    InitializeDirectoriesToMonitor();
                    IO.SetupWatchers(directoriesToMonitor, fileWatchers);
                }
            };
        }

        private void InitializeDirectoriesToMonitor()
        {
            Log.Debug("InitializeDirectoriesToMonitor started.");

            foreach (var watcher in fileWatchers) watcher.Dispose();
            fileWatchers.Clear();
            directoriesToMonitor.Clear();

            var defaultScreenshotFolder = Environment.GetDefaultScreenshotFolder();

            if (defaultScreenshotFolder != null)
            {
                directoriesToMonitor.Add(defaultScreenshotFolder);
                Log.Debug($"Default screenshot folder added to monitor list: {defaultScreenshotFolder}");
            }

            var gameBaseDir = Environment.GetGameDirectory();
            var dxgiPath = Path.Combine(gameBaseDir, "dxgi.dll");

            if (File.Exists(dxgiPath))
            {
                Log.Debug("dxgi.dll found, checking for INI files.");
                foreach (var iniFileName in iniFilesToCheck)
                {
                    CheckIniFileForScreenshotPath(gameBaseDir, iniFileName);
                }
            }
            else
            {
                Log.Debug("dxgi.dll not found in game folder, skipping INI file checks.");
                SendMessage("Debug: dxgi.dll not found, skipping INI file checks.");
            }

            Log.Debug("InitializeDirectoriesToMonitor finished.");
        }

        private void CheckIniFileForScreenshotPath(string gameBaseDir, string iniFileName)
        {
            var iniFilePath = Path.Combine(gameBaseDir, iniFileName);
            Log.Debug($"Checking for {iniFileName} at: {iniFilePath}");

            if (File.Exists(iniFilePath))
            {
                Log.Debug($"{iniFileName} found in game folder.");
                try
                {
                    var lines = File.ReadAllLines(iniFilePath);
                    string? savePath = null;
                    var inScreenshotGroup = false;

                    foreach (var line in lines)
                    {
                        var trimmedLine = line.Trim();
                        if (trimmedLine == "[SCREENSHOT]")
                        {
                            inScreenshotGroup = true;
                            Log.Debug($"Found [SCREENSHOT] group in {iniFileName}");
                        }
                        else if (inScreenshotGroup && trimmedLine.StartsWith("SavePath="))
                        {
                            savePath = trimmedLine.Substring("SavePath=".Length).Trim().Trim('"');
                            Log.Debug($"{iniFileName} SavePath found in ini: {savePath}");
                            break;
                        }
                        else if (trimmedLine.StartsWith("["))
                        {
                            inScreenshotGroup = false;
                        }
                    }

                    if (savePath != null)
                    {
                        var resolvedSavePath = savePath;
                        if (!Path.IsPathRooted(savePath))
                        {
                            resolvedSavePath = Path.GetFullPath(Path.Combine(gameBaseDir, savePath));
                            Log.Debug($"{iniFileName} SavePath is relative, resolving to absolute path: {resolvedSavePath}");
                        }

                        if (!directoriesToMonitor.Contains(resolvedSavePath) && Directory.Exists(resolvedSavePath))
                        {
                            directoriesToMonitor.Add(resolvedSavePath);
                            Log.Debug($"{iniFileName} SavePath folder added: {resolvedSavePath}");
                            SendMessage($"Debug: {iniFileName} SavePath folder added: {resolvedSavePath}");
                        }
                        else
                        {
                            Log.Debug($"{iniFileName} SavePath folder already monitored or does not exist: {resolvedSavePath}");
                        }
                    }
                    else
                    {
                        Log.Debug($"{iniFileName} SavePath not found in ini.");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"Error reading {iniFileName}: {ex}");
                    ChatGui.PrintError($"[Sightseeingway] Error reading {iniFileName}: {ex}");
                }
            }
            else
            {
                Log.Debug($"{iniFileName} not found in game folder: {iniFilePath}");
                SendMessage($"Debug: {iniFileName} not found in game folder: {iniFilePath}");
            }
        }

        private void SetupShadingwayWatcher()
        {
            var gameBaseDir = Environment.GetGameDirectory();
            var shadingwayStateFilePath = Path.Combine(gameBaseDir, ShadingwayStateFileName);

            if (File.Exists(shadingwayStateFilePath))
            {
                Log.Debug($"Found {ShadingwayStateFileName} at: {shadingwayStateFilePath}");

                // Load the initial state immediately, waiting for file release
                if (IO.WaitForFileReleaseGeneric(shadingwayStateFilePath))
                {
                    CurrentShadingwayState = IO.LoadShadingwayState(shadingwayStateFilePath);

                    if (CurrentShadingwayState?.Pid == Process.GetCurrentProcess().Id)
                    {
                        Client.PrintMessage("Ooh! Shadingway spotted! *waves excitedly*");
                    }
                }
                else
                {
                    Log.Warning($"Could not load initial Shadingway state due to file access issues.");
                    ChatGui.PrintError("[Sightseeingway] Warning: Could not read Shadingway state file on startup.");
                }


                var watcher = new FileSystemWatcher(gameBaseDir)
                {
                    Filter = ShadingwayStateFileName,
                    EnableRaisingEvents = true,
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName
                };
                watcher.Changed += OnShadingwayStateFileChanged; // Use Plugin's event handler
                watcher.Created += OnShadingwayStateFileChanged; // Use Plugin's event handler
                watcher.Renamed += OnShadingwayStateFileChanged; // Use Plugin's event handler
                fileWatchers.Add(watcher);
                Log.Information($"Monitoring {ShadingwayStateFileName} in: {gameBaseDir}");
                SendMessage($"Monitoring {ShadingwayStateFileName} in game folder.");
            }
            else
            {
                Log.Debug($"{ShadingwayStateFileName} not found in game folder: {shadingwayStateFilePath}");
                SendMessage($"Debug: {ShadingwayStateFileName} not found in game folder, monitoring disabled.");
            }
        }

        // Plugin's own event handler to update the state
        private void OnShadingwayStateFileChanged(object sender, FileSystemEventArgs e)
        {
            Log.Debug($"Shadingway State File changed: {e.FullPath}");
            SendMessage($"Debug: Shadingway State File changed: {e.FullPath}");
            // Wait for file release before loading state on file change
            if (IO.WaitForFileReleaseGeneric(e.FullPath))
            {
                CurrentShadingwayState = IO.LoadShadingwayState(e.FullPath); // Load state on file change
            }
            else
            {
                Log.Warning($"Could not reload Shadingway state due to file access issues.");
                ChatGui.PrintError("[Sightseeingway] Warning: Could not read Shadingway state file on file change.");
            }
        }

        public void Dispose()
        {
            Log.Debug("Dispose started.");
            SendMessage("Plugin Disposing...");

            // Dispose UI
            windowSystem.RemoveAllWindows();
            configWindow.Dispose();
            
            // Unregister commands
            CommandManager.RemoveHandler("/sightseeingway");
            CommandManager.RemoveHandler("/sway");
            
            // Unregister UI events
            PluginInterface.UiBuilder.Draw -= DrawUI;
            PluginInterface.UiBuilder.OpenConfigUi -= DrawConfigUI;

            foreach (var watcher in fileWatchers)
            {
                Log.Debug($"Disposing watcher for folder: {watcher.Path}");
                watcher.Created -= IO.OnFileCreated;
                watcher.Changed -= OnShadingwayStateFileChanged; // Use Plugin's event handler
                watcher.Dispose();
                Log.Debug($"Watcher for folder disposed: {watcher.Path}");
            }
            fileWatchers.Clear();
            Log.Debug("Watcher list cleared.");
            Log.Debug("Dispose finished.");
            SendMessage("Plugin Disposed.");
        }

        public static void SendMessage(string message)
        {
            if (DebugMode)
            {
                Client.PrintMessage(message);
            }
        }
    }
}
