using System.Text.Json;
using Crystalfly.Core.Serialization;
using Crystalfly.Core.Updates;
using NSec.Cryptography;

namespace Crystalfly.Core.Tests.Updates;

public sealed class UpdateManifestVerifierTests
{
    [Fact]
    public void Verify_accepts_signed_payload_and_returns_exact_assets()
    {
        using var key = CreateKey();
        var payload = Payload();
        string document = Sign(payload, key);

        UpdateManifest result = UpdateManifestVerifier.Verify(
            document,
            key.PublicKey.Export(KeyBlobFormat.RawPublicKey));

        Assert.Equal("0.6.0", result.Version);
        Assert.Equal(2, result.Assets.Count);
        Assert.Contains(result.Assets, asset => asset.Kind == UpdateAssetKind.Installer);
        Assert.Contains(result.Assets, asset => asset.Kind == UpdateAssetKind.Portable);
    }

    [Fact]
    public void Verify_rejects_payload_changed_after_signing()
    {
        using var key = CreateKey();
        var envelope = JsonSerializer.Deserialize<SignedUpdateManifest>(
            Sign(Payload(), key),
            CrystalflyJson.Options)!;
        var payload = Convert.FromBase64String(envelope.Payload);
        payload[^2] ^= 1;
        string changed = CrystalflyJson.Serialize(envelope with
        {
            Payload = Convert.ToBase64String(payload)
        });

        Assert.Throws<InvalidDataException>(() => UpdateManifestVerifier.Verify(
            changed,
            key.PublicKey.Export(KeyBlobFormat.RawPublicKey)));
    }

    [Fact]
    public void Verify_rejects_manifest_when_public_key_does_not_match_signing_key()
    {
        using var signingKey = CreateKey();
        using var otherKey = CreateKey();

        Assert.Throws<InvalidDataException>(() => UpdateManifestVerifier.Verify(
            Sign(Payload(), signingKey),
            otherKey.PublicKey.Export(KeyBlobFormat.RawPublicKey)));
    }

    [Fact]
    public void Verify_rejects_non_github_asset_even_with_a_valid_signature()
    {
        using var key = CreateKey();
        var payload = Payload() with
        {
            Assets =
            [
                Asset(UpdateAssetKind.Installer) with { Url = "https://example.com/setup.exe" },
                Asset(UpdateAssetKind.Portable)
            ]
        };

        Assert.Throws<InvalidDataException>(() => UpdateManifestVerifier.Verify(
            Sign(payload, key),
            key.PublicKey.Export(KeyBlobFormat.RawPublicKey)));
    }

    [Fact]
    public void Verify_rejects_duplicate_asset_kind()
    {
        using var key = CreateKey();
        var payload = Payload() with
        {
            Assets = [Asset(UpdateAssetKind.Portable), Asset(UpdateAssetKind.Portable)]
        };

        Assert.Throws<InvalidDataException>(() => UpdateManifestVerifier.Verify(
            Sign(payload, key),
            key.PublicKey.Export(KeyBlobFormat.RawPublicKey)));
    }

    [Fact]
    public void Verify_wraps_null_envelope_members_as_invalid_data()
    {
        using var key = CreateKey();
        string signature = Convert.ToBase64String(new byte[SignatureAlgorithm.Ed25519.SignatureSize]);
        var publicKey = key.PublicKey.Export(KeyBlobFormat.RawPublicKey);

        Assert.Throws<InvalidDataException>(() => UpdateManifestVerifier.Verify(
            $$"""{"schemaVersion":1,"keyId":"stable-1","payload":null,"signature":"{{signature}}"}""",
            publicKey));
        Assert.Throws<InvalidDataException>(() => UpdateManifestVerifier.Verify(
            """{"schemaVersion":1,"keyId":"stable-1","payload":"AA==","signature":null}""",
            publicKey));
    }

    [Theory]
    [MemberData(nameof(InvalidSignedPayloads))]
    public void Verify_wraps_null_signed_payload_members_as_invalid_data(string payloadJson)
    {
        using var key = CreateKey();

        Assert.Throws<InvalidDataException>(() => UpdateManifestVerifier.Verify(
            SignBytes(System.Text.Encoding.UTF8.GetBytes(payloadJson), key),
            key.PublicKey.Export(KeyBlobFormat.RawPublicKey)));
    }

    public static TheoryData<string> InvalidSignedPayloads => new()
    {
        PayloadJson(channel: "null"),
        PayloadJson(version: "null"),
        PayloadJson(notes: "null"),
        PayloadJson(assets: "null"),
        PayloadJson(assets: "[null,{\"kind\":\"portable\",\"runtime\":\"win-x64\",\"url\":\"https://github.com/wzxnb2333/Crystalfly/releases/download/v0.6.0/Portable.bin\",\"size\":1024,\"sha256\":\"BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB\"}]"),
        PayloadJson(installerRuntime: "null"),
        PayloadJson(installerUrl: "null"),
        PayloadJson(installerSha256: "null")
    };

    private static Key CreateKey() => Key.Create(
        SignatureAlgorithm.Ed25519,
        new KeyCreationParameters { ExportPolicy = KeyExportPolicies.AllowPlaintextExport });

    private static string Sign(UpdateManifest payload, Key key)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, CrystalflyJson.Options);
        return SignBytes(bytes, key);
    }

    private static string SignBytes(byte[] bytes, Key key)
    {
        return CrystalflyJson.Serialize(new SignedUpdateManifest
        {
            SchemaVersion = 1,
            KeyId = "stable-1",
            Payload = Convert.ToBase64String(bytes),
            Signature = Convert.ToBase64String(SignatureAlgorithm.Ed25519.Sign(key, bytes))
        });
    }

    private static string PayloadJson(
        string channel = "\"stable\"",
        string version = "\"0.6.0\"",
        string notes = "\"notes\"",
        string? assets = null,
        string installerRuntime = "\"win-x64\"",
        string installerUrl = "\"https://github.com/wzxnb2333/Crystalfly/releases/download/v0.6.0/Installer.bin\"",
        string installerSha256 = "\"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA\"")
    {
        assets ??= $$"""[{"kind":"installer","runtime":{{installerRuntime}},"url":{{installerUrl}},"size":1024,"sha256":{{installerSha256}}},{"kind":"portable","runtime":"win-x64","url":"https://github.com/wzxnb2333/Crystalfly/releases/download/v0.6.0/Portable.bin","size":1024,"sha256":"BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB"}]""";
        return $$"""{"schemaVersion":1,"channel":{{channel}},"version":{{version}},"publishedAt":"2026-07-23T00:00:00+00:00","notesMarkdown":{{notes}},"assets":{{assets}}}""";
    }

    private static UpdateManifest Payload() => new()
    {
        SchemaVersion = 1,
        Channel = "stable",
        Version = "0.6.0",
        PublishedAt = new DateTimeOffset(2026, 7, 23, 0, 0, 0, TimeSpan.Zero),
        NotesMarkdown = "Signed update notes.",
        Assets = [Asset(UpdateAssetKind.Installer), Asset(UpdateAssetKind.Portable)]
    };

    private static UpdateAsset Asset(UpdateAssetKind kind) => new()
    {
        Kind = kind,
        Runtime = "win-x64",
        Url = $"https://github.com/wzxnb2333/Crystalfly/releases/download/v0.6.0/{kind}.bin",
        Size = 1024,
        Sha256 = new string(kind == UpdateAssetKind.Installer ? 'A' : 'B', 64)
    };
}
