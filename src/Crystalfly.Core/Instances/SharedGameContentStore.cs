using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using Crystalfly.Core.Models;
using Crystalfly.Core.Runtime;
using Microsoft.Win32.SafeHandles;

namespace Crystalfly.Core.Instances;

/// <summary>Protected, content-addressed Unity resources. Mutable instance files never enter this store.</summary>
public sealed class SharedGameContentStore
{
    private readonly string versionRoot;
    private readonly string root;
    [SupportedOSPlatform("windows")]
    private static FileSystemRights DeniedWrites => FileSystemRights.WriteData | FileSystemRights.AppendData;

    public SharedGameContentStore(string versionRoot)
    {
        this.versionRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(versionRoot));
        root = Path.Combine(this.versionRoot, ".crystalfly", "shared-content-v1");
    }

    public static bool IsSupported(string path) => OperatingSystem.IsWindows()
        && !Path.GetFullPath(path).StartsWith(@"\\", StringComparison.Ordinal)
        && new DriveInfo(Path.GetPathRoot(Path.GetFullPath(path))!).DriveFormat.Equals("NTFS", StringComparison.OrdinalIgnoreCase);

    public static bool IsSteamDirectory(string path) => Path.GetFullPath(path).Split(Path.DirectorySeparatorChar)
        .Contains("steamapps", StringComparer.OrdinalIgnoreCase);

    internal static bool IsShareable(string relativePath, GameId game)
    {
        var path = relativePath.Replace('\\', '/');
        var prefix = GameDefinition.Get(game).DataDirectoryName + "/";
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
        var dataPath = path[prefix.Length..];
        if (dataPath.StartsWith("Managed/", StringComparison.OrdinalIgnoreCase)
            || dataPath.StartsWith("Plugins/", StringComparison.OrdinalIgnoreCase)) return false;
        var extension = Path.GetExtension(dataPath).ToLowerInvariant();
        return extension is ".assets" or ".ress" or ".resource" or ".bundle" or ".unity3d" or ".bank" or ".fsb"
            || (!dataPath.Contains('/') && dataPath.StartsWith("level", StringComparison.Ordinal)
                && int.TryParse(dataPath.AsSpan(5), out _));
    }

    public async Task<InstanceRecord> OptimizeAsync(InstanceRecord instance, CancellationToken token = default,
        IProgress<(int Completed, int Total)>? progress = null)
    {
        ValidateInstance(instance);
        if (!IsSupported(instance.RootPath) || !OperatingSystem.IsWindows()) return instance;
        if (IsSteamDirectory(instance.RootPath)) throw new InvalidOperationException("Steam installations must remain independent; clone the installation first.");
        await using var lease = await AcquireAsync(token);
        instance = await ReadRegisteredInstanceAsync(instance, token);
        var files = EnumerateSafeFiles(instance.RootPath).Where(path =>
            IsShareable(Path.GetRelativePath(instance.RootPath, path), instance.GameId) && new FileInfo(path).Length >= 1024 * 1024).ToArray();
        if (files.Length == 0) return instance;
        // Persist first: cancellation leaves a valid partially shared instance that can be resumed or detached.
        instance = instance with { UsesSharedContent = true };
        await InstanceSidecar.SaveAsync(instance, token);
        try
        {
            var completed = 0;
            foreach (var path in files)
            {
                token.ThrowIfCancellationRequested();
                if (!IsProtectedLink(path)) await ShareAsync(path, instance.GameId, token);
                progress?.Report((++completed, files.Length));
            }
        }
        finally { CollectUnusedCore(); }
        return instance;
    }

    public async Task<InstanceRecord> MaterializeAsync(InstanceRecord instance, CancellationToken token = default)
    {
        ValidateInstance(instance);
        if (!OperatingSystem.IsWindows()) return instance;
        await using var lease = await AcquireAsync(token);
        instance = await ReadRegisteredInstanceAsync(instance, token);
        try
        {
            foreach (var path in EnumerateSafeFiles(instance.RootPath))
            {
                token.ThrowIfCancellationRequested();
                if (!IsShareable(Path.GetRelativePath(instance.RootPath, path), instance.GameId) || !HasWriteProtection(path)) continue;
                var temporary = TemporaryPath();
                try
                {
                    await CopyVerifiedAsync(path, temporary, token);
                    InstanceDirectory.RejectReparseAncestors(path);
                    File.Move(temporary, path, overwrite: true);
                }
                finally { File.Delete(temporary); }
            }
            instance = instance with { UsesSharedContent = false };
            await InstanceSidecar.SaveAsync(instance, token);
        }
        finally { CollectUnusedCore(); }
        return instance;
    }

    internal static bool TryCloneFile(string source, string destination, string relativePath, GameId game)
    {
        if (!OperatingSystem.IsWindows() || !IsShareable(relativePath, game)) return false;
        InstanceDirectory.RejectReparseAncestors(source);
        InstanceDirectory.RejectReparseAncestors(destination);
        using var held = File.OpenHandle(source, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (!IsProtectedLink(source)) return false;
        CreateLink(destination, source);
        return true;
    }

    private static async Task<InstanceRecord> ReadRegisteredInstanceAsync(InstanceRecord expected, CancellationToken token)
    {
        var actual = await InstanceSidecar.LoadAsync(expected.RootPath, token);
        if (actual is null || actual.Id != expected.Id || actual.GameId != expected.GameId || actual.Purpose != expected.Purpose)
            throw new InvalidDataException("The instance identity changed before storage conversion.");
        return actual;
    }

    public async Task CollectUnusedAsync(CancellationToken token = default)
    {
        if (!OperatingSystem.IsWindows() || !Directory.Exists(root)) return;
        await using var lease = await AcquireAsync(token);
        CollectUnusedCore();
    }

    private void ValidateInstance(InstanceRecord instance)
    {
        if (!string.Equals(Path.GetDirectoryName(Path.GetFullPath(instance.RootPath)), versionRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The instance must belong to this version root.");
        InstanceDirectory.RejectReparseAncestors(instance.RootPath);
        InstanceDirectory.RejectReparseAncestors(root);
        if (new SystemHollowKnightProcessProbe(GameDefinition.Get(instance.GameId).ProcessName).IsRunning())
            throw new InvalidOperationException("Close the game before changing instance storage.");
    }

    private async Task<FileStream> AcquireAsync(CancellationToken token)
    {
        InstanceDirectory.RejectReparseAncestors(root);
        Directory.CreateDirectory(root);
        var lockPath = Path.Combine(root, "store.lock");
        InstanceDirectory.RejectReparseAncestors(lockPath);
        while (true)
        {
            token.ThrowIfCancellationRequested();
            FileStream lease;
            try { lease = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException error) when ((error.HResult & 0xffff) is 32 or 33)
            { await Task.Delay(50, token); continue; }
            try
            {
                foreach (var path in EnumerateSafeFiles(root).Where(path => Path.GetFileName(path).StartsWith("work-", StringComparison.Ordinal)))
                    File.Delete(path);
                return lease;
            }
            catch { lease.Dispose(); throw; }
        }
    }

    private string TemporaryPath() => Path.Combine(root, "work-" + Guid.NewGuid().ToString("N") + ".tmp");

    [SupportedOSPlatform("windows")]
    private async Task ShareAsync(string path, GameId game, CancellationToken token)
    {
        var temporary = TemporaryPath();
        var link = TemporaryPath();
        try
        {
            // Holding this handle denies in-place writes until the instance path has been atomically replaced.
            await using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete,
                1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(input, token));
            var directory = Path.Combine(root, game.ToString());
            var stored = Path.Combine(directory, hash + ".bin");
            InstanceDirectory.RejectReparseAncestors(stored);
            Directory.CreateDirectory(directory);
            if (!File.Exists(stored))
            {
                input.Position = 0;
                await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                    1024 * 1024, FileOptions.Asynchronous))
                {
                    await input.CopyToAsync(output, token);
                    await output.FlushAsync(token);
                    output.Flush(flushToDisk: true);
                }
                Protect(temporary);
                File.Move(temporary, stored);
            }
            await using (var existing = File.OpenRead(stored))
                if (!hash.Equals(Convert.ToHexString(await SHA256.HashDataAsync(existing, token)), StringComparison.Ordinal))
                    throw new InvalidDataException("Shared game content failed its hash check.");
            if (!HasWriteProtection(stored)) Protect(stored);
            CreateLink(link, stored);
            InstanceDirectory.RejectReparseAncestors(path);
            if (Identity(path) != Identity(input.SafeFileHandle)) throw new IOException("The source file changed while preparing shared content.");
            token.ThrowIfCancellationRequested();
            ReplaceOpenFile(link, path);
        }
        finally { File.Delete(link); File.Delete(temporary); }
    }

    private static async Task CopyVerifiedAsync(string source, string destination, CancellationToken token)
    {
        InstanceDirectory.RejectReparseAncestors(source);
        await using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, true);
        await using (var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 1024, true))
        {
            await input.CopyToAsync(output, token);
            await output.FlushAsync(token);
            output.Flush(flushToDisk: true);
        }
        input.Position = 0;
        var expected = await SHA256.HashDataAsync(input, token);
        await using var copied = File.OpenRead(destination);
        var actual = await SHA256.HashDataAsync(copied, token);
        if (!expected.SequenceEqual(actual)) throw new IOException("Independent copy failed its hash check.");
    }

    private void CollectUnusedCore()
    {
        if (!OperatingSystem.IsWindows()) return;
        foreach (var path in EnumerateSafeFiles(root).Where(path => Path.GetExtension(path) == ".bin"))
        {
            var name = Path.GetFileNameWithoutExtension(path);
            if (name.Length != 64 || !name.All(Uri.IsHexDigit)) continue;
            try { if (LinkCount(path) == 1) File.Delete(path); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
        }
    }

    private static IEnumerable<string> EnumerateSafeFiles(string directory)
    {
        InstanceDirectory.RejectReparseAncestors(directory);
        foreach (var path in Directory.EnumerateFileSystemEntries(directory))
        {
            InstanceDirectory.RejectReparseAncestors(path);
            if (Directory.Exists(path))
            {
                foreach (var child in EnumerateSafeFiles(path)) yield return child;
            }
            else yield return path;
        }
    }

    [SupportedOSPlatform("windows")]
    private static void Protect(string path)
    {
        var file = new FileInfo(path);
        var security = file.GetAccessControl();
        security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null),
            DeniedWrites, AccessControlType.Deny));
        file.SetAccessControl(security);
    }

    [SupportedOSPlatform("windows")]
    private static bool HasWriteProtection(string path) => new FileInfo(path).GetAccessControl()
        .GetAccessRules(true, false, typeof(SecurityIdentifier)).OfType<FileSystemAccessRule>()
        .Any(rule => rule.AccessControlType == AccessControlType.Deny && (rule.FileSystemRights & DeniedWrites) == DeniedWrites
            && rule.IdentityReference.Value == "S-1-1-0");

    [SupportedOSPlatform("windows")]
    private static bool IsProtectedLink(string path) => LinkCount(path) > 1 && HasWriteProtection(path);

    [SupportedOSPlatform("windows")]
    internal static uint LinkCount(string path)
    {
        using var file = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (!GetFileInformationByHandle(file, out var info)) throw new IOException("Cannot inspect shared content.", new Win32Exception());
        return info.NumberOfLinks;
    }

    [SupportedOSPlatform("windows")]
    internal static (uint Volume, ulong File) Identity(string path)
    {
        using var file = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return Identity(file);
    }

    [SupportedOSPlatform("windows")]
    private static (uint Volume, ulong File) Identity(SafeFileHandle file)
    {
        if (!GetFileInformationByHandle(file, out var info)) throw new IOException("Cannot inspect shared content.", new Win32Exception());
        return (info.VolumeSerialNumber, ((ulong)info.FileIndexHigh << 32) | info.FileIndexLow);
    }

    [SupportedOSPlatform("windows")]
    private static void CreateLink(string path, string source)
    {
        if (!CreateHardLinkW(path, source, 0)) throw new IOException("Cannot link shared content.", new Win32Exception());
    }

    [SupportedOSPlatform("windows")]
    private static void ReplaceOpenFile(string source, string destination)
    {
        // POSIX rename keeps the original read handle valid while replacing its directory entry atomically.
        using var handle = CreateFileW(source, 0x00010000, 7, 0, 3, 0, 0);
        if (handle.IsInvalid) throw new IOException("Cannot open the shared file for replacement.", new Win32Exception());
        var name = System.Text.Encoding.Unicode.GetBytes(destination);
        var nameOffset = 4 + 2 * IntPtr.Size;
        var information = new byte[nameOffset + name.Length];
        BitConverter.GetBytes(3u).CopyTo(information, 0); // REPLACE_IF_EXISTS | POSIX_SEMANTICS
        BitConverter.GetBytes(name.Length).CopyTo(information, 2 * IntPtr.Size);
        name.CopyTo(information, nameOffset);
        if (!SetFileInformationByHandle(handle, 22, information, information.Length))
            throw new IOException("Cannot atomically replace the instance resource.", new Win32Exception());
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileInformation
    {
        public uint FileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime, LastAccessTime, LastWriteTime;
        public uint VolumeSerialNumber, FileSizeHigh, FileSizeLow, NumberOfLinks, FileIndexHigh, FileIndexLow;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLinkW(string fileName, string existingFileName, nint securityAttributes);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle file, out FileInformation information);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
    private static extern SafeFileHandle CreateFileW(string path, uint access, uint share, nint security, uint creation, uint flags, nint template);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileInformationByHandle(SafeFileHandle file, int kind, byte[] information, int length);
}
