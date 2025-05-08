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
using Sightseeingway.Services;
using Sightseeingway.Results;

namespace Sightseeingway
{
    public sealed class Plugin : IDalamudPlugin
    {
        // Static debug flag - will be initialized from config
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

        // The unified logger service
        public static Logger? Logger { get; private set; } = null;

        private readonly List<FileSystemWatcher> fileWatchers = [];
        private readonly List<string> directoriesToMonitor = [];

        // Configuration and UI
        public Configuration Config { get; private set; } = null!;
        private WindowSystem windowSystem = null!;
        private ConfigWindow? configWindow = null;

        // Hold the shadingway state
        public ShadingwayState? CurrentShadingwayState { get; private set; }

        public Plugin()
        {
            try
            {
                // IMPORTANT: Initialize all dependencies BEFORE creating the Logger
                Log?.Debug("Plugin constructor started.");
                
                // Load configuration first
                Config = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
                
                // Set debug mode from configuration
                DebugMode = Config.DebugMode;
                
                // Setup UI system first, before creating any windows
                windowSystem = new WindowSystem(Constants.Plugin.Name);
                
                // NOW initialize the Logger after basic services are ready
                Logger = new Logger(Log, ChatGui, DebugMode);
                
                // After Logger is ready, we can use it safely
                Logger.Debug("Logger initialized, continuing with plugin setup.");
                
                // Now create the config window (which uses Logger)
                configWindow = new ConfigWindow(Config);
                windowSystem.AddWindow(configWindow);
                
                // Register UI events
                PluginInterface.UiBuilder.Draw += DrawUI;
                PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
                
                // Register commands
                CommandManager.AddHandler(Constants.Plugin.Command, new CommandInfo(OnCommand)
                {
                    HelpMessage = Constants.Plugin.HelpMessage
                });
                
                CommandManager.AddHandler(Constants.Plugin.ShortCommand, new CommandInfo(OnCommand)
                {
                    HelpMessage = Constants.Plugin.HelpMessage
                });

                // Now it's safe to use PrintMessage
                SafeUserMessage("Ready to help, friend!");

                InitializeDirectoriesToMonitor();
                IO.SetupWatchers(directoriesToMonitor, fileWatchers);
                SetupShadingwayWatcher();

                SetupConfigChangeWatcher();

                Logger.Debug("Plugin constructor finished.");
            }
            catch (Exception ex)
            {
                // Last resort error handling
                Log?.Error($"Critical error during plugin initialization: {ex}");
                if (ChatGui != null)
                {
                    try
                    {
                        ChatGui.PrintError($"[Sightseeingway] Critical error during initialization: {ex.Message}");
                    }
                    catch
                    {
                        // Nothing we can do if even this fails
                    }
                }
            }
        }

        // Safe method to show messages to users even during initialization
        private void SafeUserMessage(string message)
        {
            try
            {
                if (Logger != null)
                {
                    Logger.UserMessage(message);
                }
                else if (ChatGui != null)
                {
                    ChatGui.Print($"[Sightseeingway] {message}");
                }
            }
            catch
            {
                // Silently fail if we can't log during startup
            }
        }

        private void OnCommand(string command, string args)
        {
            // Handle args for debug mode if specified
            if (!string.IsNullOrWhiteSpace(args))
            {
                var argsParts = args.Trim().Split(' ');
                if (argsParts.Length > 0)
                {
                    switch (argsParts[0].ToLower())
                    {
                        case "debug":
                            // Toggle debug mode directly from command
                            Config.DebugMode = !Config.DebugMode;
                            DebugMode = Config.DebugMode;
                            if (Logger != null)
                            {
                                Logger.SetDebugMode(DebugMode);
                                Logger.UserMessage($"Debug mode {(DebugMode ? "enabled" : "disabled")}");
                            }
                            Config.Save();
                            return;
                    }
                }
            }
            
            // Default behavior: toggle config window visibility
            configWindow!.IsOpen = !configWindow.IsOpen;
        }
        
        private void DrawUI()
        {
            // Draw the configuration UI
            windowSystem.Draw();
        }
        
