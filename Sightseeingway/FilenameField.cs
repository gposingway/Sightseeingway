namespace Sightseeingway
{
    /// <summary>
    /// The fields that can be composed into a screenshot filename, in any order.
    /// Enum declaration order defines the default order for new installs (see
    /// <see cref="Configuration.GetDefaultSelectedFields"/>).
    /// </summary>
    public enum FilenameField
    {
        Timestamp,
        CharacterName,
        MapName,
        SubLocation,
        Position,
        EorzeaTime,
        Weather,
        ShaderPreset,
    }

    public enum TimestampFormat
    {
        Compact,    // yyyyMMddHHmmssfff
        Regular,    // yyyyMMdd-HHmmss-fff
        Readable,   // yyyy-MM-dd_HH-mm-ss.fff
    }
}
