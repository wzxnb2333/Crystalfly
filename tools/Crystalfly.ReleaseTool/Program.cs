using System.Security.Cryptography;
using System.Text.Json;
using Crystalfly.Core.Serialization;
using Crystalfly.Core.Updates;
using NSec.Cryptography;

return await ReleaseTool.RunAsync(args);

internal static class ReleaseTool
{
    public static async Task<int> RunAsync(string[] args)
    {
        try
        {
            if (args.Length == 5 && args[0] == "generate-key")
            {
                GenerateKey(RequiredOption(args, "--private-env"), RequiredOption(args, "--public-out"));
                return 0;
            }
            if (args.Length == 17 && args[0] == "sign")
            {
                await SignAsync(
                    RequiredOption(args, "--version"),
                    RequiredOption(args, "--notes"),
                    RequiredOption(args, "--portable"),
                    RequiredOption(args, "--installer"),
                    RequiredOption(args, "--private-env"),
                    RequiredOption(args, "--output"),
                    RequiredOption(args, "--published-at"),
                    RequiredOption(args, "--tag"));
                return 0;
            }
            if (args.Length == 5 && args[0] == "verify")
            {
                Verify(
                    RequiredOption(args, "--manifest"),
                    RequiredOption(args, "--public-key"));
                return 0;
            }
            throw new ArgumentException("Unsupported release tool command or argument count.");
        }
        catch (Exception exception) when (exception is ArgumentException
            or InvalidDataException
            or IOException
            or UnauthorizedAccessException
            or CryptographicException
            or FormatException)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static void GenerateKey(string privateEnvironmentPath, string publicOutputPath)
    {
        var privatePath = Path.GetFullPath(privateEnvironmentPath);
        var publicPath = Path.GetFullPath(publicOutputPath);
        if (File.Exists(privatePath) || File.Exists(publicPath))
        {
            throw new IOException("Refusing to overwrite an existing update signing key.");
        }
        Directory.CreateDirectory(Path.GetDirectoryName(privatePath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(publicPath)!);
        using var key = Key.Create(
            SignatureAlgorithm.Ed25519,
            new KeyCreationParameters { ExportPolicy = KeyExportPolicies.AllowPlaintextExport });
        File.WriteAllText(
            privatePath,
            $"CRYSTALFLY_UPDATE_SIGNING_KEY={Convert.ToBase64String(key.Export(KeyBlobFormat.RawPrivateKey))}{Environment.NewLine}");
        File.WriteAllText(
            publicPath,
            Convert.ToBase64String(key.PublicKey.Export(KeyBlobFormat.RawPublicKey)) + Environment.NewLine);
    }

    private static async Task SignAsync(
        string version,
        string notesPath,
        string portablePath,
        string installerPath,
        string privateEnvironmentPath,
        string outputPath,
        string publishedAtText,
        string tag)
    {
        if (!Version.TryParse(version, out var parsedVersion) || parsedVersion.ToString(3) != version)
        {
            throw new ArgumentException("The release version must contain exactly three numeric components.");
        }
        if (!DateTimeOffset.TryParse(
                publishedAtText,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out var publishedAt))
        {
            throw new ArgumentException("The publication time must use an ISO-8601 offset format.");
        }
        if (string.IsNullOrWhiteSpace(tag) || tag.IndexOfAny(['/', '\\']) >= 0)
        {
            throw new ArgumentException("The release tag is invalid.");
        }
        var privateKey = ReadEnvironmentValue(privateEnvironmentPath, "CRYSTALFLY_UPDATE_SIGNING_KEY");
        byte[] privateBytes;
        try
        {
            privateBytes = Convert.FromBase64String(privateKey);
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException("The update signing key is not valid base64.", exception);
        }
        using var key = Key.Import(
            SignatureAlgorithm.Ed25519,
            privateBytes,
            KeyBlobFormat.RawPrivateKey,
            new KeyCreationParameters { ExportPolicy = KeyExportPolicies.None });
        var notes = await File.ReadAllTextAsync(Path.GetFullPath(notesPath));
        var portable = await AssetAsync(UpdateAssetKind.Portable, portablePath, version, tag);
        var installer = await AssetAsync(UpdateAssetKind.Installer, installerPath, version, tag);
        var manifest = new UpdateManifest
        {
            Channel = "stable",
            Version = version,
            PublishedAt = publishedAt.ToUniversalTime(),
            NotesMarkdown = notes,
            Assets = [installer, portable]
        };
        var payload = JsonSerializer.SerializeToUtf8Bytes(manifest, CrystalflyJson.Options);
        var envelope = new SignedUpdateManifest
        {
            KeyId = "stable-1",
            Payload = Convert.ToBase64String(payload),
            Signature = Convert.ToBase64String(SignatureAlgorithm.Ed25519.Sign(key, payload))
        };
        await Crystalfly.Core.Serialization.AtomicJsonStore.WriteAsync(
            Path.GetFullPath(outputPath),
            envelope);
    }

    private static void Verify(string manifestPath, string publicKeyPath)
    {
        byte[] publicKey;
        try
        {
            publicKey = Convert.FromBase64String(File.ReadAllText(Path.GetFullPath(publicKeyPath)).Trim());
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException("The update verification public key is not valid base64.", exception);
        }
        UpdateManifest manifest = UpdateManifestVerifier.Verify(
            File.ReadAllText(Path.GetFullPath(manifestPath)),
            publicKey);
        Console.WriteLine($"Verified update manifest {manifest.Version}.");
    }

    private static async Task<UpdateAsset> AssetAsync(
        UpdateAssetKind kind,
        string path,
        string version,
        string tag)
    {
        var file = new FileInfo(Path.GetFullPath(path));
        if (!file.Exists || file.Length <= 0)
        {
            throw new FileNotFoundException("A release asset was not found.", file.FullName);
        }
        await using var stream = file.OpenRead();
        var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream));
        var assetName = kind == UpdateAssetKind.Installer
            ? $"Crystalfly-{version}-win-x64-setup.exe"
            : $"Crystalfly-{version}-win-x64-portable.zip";
        if (!string.Equals(file.Name, assetName, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Release asset name must be '{assetName}'.");
        }
        return new UpdateAsset
        {
            Kind = kind,
            Runtime = "win-x64",
            Url = $"https://github.com/wzxnb2333/Crystalfly/releases/download/{Uri.EscapeDataString(tag)}/{assetName}",
            Size = file.Length,
            Sha256 = hash
        };
    }

    private static string ReadEnvironmentValue(string path, string name)
    {
        foreach (var line in File.ReadLines(Path.GetFullPath(path)))
        {
            if (line.StartsWith(name + "=", StringComparison.Ordinal))
            {
                var value = line[(name.Length + 1)..].Trim();
                if (value.Length != 0)
                {
                    return value;
                }
            }
        }
        throw new InvalidDataException($"The private environment file does not contain {name}.");
    }

    private static string RequiredOption(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        if (index <= 0 || index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]))
        {
            throw new ArgumentException($"Missing required option: {name}.");
        }
        return args[index + 1];
    }
}
