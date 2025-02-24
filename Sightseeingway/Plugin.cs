using Dalamud.IoC;
using Dalamud.Plugin;
using System.IO;
using Dalamud.Plugin.Services;
using System;
using System.Collections.Generic;

namespace Sightseeingway
{

    public sealed class Plugin : IDalamudPlugin
    {
        [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
        [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
        [PluginService] internal static IFramework Framework { get; private set; } = null!;
        [PluginService] internal static IClientState ClientState { get; private set; } = null!;
        [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
        [PluginService] internal static IPluginLog Log { get; private set; } = null!;
        [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;

        private readonly List<FileSystemWatcher> watchers = [];
        private readonly List<string> foldersToMonitor = [];
        private readonly List<string> iniFilesToCheck = ["ReShade.ini", "GShade.ini"];


        // Static debug flag
        public static bool DEBUG = false;

        public Plugin()
        {
            Log.Debug("Plugin constructor started.");
            PrintChatMessage("Plugin Initializing...");

            ChatGui.Print($"[Sightseeingway] Ready to help, friend!");

            InitializeFoldersToMonitor();
            InitializeWatchers();

            Log.Debug("Plugin constructor finished.");
            PrintChatMessage("Plugin Initialized. Monitoring screenshot folders with filename caching.");
        }


        private void InitializeFoldersToMonitor()
        {
            Log.Debug("InitializeFoldersToMonitor started.");
            var defaultScreenshotFolder = GetDefaultScreenshotFolder();

            if (defaultScreenshotFolder != null)
            {
                foldersToMonitor.Add(defaultScreenshotFolder);
                Log.Debug($"Default screenshot folder added to monitor list: {defaultScreenshotFolder}");
            }

            var gameBaseDir = GetGameDirectory();

            var dxgiPath = Path.Combine(gameBaseDir, "dxgi.dll");

            if (File.Exists(dxgiPath))
            {
                Log.Debug($"dxgi.dll found, checking for INI files.");
                foreach (var iniFileName in iniFilesToCheck)
                {
                    ProcessIniFile(gameBaseDir, iniFileName);
                }
            }
            else
            {
                Log.Debug($"dxgi.dll not found in game folder, skipping INI file checks.");
                PrintChatMessage($"Debug: dxgi.dll not found, skipping INI file checks.");
            }

            Log.Debug("InitializeFoldersToMonitor finished.");
        }

        private void ProcessIniFile(string gameBaseDir, string iniFileName)
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

                        if (!foldersToMonitor.Contains(resolvedSavePath) && System.IO.Directory.Exists(resolvedSavePath))
                        {
                            foldersToMonitor.Add(resolvedSavePath);
                            Log.Debug($"{iniFileName} SavePath folder added: {resolvedSavePath}");
                            PrintChatMessage($"Debug: {iniFileName} SavePath folder added: {resolvedSavePath}");
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
                PrintChatMessage($"Debug: {iniFileName} not found in game folder: {iniFilePath}");
            }
        }
        private void InitializeWatchers()
        {
            Log.Debug("InitializeWatchers started.");
            foreach (string folder in foldersToMonitor)
            {
                if (System.IO.Directory.Exists(folder))
                {
                    Log.Debug($"Creating watcher for folder: {folder}");
                    var watcher = new FileSystemWatcher(folder);
                    watcher.Created += IO.OnFileCreated;
                    watcher.EnableRaisingEvents = true;
                    watchers.Add(watcher);
                    Log.Information($"Monitoring folder: {folder}");
                    PrintChatMessage($"Monitoring folder: {folder}");
                }
                else
                {
                    Log.Error($"Folder not found: {folder}");
                    ChatGui.PrintError($"[Sightseeingway] Error: Folder not found: {folder}");
                }
            }
            Log.Debug("InitializeWatchers finished.");
        }

        public void Dispose()
        {
            Log.Debug("Dispose started.");
            PrintChatMessage("Plugin Disposing...");

            foreach (var watcher in watchers)
            {
                Log.Debug($"Disposing watcher for folder: {watcher.Path}");
                watcher.Created -= IO.OnFileCreated;
                watcher.Dispose();
                Log.Debug($"Watcher for folder disposed: {watcher.Path}");
            }
            watchers.Clear();
            Log.Debug("Watcher list cleared.");
            Log.Debug("Dispose finished.");
            PrintChatMessage("Plugin Disposed.");
        }

        public static void PrintChatMessage(string message)
        {
            if (DEBUG)
            {
                ChatGui.Print($"[Sightseeingway]: {message}");
            }
        }
    }
}
