// filepath: f:\Replica\NAS\Files\repo\github\Sightseeingway\Sightseeingway\Services\Logger.cs
using Dalamud.Plugin.Services;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;

namespace Sightseeingway.Services
{
    /// <summary>
    /// Provides unified logging functionality for the application
    /// </summary>
    public class Logger
    {
        private readonly IPluginLog? _log;
        private readonly IChatGui? _chatGui;
        private bool _debugMode; // Removed readonly modifier to allow it to be changed

        public Logger(IPluginLog? log, IChatGui? chatGui, bool debugMode = false)
        {
            _log = log;
            _chatGui = chatGui;
            _debugMode = debugMode;
        }

        /// <summary>
        /// Logs a debug message to the Dalamud log
        /// </summary>
        public void Debug(string message, 
            [CallerFilePath] string filePath = "", 
            [CallerLineNumber] int lineNumber = 0, 
            [CallerMemberName] string caller = "")
        {
            string locationInfo = FormatLocationInfo(filePath, lineNumber, caller);
            _log?.Debug($"{locationInfo}{message}");
            
            // If debug mode is enabled, also print to chat
            if (_debugMode)
            {
                // IMPORTANT: Use direct chat printing instead of Client.PrintMessage to avoid circular dependency
                SafePrintToChat($"Debug: {message} {locationInfo}");
            }
        }

        /// <summary>
        /// Logs an informational message
        /// </summary>
        public void Information(string message, 
            [CallerFilePath] string filePath = "", 
            [CallerLineNumber] int lineNumber = 0, 
            [CallerMemberName] string caller = "")
        {
            string locationInfo = FormatLocationInfo(filePath, lineNumber, caller);
            _log?.Information($"{locationInfo}{message}");
            
            // If debug mode is enabled, also print to chat
            if (_debugMode)
            {
                SafePrintToChat($"Info: {message} {locationInfo}");
            }
        }

        /// <summary>
        /// Logs a warning message and optionally displays it in chat
        /// </summary>
        public void Warning(string message, bool showInChat = false, 
            [CallerFilePath] string filePath = "", 
            [CallerLineNumber] int lineNumber = 0, 
            [CallerMemberName] string caller = "")
        {
            string locationInfo = FormatLocationInfo(filePath, lineNumber, caller);
            _log?.Warning($"{locationInfo}{message}");
            
            // Always show in chat if debug mode is enabled, otherwise respect the showInChat parameter
            if ((_debugMode || showInChat) && _chatGui != null)
            {
                try
                {
                    _chatGui.PrintError($"{Constants.Plugin.ChatPrefix} Warning: {message} {locationInfo}");
                }
                catch
                {
                    // Silently fail if we can't print
                }
            }
        }

        /// <summary>
        /// Logs an error message and optionally displays it in chat
        /// </summary>
        public void Error(string message, Exception? ex = null, bool showInChat = true, 
            [CallerFilePath] string filePath = "", 
            [CallerLineNumber] int lineNumber = 0, 
            [CallerMemberName] string caller = "")
        {
            string locationInfo = FormatLocationInfo(filePath, lineNumber, caller);
            
            if (ex != null)
            {
                _log?.Error($"{locationInfo}{message}: {ex}");
                
                // In debug mode, include exception details in chat
                if (_debugMode)
                {
                    message = $"{message}: {ex.Message}";
                }
            }
            else
            {
                _log?.Error($"{locationInfo}{message}");
            }
            
            // Always show in chat if debug mode is enabled, otherwise respect the showInChat parameter
            if ((_debugMode || showInChat) && _chatGui != null)
            {
                try
                {
                    _chatGui.PrintError($"{Constants.Plugin.ChatPrefix} Error: {message} {locationInfo}");
                }
                catch
                {
                    // Silently fail if we can't print
                }
            }
        }

        /// <summary>
        /// Prints a message to the game chat for user feedback
        /// </summary>
        public void UserMessage(string message)
        {
            // IMPORTANT: Direct chat printing instead of using Client.PrintMessage
            SafePrintToChat(message);
        }

        /// <summary>
        /// Sets the debug mode which controls whether debug messages are shown in chat
        /// </summary>
        public void SetDebugMode(bool enabled)
        {
            _debugMode = enabled;
        }
        
        /// <summary>
        /// Formats location information from caller attributes
        /// </summary>
        private string FormatLocationInfo(string filePath, int lineNumber, string caller)
        {
            if (string.IsNullOrEmpty(filePath) && lineNumber == 0 && string.IsNullOrEmpty(caller))
                return string.Empty;
                
            string fileName = Path.GetFileName(filePath);
            return $"[{fileName}:{lineNumber} in {caller}] ";
        }
        
        /// <summary>
        /// Safe direct method to print to chat without using Client class
        /// to avoid circular dependencies
        /// </summary>
        private void SafePrintToChat(string message)
        {
            if (_chatGui == null) return;
            
            try
            {
                var pluginName = "Sightseeingway";
                var payloads = new System.Collections.Generic.List<Payload> { new TextPayload(message) };
                
                var seStringBuilder = new SeStringBuilder();
                seStringBuilder.AddUiForeground(548);
                seStringBuilder.AddText($"[{pluginName}] ");
                seStringBuilder.AddUiForegroundOff();
                seStringBuilder.AddText(message);
                
                _chatGui.Print(new XivChatEntry
                {
                    Message = seStringBuilder.BuiltString,
                    Type = XivChatType.Debug,
                });
            }
            catch
            {
                // Silently fail if we can't print
            }
        }
    }
}
