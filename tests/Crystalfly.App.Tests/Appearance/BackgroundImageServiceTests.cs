using System.Buffers.Binary;
using Avalonia.Headless.XUnit;
using Crystalfly.App.Appearance;
using Crystalfly.Core.Configuration;
using SkiaSharp;

namespace Crystalfly.App.Tests.Appearance;

public sealed class BackgroundImageServiceTests : IDisposable
{
    private readonly string root = Path.Combine(
        Path.GetTempPath(),
        $"crystalfly-background-{Guid.NewGuid():N}");

    [Theory]
    [InlineData("image.png", true)]
    [InlineData("image.jpg", true)]
    [InlineData("image.jpeg", true)]
    [InlineData("image.webp", true)]
    [InlineData("image.bmp", true)]
    [InlineData("image.svg", false)]
    [InlineData("image.gif", false)]
    public void Is_supported_file_name_accepts_only_static_raster_extensions(string fileName, bool expected) =>
        Assert.Equal(expected, BackgroundImageService.IsSupportedFileName(fileName));

    [AvaloniaFact]
    public async Task Replace_validates_decodes_hashes_and_preserves_opacity()
    {
        var source = CreateBmp("source.bmp", 2, 2);
        var destination = Path.Combine(root, "appearance");
        var current = new BackgroundImageSettings { FileName = "old.bmp", OpacityPercent = 64 };
        Directory.CreateDirectory(destination);
        await File.WriteAllTextAsync(Path.Combine(destination, current.FileName), "old");
        BackgroundImageSettings? saved = null;

        var result = await new BackgroundImageService().ReplaceAsync(
            source,
            destination,
            current,
            (settings, _) =>
            {
                saved = settings;
                return Task.CompletedTask;
            });

        Assert.Equal(64, result.OpacityPercent);
        Assert.Equal(result, saved);
        Assert.Matches("^[A-F0-9]{64}\\.bmp$", result.FileName);
        Assert.True(File.Exists(Path.Combine(destination, result.FileName)));
        Assert.False(File.Exists(Path.Combine(destination, current.FileName)));
    }

    [AvaloniaFact]
    public async Task Replace_uses_default_opacity_for_first_upload()
    {
        var source = CreateBmp("first.bmp", 1, 1);

        var result = await new BackgroundImageService().ReplaceAsync(
            source,
            Path.Combine(root, "appearance"),
            null,
            static (_, _) => Task.CompletedTask);

        Assert.Equal(BackgroundImageSettings.DefaultOpacityPercent, result.OpacityPercent);
    }

    [AvaloniaFact]
    public async Task Replace_keeps_old_background_when_settings_save_fails()
    {
        var source = CreateBmp("new.bmp", 2, 2);
        var destination = Path.Combine(root, "appearance");
        var current = new BackgroundImageSettings { FileName = "old.bmp", OpacityPercent = 35 };
        Directory.CreateDirectory(destination);
        await File.WriteAllTextAsync(Path.Combine(destination, current.FileName), "old");

        await Assert.ThrowsAsync<IOException>(() => new BackgroundImageService().ReplaceAsync(
            source,
            destination,
            current,
            static (_, _) => throw new IOException("save failed")));

        Assert.True(File.Exists(Path.Combine(destination, current.FileName)));
        Assert.Single(Directory.EnumerateFiles(destination));
        Assert.Empty(Directory.EnumerateFiles(destination, "*.tmp"));
    }

    [Fact]
    public async Task Remove_deletes_file_only_after_settings_save_succeeds()
    {
        var destination = Path.Combine(root, "appearance");
        var current = new BackgroundImageSettings { FileName = "old.bmp", OpacityPercent = 35 };
        Directory.CreateDirectory(destination);
        await File.WriteAllTextAsync(Path.Combine(destination, current.FileName), "old");
        var service = new BackgroundImageService();

        await Assert.ThrowsAsync<IOException>(() => service.RemoveAsync(
            destination,
            current,
            static (_, _) => throw new IOException("save failed")));
        Assert.True(File.Exists(Path.Combine(destination, current.FileName)));

        await service.RemoveAsync(destination, current, static (_, _) => Task.CompletedTask);
        Assert.False(File.Exists(Path.Combine(destination, current.FileName)));
    }

