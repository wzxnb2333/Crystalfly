namespace Crystalfly.App.Updates;

internal static class EmbeddedUpdateSigningKey
{
    private const string ResourceName = "Crystalfly.App.Updates.stable-1.pub";

    public static byte[] Load()
    {
        using Stream stream = typeof(EmbeddedUpdateSigningKey).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidDataException("The embedded update signing key is missing.");
        using var reader = new StreamReader(stream);
        string encoded = reader.ReadToEnd().Trim();
        try
        {
            byte[] key = Convert.FromBase64String(encoded);
            return key.Length == 32
                ? key
                : throw new InvalidDataException("The embedded update signing key has an invalid length.");
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException("The embedded update signing key is invalid.", exception);
        }
    }
}
