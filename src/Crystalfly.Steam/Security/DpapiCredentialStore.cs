using System.Security.Cryptography;
using System.Text.Json;

namespace Crystalfly.Steam.Security;

public sealed class DpapiCredentialStore(string path)
{
    private static readonly byte[] Entropy = "Crystalfly.Steam.Credential.v1"u8.ToArray();

    public async Task SaveAsync(SteamUsernameCredential credential, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credential);
        if (string.IsNullOrWhiteSpace(credential.Username) || string.IsNullOrEmpty(credential.Password))
            throw new ArgumentException("Steam username and password are required.", nameof(credential));

        string fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        byte[] plaintext = JsonSerializer.SerializeToUtf8Bytes(credential);
        byte[] ciphertext = ProtectedData.Protect(plaintext, Entropy, DataProtectionScope.CurrentUser);
        string temporaryPath = fullPath + ".tmp";
        try
        {
            await File.WriteAllBytesAsync(temporaryPath, ciphertext, cancellationToken);
            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    public async Task<SteamUsernameCredential?> LoadAsync(CancellationToken cancellationToken = default)
    {
        string fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
            return null;

        byte[] ciphertext = await File.ReadAllBytesAsync(fullPath, cancellationToken);
        byte[] plaintext = ProtectedData.Unprotect(ciphertext, Entropy, DataProtectionScope.CurrentUser);
        return JsonSerializer.Deserialize<SteamUsernameCredential>(plaintext)
            ?? throw new InvalidDataException("Stored Steam username credential is empty.");
    }

    public void Delete()
    {
        string fullPath = Path.GetFullPath(path);
        File.Delete(fullPath);
        File.Delete(fullPath + ".tmp");
    }
}
