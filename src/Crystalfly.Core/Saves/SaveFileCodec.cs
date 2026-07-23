using System.Security.Cryptography;
using System.Text;

namespace Crystalfly.Core.Saves;

/// <summary>
/// Reads and writes Hollow Knight <c>user*.dat</c> save files.
/// File format: 22-byte header | Varint content length | Base64(AES-ECB-PKCS7(JSON)) | 0x0B footer.
/// </summary>
public static class SaveFileCodec
{
    private static readonly byte[] Header =
    [
        0x00, 0x01, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0x01, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x06, 0x01, 0x00, 0x00, 0x00
    ];

    private const byte Footer = 0x0B;

    private static readonly byte[] Key = Encoding.UTF8.GetBytes("UKu52ePUBwetZ9wNX88o54dnfKRu0T1l");

    public static async Task<string> DecryptAsync(string path, CancellationToken cancellationToken = default)
    {
        var data = await File.ReadAllBytesAsync(path, cancellationToken);
        return Decrypt(data);
    }

    public static string Decrypt(byte[] data)
    {
        if (data.Length < Header.Length + 1 + 1)
        {
            throw new InvalidDataException("Save file is too short.");
        }

        for (var i = 0; i < Header.Length; i++)
        {
            if (data[i] != Header[i])
            {
                throw new InvalidDataException($"Save file header mismatch at byte {i}.");
            }
        }

        if (data[^1] != Footer)
        {
            throw new InvalidDataException("Save file footer mismatch.");
        }

        var offset = Header.Length;
        var contentLength = ReadVarint(data, ref offset);
        var available = data.Length - offset - 1; // exclude footer
        if (contentLength > available)
        {
            throw new InvalidDataException(
                $"Save file content length {contentLength} exceeds available bytes {available}.");
        }

        var base64 = Encoding.ASCII.GetString(data, offset, contentLength);
        byte[] encrypted;
        try
        {
            encrypted = Convert.FromBase64String(base64);
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException("Save file content is not valid Base64.", exception);
        }

        using var aes = Aes.Create();
        aes.Key = Key;
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.PKCS7;
        using var decryptor = aes.CreateDecryptor();
        var json = decryptor.TransformFinalBlock(encrypted, 0, encrypted.Length);
        return Encoding.UTF8.GetString(json);
    }

    public static async Task EncryptAsync(string path, string json, CancellationToken cancellationToken = default)
    {
        var data = Encrypt(json);
        await File.WriteAllBytesAsync(path, data, cancellationToken);
    }

    public static byte[] Encrypt(string json)
    {
        var plainBytes = Encoding.UTF8.GetBytes(json);
        using var aes = Aes.Create();
        aes.Key = Key;
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.PKCS7;
        using var encryptor = aes.CreateEncryptor();
        var encrypted = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        var base64 = Encoding.ASCII.GetBytes(Convert.ToBase64String(encrypted));

        var varint = WriteVarint(base64.Length);
        var result = new byte[Header.Length + varint.Length + base64.Length + 1];
        Header.CopyTo(result, 0);
        varint.CopyTo(result, Header.Length);
        base64.CopyTo(result, Header.Length + varint.Length);
        result[^1] = Footer;
        return result;
    }

    private static int ReadVarint(byte[] data, ref int offset)
    {
        var result = 0;
        var shift = 0;
        while (offset < data.Length)
        {
            var b = data[offset++];
            result |= (b & 0x7F) << shift;
            if ((b & 0x80) == 0)
            {
                return result;
            }

            shift += 7;
            if (shift >= 35)
            {
                throw new InvalidDataException("Save file Varint is too long.");
            }
        }

        throw new InvalidDataException("Save file Varint is truncated.");
    }

    private static byte[] WriteVarint(int value)
    {
        var bytes = new List<byte>(5);
        while (value > 0x7F)
        {
            bytes.Add((byte)((value & 0x7F) | 0x80));
            value >>= 7;
        }

        bytes.Add((byte)(value & 0x7F));
        return [.. bytes];
    }
}