        private void DrawConfigUI()
        {
            // Open the configuration window
            configWindow!.IsOpen = true;
        }

        private void SetupConfigChangeWatcher()
        {
            GameConfig.SystemChanged += (sender, args) =>
            {
                if (args.Option is SystemConfigOption option && option == SystemConfigOption.ScreenShotDir)
                {
                    Logger?.Debug("Screenshot directory setting changed, reinitializing directories to monitor.");
                    InitializeDirectoriesToMonitor();
                    IO.SetupWatchers(directoriesToMonitor, fileWatchers);
                }
            };
        }

        private void InitializeDirectoriesToMonitor()
        {
            Logger?.Debug("InitializeDirectoriesToMonitor started.");

            foreach (var watcher in fileWatchers) watcher.Dispose();
            fileWatchers.Clear();
            directoriesToMonitor.Clear();

            var defaultScreenshotFolder = Environment.GetDefaultScreenshotFolder();

            if (defaultScreenshotFolder != null)
            {
                directoriesToMonitor.Add(defaultScreenshotFolder);
                Logger?.Debug($"Default screenshot folder added to monitor list: {defaultScreenshotFolder}");
            }

            var gameBaseDir = Environment.GetGameDirectory();
            var dxgiPath = Path.Combine(gameBaseDir, Constants.FileOperations.DxgiFileName);

            if (File.Exists(dxgiPath))
            {
                Logger?.Debug("dxgi.dll found, checking for INI files.");
                foreach (var iniFileName in Constants.FileOperations.ShaderIniFiles)
                {
                    CheckIniFileForScreenshotPath(gameBaseDir, iniFileName);
                }
            }
            else
            {
                Logger?.Debug("dxgi.dll not found in game folder, skipping INI file checks.");
            }

            Logger?.Debug("InitializeDirectoriesToMonitor finished.");
        }

        private void CheckIniFileForScreenshotPath(string gameBaseDir, string iniFileName)
        {
            var iniFilePath = Path.Combine(gameBaseDir, iniFileName);
            Logger?.Debug($"Checking for {iniFileName} at: {iniFilePath}");

            if (File.Exists(iniFilePath))
            {
                Logger?.Debug($"{iniFileName} found in game folder.");
                try
                {
                    var lines = File.ReadAllLines(iniFilePath);
                    string? savePath = null;
                    var inScreenshotGroup = false;

                    foreach (var line in lines)
                    {
                        var trimmedLine = line.Trim();
                        if (trimmedLine == Constants.FileOperations.ScreenshotSection)
                        {
                            inScreenshotGroup = true;
                            Logger?.Debug($"Found [SCREENSHOT] group in {iniFileName}");
                        }
                        else if (inScreenshotGroup && trimmedLine.StartsWith(Constants.FileOperations.SavePathKey))
                        {
                            savePath = trimmedLine.Substring(Constants.FileOperations.SavePathKey.Length).Trim().Trim('"');
                            Logger?.Debug($"{iniFileName} SavePath found in ini: {savePath}");
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
                            Logger?.Debug($"{iniFileName} SavePath is relative, resolving to absolute path: {resolvedSavePath}");
                        }

                        if (!directoriesToMonitor.Contains(resolvedSavePath) && Directory.Exists(resolvedSavePath))
                        {
                            directoriesToMonitor.Add(resolvedSavePath);
                            Logger?.Debug($"{iniFileName} SavePath folder added: {resolvedSavePath}");
                        }
                        else
                        {
                            Logger?.Debug($"{iniFileName} SavePath folder already monitored or does not exist: {resolvedSavePath}");
                        }
                    }
                    else
                    {
                        Logger?.Debug($"{iniFileName} SavePath not found in ini.");
                    }
                }
                catch (Exception ex)
                {
                    Logger?.Error($"Error reading {iniFileName}", ex, true);
                }
            }
            else
            {
                Logger?.Debug($"{iniFileName} not found in game folder: {iniFilePath}");
            }
        }

