using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using Crystalfly.Core.Serialization;
using NSec.Cryptography;

namespace Crystalfly.Core.Updates;

public static class UpdateManifestVerifier
{
    public const int MaxEnvelopeBytes = 2 * 1024 * 1024;
    public const int MaxPayloadBytes = 1024 * 1024;
    private const string StableKeyId = "stable-1";
    private static readonly JsonSerializerOptions StrictJson = CreateStrictJson();

    public static UpdateManifest Verify(string document, ReadOnlySpan<byte> publicKeyBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(document);
        if (System.Text.Encoding.UTF8.GetByteCount(document) > MaxEnvelopeBytes)
        {
            throw new InvalidDataException("The update manifest envelope is too large.");
        }

        SignedUpdateManifest envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<SignedUpdateManifest>(document, StrictJson)
                ?? throw new InvalidDataException("The update manifest envelope is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The update manifest envelope is invalid.", exception);
        }
        if (envelope.SchemaVersion != SignedUpdateManifest.CurrentSchemaVersion
            || !string.Equals(envelope.KeyId, StableKeyId, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(envelope.Payload)
            || string.IsNullOrWhiteSpace(envelope.Signature))
        {
            throw new InvalidDataException("The update manifest signing key or schema is unsupported.");
        }

        byte[] payload;
        byte[] signature;
        try
        {
            payload = Convert.FromBase64String(envelope.Payload);
            signature = Convert.FromBase64String(envelope.Signature);
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException("The update manifest signature encoding is invalid.", exception);
        }
        if (payload.Length is <= 0 or > MaxPayloadBytes
            || signature.Length != SignatureAlgorithm.Ed25519.SignatureSize
            || publicKeyBytes.Length != SignatureAlgorithm.Ed25519.PublicKeySize)
        {
            throw new InvalidDataException("The update manifest signature sizes are invalid.");
        }

        PublicKey publicKey;
        try
        {
            publicKey = PublicKey.Import(
                SignatureAlgorithm.Ed25519,
                publicKeyBytes,
                KeyBlobFormat.RawPublicKey);
        }
        catch (Exception exception) when (exception is ArgumentException or CryptographicException)
        {
            throw new InvalidDataException("The embedded update signing key is invalid.", exception);
        }
        if (!SignatureAlgorithm.Ed25519.Verify(publicKey, payload, signature))
        {
            throw new InvalidDataException("The update manifest signature is invalid.");
        }

        UpdateManifest manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<UpdateManifest>(payload, StrictJson)
                ?? throw new InvalidDataException("The signed update payload is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The signed update payload is invalid.", exception);
        }
        Validate(manifest);
        return manifest;
    }

    private static void Validate(UpdateManifest manifest)
    {
        if (manifest.SchemaVersion != UpdateManifest.CurrentSchemaVersion
            || !string.Equals(manifest.Channel, "stable", StringComparison.Ordinal)
            || !Version.TryParse(manifest.Version, out var version)
            || version.Major < 0
            || version.ToString(3) != manifest.Version
            || manifest.PublishedAt == default
            || manifest.NotesMarkdown is null
            || manifest.NotesMarkdown.Length > 64 * 1024
            || manifest.Assets is null
            || manifest.Assets.Count != 2)
        {
            throw new InvalidDataException("The signed update payload metadata is invalid.");
        }

        var kinds = new HashSet<UpdateAssetKind>();
        foreach (var asset in manifest.Assets)
        {
            if (asset is null
                || !Enum.IsDefined(asset.Kind)
                || !kinds.Add(asset.Kind)
                || !string.Equals(asset.Runtime, "win-x64", StringComparison.Ordinal)
                || asset.Size is <= 0 or > int.MaxValue
                || string.IsNullOrWhiteSpace(asset.Sha256)
                || asset.Sha256.Length != 64
                || !IsSha256(asset.Sha256)
                || !Uri.TryCreate(asset.Url, UriKind.Absolute, out var uri)
                || uri.Scheme != Uri.UriSchemeHttps
                || !string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase)
                || !uri.AbsolutePath.StartsWith(
                    "/wzxnb2333/Crystalfly/releases/download/",
                    StringComparison.OrdinalIgnoreCase)
                || !string.IsNullOrEmpty(uri.UserInfo)
                || !string.IsNullOrEmpty(uri.Fragment))
            {
                throw new InvalidDataException("The signed update payload contains an invalid asset.");
            }
        }
        if (!kinds.SetEquals([UpdateAssetKind.Installer, UpdateAssetKind.Portable]))
        {
            throw new InvalidDataException("The signed update payload is missing a required asset.");
        }
    }

    private static bool IsSha256(string value)
    {
        try
        {
            return Convert.FromHexString(value).Length == 32;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static JsonSerializerOptions CreateStrictJson()
    {
        var options = new JsonSerializerOptions(CrystalflyJson.Options)
        {
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };
        return options;
    }
}
