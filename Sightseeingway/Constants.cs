using System.Numerics;

namespace Sightseeingway
{
    /// <summary>
    /// Centralizes constant values used throughout the application
    /// </summary>
    public static class Constants
    {
        // Plugin Information
        public static class Plugin
        {
            public const string Name = "Sightseeingway";
            public const string Command = "/sightseeingway";
            public const string ShortCommand = "/sway";
            public const string HelpMessage = "Opens the configuration window for Sightseeingway.";
            public const string ChatPrefix = "[Sightseeingway]";
        }
        
        // File Operations
        public static class FileOperations
        {
            public const int MaxFileTries = 30;
            public const int MaxMoveTries = 10;
            public const int FileReleaseWaitTimeMs = 500;
            public const int MoveRetryWaitTimeMs = 100;
            public const int RenameQueueDelayMs = 3000;
            
            public static readonly string[] SupportedImageExtensions = { ".jpg", ".jpeg", ".png" };
            public static readonly string[] ShaderIniFiles = { "ReShade.ini", "GShade.ini" };
            public const string DxgiFileName = "dxgi.dll";
            public const string ShadingwayStateFileName = "shadingway.addon-state.json";
            
            // INI file sections and keys
            public const string ScreenshotSection = "[SCREENSHOT]";
            public const string SavePathKey = "SavePath=";
        }
        
        // Timestamp Formats
        public static class Formats
        {
            public const string CompactTimestamp = "yyyyMMddHHmmssfff";
            public const string RegularTimestamp = "yyyyMMdd-HHmmss-fff";
            public const string ReadableTimestamp = "yyyy-MM-dd_HH-mm-ss.fff";
        }
        
        // UI Settings
        public static class UI
        {
            public static readonly Vector2 DefaultWindowSize = new(880, 660);

            public static readonly Vector4 SaveDirtyTint = new(0.85f, 0.55f, 0.10f, 1.0f);
            public static readonly Vector4 SaveDirtyHover = new(0.95f, 0.65f, 0.20f, 1.0f);
            public static readonly Vector4 HeaderColor = new(0.7f, 0.9f, 1.0f, 1.0f);
            public static readonly Vector4 SectionAccentColor = new(0.0f, 0.85f, 1.0f, 1.0f);
            public static readonly Vector4 FieldHeaderColor = new(1.0f, 0.85f, 0.0f, 1.0f);
            public static readonly Vector4 InfoColor = new(0.8f, 0.8f, 0.5f, 1.0f);
            public static readonly Vector4 ExampleHeaderColor = new(0.5f, 0.9f, 0.5f, 1.0f);
            public static readonly Vector4 ExampleColor = new(1.0f, 1.0f, 1.0f, 1.0f);
            public static readonly Vector4 MandatoryFieldBg = new(0.3f, 0.3f, 0.5f, 0.5f);
            public static readonly Vector4 LinkColor = new(0.6f, 0.8f, 1.0f, 1.0f);

            public const float ButtonRowReservedHeight = 182f;
            public const float ExampleBoxHeight = 60f;
            public const float ButtonHeight = 24f;
            public const float ButtonRowMargin = 40f;
            public const float ButtonGap = 10f;
            public const float DebugCheckboxOffsetRatio = 0.7f;

            public const string ShadingwayRepoUrl = "https://github.com/gposingway/shadingway";
        }
    }
}