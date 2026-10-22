// Metadata fixture: the reader must never load or execute game code.
internal static class Constants
{
    public const string GAME_VERSION = "1.0.28324";

    static Constants() => throw new InvalidOperationException("Game code must not execute.");
}