        private void SetupShadingwayWatcher()
        {
            var gameBaseDir = Environment.GetGameDirectory();
            var shadingwayStateFilePath = Path.Combine(gameBaseDir, Constants.FileOperations.ShadingwayStateFileName);

            if (File.Exists(shadingwayStateFilePath))
            {
                Logger?.Debug($"Found {Constants.FileOperations.ShadingwayStateFileName} at: {shadingwayStateFilePath}");

                // Load the initial state immediately, waiting for file release
                var waitResult = IO.WaitForFileReleaseGeneric(shadingwayStateFilePath);
                if (waitResult.IsSuccess)
                {
                    var loadResult = IO.LoadShadingwayState(shadingwayStateFilePath);
                    if (loadResult.IsSuccess)
                    {
                        CurrentShadingwayState = loadResult.Data;

                        if (CurrentShadingwayState?.Pid == Process.GetCurrentProcess().Id)
                        {
                            SafeUserMessage("Ooh! Shadingway spotted! *waves excitedly*");
                        }
                    }
                    else
                    {
                        Logger?.Warning(loadResult.ErrorMessage ?? "Unknown error loading Shadingway state");
                    }
                }
                else
                {
                    Logger?.Warning("Could not load initial Shadingway state due to file access issues.");
                }

                var watcher = new FileSystemWatcher(gameBaseDir)
                {
                    Filter = Constants.FileOperations.ShadingwayStateFileName,
                    EnableRaisingEvents = true,
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName
                };
                watcher.Changed += OnShadingwayStateFileChanged;
                watcher.Created += OnShadingwayStateFileChanged;
                watcher.Renamed += OnShadingwayStateFileChanged;
                fileWatchers.Add(watcher);
                Logger?.Information($"Monitoring {Constants.FileOperations.ShadingwayStateFileName} in: {gameBaseDir}");
            }
            else
            {
                Logger?.Debug($"{Constants.FileOperations.ShadingwayStateFileName} not found in game folder: {shadingwayStateFilePath}");
            }
        }

        // Plugin's own event handler to update the state
        private void OnShadingwayStateFileChanged(object sender, FileSystemEventArgs e)
        {
            Logger?.Debug($"Shadingway State File changed: {e.FullPath}");
            
            // Wait for file release before loading state on file change
            var waitResult = IO.WaitForFileReleaseGeneric(e.FullPath);
            if (waitResult.IsSuccess)
            {
                var loadResult = IO.LoadShadingwayState(e.FullPath);
                if (loadResult.IsSuccess)
                {
                    CurrentShadingwayState = loadResult.Data;
                }
                else
                {
                    // Logger?.Warning(loadResult.ErrorMessage ?? "Unknown error reloading Shadingway state");
                }
            }
            else
            {
                Logger?.Warning("Could not reload Shadingway state due to file access issues.");
            }
        }

        public void Dispose()
        {
            Logger?.Debug("Dispose started.");

            // Dispose UI
            windowSystem.RemoveAllWindows();
            configWindow?.Dispose();
            
            // Unregister commands
            CommandManager.RemoveHandler(Constants.Plugin.Command);
            CommandManager.RemoveHandler(Constants.Plugin.ShortCommand);
            
            // Unregister UI events
            PluginInterface.UiBuilder.Draw -= DrawUI;
            PluginInterface.UiBuilder.OpenConfigUi -= DrawConfigUI;

            foreach (var watcher in fileWatchers)
            {
                Logger?.Debug($"Disposing watcher for folder: {watcher.Path}");
                watcher.Created -= IO.OnFileCreated;
                watcher.Changed -= OnShadingwayStateFileChanged;
                watcher.Dispose();
                Logger?.Debug($"Watcher for folder disposed: {watcher.Path}");
            }
            fileWatchers.Clear();
            Logger?.Debug("Watcher list cleared.");
            Logger?.Debug("Dispose finished.");
            SafeUserMessage("Plugin Disposed.");
        }

        // Update the static SendMessage method to use our Logger instead
        public static void SendMessage(string message)
        {
            if (DebugMode && Logger != null)
            {
                Logger.UserMessage(message);
            }
        }
    }
}
