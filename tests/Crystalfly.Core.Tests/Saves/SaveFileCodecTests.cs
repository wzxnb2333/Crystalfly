using Crystalfly.Core.Saves;

namespace Crystalfly.Core.Tests.Saves;

public sealed class SaveFileCodecTests
{
    [Fact]
    public void Encrypt_then_decrypt_roundtrips_json()
    {
        const string json = """{"health":5,"maxHealth":9,"geo":1200,"chars":["knight","hornet"],"nested":{"a":true}}""";

        var encrypted = SaveFileCodec.Encrypt(json);
        var decrypted = SaveFileCodec.Decrypt(encrypted);

        Assert.Equal(json, decrypted);
    }

    [Fact]
    public void Encrypt_produces_valid_frame_structure()
    {
        var data = SaveFileCodec.Encrypt("{}");

        // Header check
        Assert.Equal(0x00, data[0]);
        Assert.Equal(0x01, data[1]);
        Assert.Equal(0xFF, data[5]);
        // Footer check
        Assert.Equal(0x0B, data[^1]);
    }

    [Fact]
    public void Decrypt_rejects_short_data()
    {
        Assert.Throws<InvalidDataException>(() => SaveFileCodec.Decrypt([0x00, 0x01]));
    }

    [Fact]
    public void Decrypt_rejects_invalid_header()
    {
        var data = SaveFileCodec.Encrypt("{}");
        data[0] = 0xFF; // corrupt header

        Assert.Throws<InvalidDataException>(() => SaveFileCodec.Decrypt(data));
    }

    [Fact]
    public void Decrypt_rejects_invalid_footer()
    {
        var data = SaveFileCodec.Encrypt("{}");
        data[^1] = 0x00; // corrupt footer

        Assert.Throws<InvalidDataException>(() => SaveFileCodec.Decrypt(data));
    }

    [Fact]
    public void Roundtrip_empty_json_object()
    {
        const string json = "{}";
        Assert.Equal(json, SaveFileCodec.Decrypt(SaveFileCodec.Encrypt(json)));
    }

    [Fact]
    public void Roundtrip_large_json()
    {
        var json = "{\"data\":\"" + new string('x', 100_000) + "\"}";
        Assert.Equal(json, SaveFileCodec.Decrypt(SaveFileCodec.Encrypt(json)));
    }

    [Fact]
    public void Roundtrip_unicode_json()
    {
        const string json = """{"name":"空洞骑士","version":"1.5.12620.0"}""";
        Assert.Equal(json, SaveFileCodec.Decrypt(SaveFileCodec.Encrypt(json)));
    }

    [Fact]
    public async Task File_roundtrip()
    {
        var path = Path.Combine(Path.GetTempPath(), $"crystalfly-save-test-{Guid.NewGuid():N}.dat");
        try
        {
            const string json = """{"test":true}""";
            await SaveFileCodec.EncryptAsync(path, json);
            var result = await SaveFileCodec.DecryptAsync(path);
            Assert.Equal(json, result);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
