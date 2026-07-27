using System.Buffers.Binary;
using System.Security.Cryptography;
using Avalonia.Media.Imaging;
using Crystalfly.Core.Configuration;

namespace Crystalfly.App.Appearance;

internal sealed class BackgroundImageService
{
    public const long MaximumFileSizeBytes = 25L * 1024 * 1024;
    public const int MaximumDimension = 8192;
    public const long MaximumPixelCount = 40_000_000;

    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".webp", ".bmp" };

    public static bool IsSupportedFileName(string path) =>
        SupportedExtensions.Contains(Path.GetExtension(path));

    public async Task ValidateAsync(string sourcePath, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.GetFullPath(sourcePath);
        if (!IsSupportedFileName(fullPath))
        {
            throw new InvalidDataException("Unsupported background image format.");
        }

        var length = new FileInfo(fullPath).Length;
        if (length is <= 0 or > MaximumFileSizeBytes)
        {
            throw new InvalidDataException("Background image file size is outside the supported range.");
        }

        var bytes = await File.ReadAllBytesAsync(fullPath, cancellationToken);
        var metadata = ReadMetadata(bytes, Path.GetExtension(fullPath));
        if (metadata.IsAnimated)
        {
            throw new InvalidDataException("Animated background images are not supported.");
        }
        if (metadata.Width <= 0
            || metadata.Height <= 0
            || metadata.Width > MaximumDimension
            || metadata.Height > MaximumDimension
            || (long)metadata.Width * metadata.Height > MaximumPixelCount)
        {
            throw new InvalidDataException("Background image dimensions are outside the supported range.");
        }

        try
        {
            using var bitmap = new Bitmap(fullPath);
            if (bitmap.PixelSize.Width != metadata.Width || bitmap.PixelSize.Height != metadata.Height)
            {
                throw new InvalidDataException("Background image dimensions do not match the decoded image.");
            }
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException)
        {
            throw new InvalidDataException("Background image could not be decoded.", exception);
        }
    }

    public async Task<BackgroundImageSettings> ReplaceAsync(
        string sourcePath,
        string destinationDirectory,
        BackgroundImageSettings? current,
        Func<BackgroundImageSettings, CancellationToken, Task> saveSettingsAsync,
        CancellationToken cancellationToken = default)
    {
        await ValidateAsync(sourcePath, cancellationToken);
        destinationDirectory = Path.GetFullPath(destinationDirectory);
        Directory.CreateDirectory(destinationDirectory);

        string hash;
        await using (var source = new FileStream(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan))
        {
            hash = Convert.ToHexString(await SHA256.HashDataAsync(source, cancellationToken));
        }

        var fileName = hash + Path.GetExtension(sourcePath).ToLowerInvariant();
        var targetPath = Path.Combine(destinationDirectory, fileName);
        var temporaryPath = Path.Combine(destinationDirectory, $".{fileName}.{Guid.NewGuid():N}.tmp");
        var createdTarget = false;
        var settingsSaved = false;
        try
        {
            if (!File.Exists(targetPath))
            {
                await using (var source = new FileStream(
                    sourcePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    81920,
                    FileOptions.Asynchronous | FileOptions.SequentialScan))
                await using (var target = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    81920,
                    FileOptions.Asynchronous | FileOptions.WriteThrough))
                {
                    await source.CopyToAsync(target, cancellationToken);
                    await target.FlushAsync(cancellationToken);
                }
                File.Move(temporaryPath, targetPath);
                createdTarget = true;
            }

            var next = new BackgroundImageSettings
            {
                FileName = fileName,
                OpacityPercent = current?.OpacityPercent ?? BackgroundImageSettings.DefaultOpacityPercent
            };
            await saveSettingsAsync(next, cancellationToken);
            settingsSaved = true;
            DeleteIfSuperseded(destinationDirectory, current?.FileName, fileName);
            return next;
        }
        catch
        {
            if (createdTarget && !settingsSaved)
            {
                File.Delete(targetPath);
            }
            throw;
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    public async Task RemoveAsync(
        string destinationDirectory,
        BackgroundImageSettings? current,
        Func<BackgroundImageSettings?, CancellationToken, Task> saveSettingsAsync,
        CancellationToken cancellationToken = default)
    {
        await saveSettingsAsync(null, cancellationToken);
        DeleteIfSuperseded(Path.GetFullPath(destinationDirectory), current?.FileName, null);
    }

    private static void DeleteIfSuperseded(string directory, string? oldFileName, string? newFileName)
    {
        if (!BackgroundImageSettings.IsSafeFileName(oldFileName)
            || string.Equals(oldFileName, newFileName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        try
        {
            File.Delete(Path.Combine(directory, oldFileName!));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The saved configuration already points at the replacement; leave a locked old copy for later cleanup.
        }
    }

    private static ImageMetadata ReadMetadata(ReadOnlySpan<byte> bytes, string extension) =>
        extension.ToLowerInvariant() switch
        {
            ".png" => ReadPng(bytes),
            ".jpg" or ".jpeg" => ReadJpeg(bytes),
            ".webp" => ReadWebP(bytes),
            ".bmp" => ReadBmp(bytes),
            _ => throw new InvalidDataException("Unsupported background image format.")
        };

    private static ImageMetadata ReadPng(ReadOnlySpan<byte> bytes)
    {
        ReadOnlySpan<byte> signature = [137, 80, 78, 71, 13, 10, 26, 10];
        if (bytes.Length < 24 || !bytes[..8].SequenceEqual(signature) || !bytes.Slice(12, 4).SequenceEqual("IHDR"u8))
        {
            throw new InvalidDataException("Invalid PNG image.");
        }
        var animated = false;
        for (var offset = 8; offset + 12 <= bytes.Length;)
        {
            var length = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(offset, 4));
            var next = (long)offset + 12 + length;
            if (next > bytes.Length)
            {
                throw new InvalidDataException("Invalid PNG chunk length.");
            }
            animated |= bytes.Slice(offset + 4, 4).SequenceEqual("acTL"u8);
            offset = (int)next;
        }
        return new(
            checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(16, 4))),
            checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(20, 4))),
            animated);
    }

    private static ImageMetadata ReadJpeg(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 4 || bytes[0] != 0xFF || bytes[1] != 0xD8)
        {
            throw new InvalidDataException("Invalid JPEG image.");
        }
        for (var offset = 2; offset + 4 <= bytes.Length;)
        {
            while (offset < bytes.Length && bytes[offset] == 0xFF)
            {
                offset++;
            }
            if (offset >= bytes.Length)
            {
                break;
            }
            var marker = bytes[offset++];
            if (marker is 0xD8 or 0xD9 || marker is >= 0xD0 and <= 0xD7)
            {
                continue;
            }
            if (offset + 2 > bytes.Length)
            {
                break;
            }
            var length = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(offset, 2));
            if (length < 2 || offset + length > bytes.Length)
            {
                throw new InvalidDataException("Invalid JPEG segment length.");
            }
            if (IsJpegStartOfFrame(marker) && length >= 7)
            {
                return new(
                    BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(offset + 5, 2)),
                    BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(offset + 3, 2)),
                    false);
            }
            offset += length;
        }
        throw new InvalidDataException("JPEG dimensions were not found.");
    }

    private static bool IsJpegStartOfFrame(byte marker) =>
        marker is >= 0xC0 and <= 0xC3
            or >= 0xC5 and <= 0xC7
            or >= 0xC9 and <= 0xCB
            or >= 0xCD and <= 0xCF;

    private static ImageMetadata ReadBmp(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 26 || bytes[0] != (byte)'B' || bytes[1] != (byte)'M')
        {
            throw new InvalidDataException("Invalid BMP image.");
        }
        var dibSize = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(14, 4));
        if (dibSize < 40 || bytes.Length < 26)
        {
            throw new InvalidDataException("Unsupported BMP header.");
        }
        return new(
            Math.Abs(BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(18, 4))),
            Math.Abs(BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(22, 4))),
            false);
    }

    private static ImageMetadata ReadWebP(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 20 || !bytes[..4].SequenceEqual("RIFF"u8) || !bytes.Slice(8, 4).SequenceEqual("WEBP"u8))
        {
            throw new InvalidDataException("Invalid WebP image.");
        }
        ImageMetadata? metadata = null;
        var animated = false;
        for (var offset = 12; offset + 8 <= bytes.Length;)
        {
            var chunkType = bytes.Slice(offset, 4);
            var length = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset + 4, 4));
            var payloadOffset = offset + 8;
            var next = (long)payloadOffset + length + (length & 1);
            if (next > bytes.Length)
            {
                throw new InvalidDataException("Invalid WebP chunk length.");
            }
            var payload = bytes.Slice(payloadOffset, checked((int)length));
            if (chunkType.SequenceEqual("VP8X"u8) && payload.Length >= 10)
            {
                animated |= (payload[0] & 0x02) != 0;
                metadata = new(
                    1 + ReadUInt24LittleEndian(payload.Slice(4, 3)),
                    1 + ReadUInt24LittleEndian(payload.Slice(7, 3)),
                    animated);
            }
            else if (chunkType.SequenceEqual("VP8 "u8)
                && payload.Length >= 10
                && payload[3] == 0x9D
                && payload[4] == 0x01
                && payload[5] == 0x2A)
            {
                metadata ??= new(
                    BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(6, 2)) & 0x3FFF,
                    BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(8, 2)) & 0x3FFF,
                    animated);
            }
            else if (chunkType.SequenceEqual("VP8L"u8) && payload.Length >= 5 && payload[0] == 0x2F)
            {
                metadata ??= new(
                    1 + (((payload[2] & 0x3F) << 8) | payload[1]),
                    1 + ((payload[4] << 10) | (payload[3] << 2) | (payload[2] >> 6)),
                    animated);
            }
            animated |= chunkType.SequenceEqual("ANIM"u8) || chunkType.SequenceEqual("ANMF"u8);
            offset = (int)next;
        }
        return metadata is { } value
            ? value with { IsAnimated = animated }
            : throw new InvalidDataException("WebP dimensions were not found.");
    }

    private static int ReadUInt24LittleEndian(ReadOnlySpan<byte> bytes) =>
        bytes[0] | (bytes[1] << 8) | (bytes[2] << 16);

    private sealed record ImageMetadata(int Width, int Height, bool IsAnimated);
}
