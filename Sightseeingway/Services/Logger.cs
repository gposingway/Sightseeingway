using Dalamud.Plugin.Services;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;

namespace Sightseeingway.Services
{
    /// <summary>
    /// Provides unified logging functionality for the application.
    ///
    /// Every level method accepts an optional <see cref="Guid"/> correlation
    /// ID; when present it is rendered into the message prefix as
    /// <c>id=...</c> for cross-thread, cross-file traceability of a single
    /// screenshot's pipeline lifecycle.
    /// </summary>
    public class Logger
    {
        private readonly IPluginLog? _log;
        private readonly IChatGui? _chatGui;
        private LogVerbosity _verbosity;

        public Logger(IPluginLog? log, IChatGui? chatGui, LogVerbosity verbosity = LogVerbosity.Status)
        {
            _log = log;
            _chatGui = chatGui;
            _verbosity = verbosity;
        }

        public LogVerbosity Verbosity => _verbosity;

        public void SetVerbosity(LogVerbosity verbosity) => _verbosity = verbosity;

        public void Debug(string message,
            Guid? correlationId = null,
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0,
            [CallerMemberName] string caller = "")
        {
            var prefix = FormatPrefix(filePath, lineNumber, caller, correlationId);
            _log?.Debug($"{prefix}{message}");

            if (_verbosity == LogVerbosity.Debug)
                SafePrintToChat($"Debug: {message} {prefix}");
        }

        public void Information(string message,
            Guid? correlationId = null,
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0,
            [CallerMemberName] string caller = "")
        {
            var prefix = FormatPrefix(filePath, lineNumber, caller, correlationId);
            _log?.Information($"{prefix}{message}");

            if (_verbosity == LogVerbosity.Debug)
                SafePrintToChat($"Info: {message} {prefix}");
        }

        public void Warning(string message,
            bool showInChat = false,
            Guid? correlationId = null,
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0,
            [CallerMemberName] string caller = "")
        {
            var prefix = FormatPrefix(filePath, lineNumber, caller, correlationId);
            _log?.Warning($"{prefix}{message}");

            // Status: respect showInChat. Debug: always. Quiet: log only.
            var shouldShow = _verbosity switch
            {
                LogVerbosity.Debug => true,
                LogVerbosity.Status => showInChat,
                _ => false,
            };

            if (shouldShow && _chatGui != null)
            {
                try
                {
                    _chatGui.PrintError($"{Constants.Plugin.ChatPrefix} Warning: {message} {prefix}");
                }
                catch { /* never throw from logging */ }
            }
        }

        public void Error(string message,
            Exception? ex = null,
            bool showInChat = true,
            Guid? correlationId = null,
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0,
            [CallerMemberName] string caller = "")
        {
            var prefix = FormatPrefix(filePath, lineNumber, caller, correlationId);

            if (ex != null)
            {
                _log?.Error($"{prefix}{message}: {ex}");
                if (_verbosity == LogVerbosity.Debug)
                    message = $"{message}: {ex.Message}";
            }
            else
            {
                _log?.Error($"{prefix}{message}");
            }

            // Errors always show unless verbosity is Quiet AND caller didn't ask.
            var shouldShow = _verbosity != LogVerbosity.Quiet || showInChat;

            if (shouldShow && _chatGui != null)
            {
                try
                {
                    _chatGui.PrintError($"{Constants.Plugin.ChatPrefix} Error: {message} {prefix}");
                }
                catch { /* never throw from logging */ }
            }
        }

        /// <summary>
        /// User-facing chat message. Always shown unless verbosity is Quiet.
        /// </summary>
        public void UserMessage(string message)
        {
            if (_verbosity == LogVerbosity.Quiet) return;
            SafePrintToChat(message);
        }

        private static string FormatPrefix(string filePath, int lineNumber, string caller, Guid? id)
        {
            if (string.IsNullOrEmpty(filePath) && lineNumber == 0 && string.IsNullOrEmpty(caller) && id == null)
                return string.Empty;

            var fileName = string.IsNullOrEmpty(filePath) ? string.Empty : Path.GetFileName(filePath);
            var location = string.IsNullOrEmpty(fileName)
                ? string.Empty
                : $"[{fileName}:{lineNumber} in {caller}] ";

            return id.HasValue ? $"[id={id.Value:D}] {location}" : location;
        }

        private void SafePrintToChat(string message)
        {
            if (_chatGui == null) return;

            try
            {
                var seStringBuilder = new SeStringBuilder();
                seStringBuilder.AddUiForeground(548);
                seStringBuilder.AddText($"{Constants.Plugin.ChatPrefix} ");
                seStringBuilder.AddUiForegroundOff();
                seStringBuilder.AddText(message);

                _chatGui.Print(new XivChatEntry
                {
                    Message = seStringBuilder.BuiltString,
                    Type = XivChatType.Debug,
                });
            }
            catch { /* never throw from logging */ }
        }
    }
}