    [AvaloniaFact]
    public async Task Validate_rejects_large_file_oversized_dimensions_animation_and_invalid_content()
    {
        var service = new BackgroundImageService();
        var tooLarge = Path.Combine(root, "large.bmp");
        Directory.CreateDirectory(root);
        await using (var stream = new FileStream(tooLarge, FileMode.Create, FileAccess.Write))
        {
            stream.SetLength(BackgroundImageService.MaximumFileSizeBytes + 1);
        }
        var tooWide = CreateBmp("wide.bmp", BackgroundImageService.MaximumDimension + 1, 1, includePixels: false);
        var tooManyPixels = CreateBmp("pixels.bmp", 8000, 5001, includePixels: false);
        var animated = CreateAnimatedWebP("animated.webp");
        var invalid = Path.Combine(root, "invalid.png");
        await File.WriteAllTextAsync(invalid, "not an image");

        await Assert.ThrowsAsync<InvalidDataException>(() => service.ValidateAsync(tooLarge));
        await Assert.ThrowsAsync<InvalidDataException>(() => service.ValidateAsync(tooWide));
        await Assert.ThrowsAsync<InvalidDataException>(() => service.ValidateAsync(tooManyPixels));
        await Assert.ThrowsAsync<InvalidDataException>(() => service.ValidateAsync(animated));
        await Assert.ThrowsAsync<InvalidDataException>(() => service.ValidateAsync(invalid));
    }
    [AvaloniaTheory]
    [InlineData("valid.png", SKEncodedImageFormat.Png)]
    [InlineData("valid.jpg", SKEncodedImageFormat.Jpeg)]
    [InlineData("valid.webp", SKEncodedImageFormat.Webp)]
    public async Task Validate_accepts_decodable_png_jpeg_and_webp(
        string name,
        SKEncodedImageFormat format)
    {
        var source = CreateEncodedImage(name, format);

        await new BackgroundImageService().ValidateAsync(source);
    }

    [AvaloniaFact]
    public async Task Validate_rejects_apng_animation_chunk()
    {
        var source = CreateAnimatedPng("animated.png");

        await Assert.ThrowsAsync<InvalidDataException>(
            () => new BackgroundImageService().ValidateAsync(source));
    }

    [AvaloniaFact]
    public async Task Replace_keeps_published_background_when_old_file_cleanup_is_blocked()
    {
        var source = CreateBmp("replacement.bmp", 2, 2);
        var destination = Path.Combine(root, "appearance-locked");
        var current = new BackgroundImageSettings { FileName = "old.bmp", OpacityPercent = 35 };
        Directory.CreateDirectory(destination);
        var oldPath = Path.Combine(destination, current.FileName);
        await File.WriteAllTextAsync(oldPath, "old");
        await using var locked = new FileStream(oldPath, FileMode.Open, FileAccess.Read, FileShare.None);
        BackgroundImageSettings? saved = null;

        var result = await new BackgroundImageService().ReplaceAsync(
            source,
            destination,
            current,
            (settings, _) =>
            {
                saved = settings;
                return Task.CompletedTask;
            });

        Assert.Equal(result, saved);
        Assert.True(File.Exists(Path.Combine(destination, result.FileName)));
        Assert.True(File.Exists(oldPath));
    }

    private string CreateEncodedImage(string name, SKEncodedImageFormat format)
    {
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, name);
        using var bitmap = new SKBitmap(2, 2);
        bitmap.Erase(SKColors.CornflowerBlue);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(format, 90);
        using var stream = File.Create(path);
        encoded.SaveTo(stream);
        return path;
    }

    private string CreateAnimatedPng(string name)
    {
        var source = File.ReadAllBytes(CreateEncodedImage(name, SKEncodedImageFormat.Png));
        var chunk = new byte[20];
        BinaryPrimitives.WriteUInt32BigEndian(chunk, 8);
        "acTL"u8.CopyTo(chunk.AsSpan(4));
        BinaryPrimitives.WriteUInt32BigEndian(chunk.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt32BigEndian(chunk.AsSpan(12), 0);
        var result = new byte[source.Length + chunk.Length];
        source.AsSpan(0, 33).CopyTo(result);
        chunk.CopyTo(result.AsSpan(33));
        source.AsSpan(33).CopyTo(result.AsSpan(53));
        var path = Path.Combine(root, name);
        File.WriteAllBytes(path, result);
        return path;
    }


    private string CreateBmp(string name, int width, int height, bool includePixels = true)
    {
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, name);
        var rowSize = checked((width * 3 + 3) & ~3);
        var pixelBytes = includePixels ? checked(rowSize * height) : 0;
        var bytes = new byte[54 + pixelBytes];
        bytes[0] = (byte)'B';
        bytes[1] = (byte)'M';
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(2), bytes.Length);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(10), 54);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(14), 40);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(18), width);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(22), height);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(26), 1);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(28), 24);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private string CreateAnimatedWebP(string name)
    {
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, name);
        var bytes = new byte[30];
        "RIFF"u8.CopyTo(bytes);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 22);
        "WEBPVP8X"u8.CopyTo(bytes.AsSpan(8));
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16), 10);
        bytes[20] = 0x02;
        File.WriteAllBytes(path, bytes);
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
