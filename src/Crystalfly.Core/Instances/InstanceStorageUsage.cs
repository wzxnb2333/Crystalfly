using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Crystalfly.Core.Instances;

public sealed record InstanceStorageUsage(long AllocatedBytes, long SharedSavingsBytes, int UnreadableEntries)
{
    public static InstanceStorageUsage Measure(IEnumerable<string> instanceRoots,
        IEnumerable<string> auxiliaryRoots, CancellationToken token = default)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        var identities = new HashSet<(uint Volume, ulong File)>();
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var clusters = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
        long allocated = 0, savings = 0;
        var unreadable = 0;
        foreach (var root in instanceRoots) Scan(root, primary: true);
        foreach (var root in auxiliaryRoots) Scan(root, primary: false);
        return new(allocated, savings, unreadable);

        void Scan(string root, bool primary)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                root = Path.GetFullPath(root);
                InstanceDirectory.RejectReparseAncestors(root);
                if (!Directory.Exists(root) && !primary) return;
                var volume = Path.GetPathRoot(root)!;
                if (!clusters.TryGetValue(volume, out var cluster))
                {
                    if (!GetDiskFreeSpaceW(volume, out var sectors, out var bytes, out _, out _))
                        throw new IOException("Cannot query allocation unit.", new Win32Exception());
                    clusters[volume] = cluster = checked(sectors * bytes);
                }
                var pending = new Stack<string>();
                pending.Push(root);
                while (pending.TryPop(out var directory))
                {
                    token.ThrowIfCancellationRequested();
                    try
                    {
                        if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                        { unreadable++; continue; }
                        foreach (var path in Directory.EnumerateFileSystemEntries(directory))
                        {
                            token.ThrowIfCancellationRequested();
                            try
                            {
                                var attributes = File.GetAttributes(path);
                                if ((attributes & FileAttributes.ReparsePoint) != 0) { unreadable++; continue; }
                                if ((attributes & FileAttributes.Directory) != 0) { pending.Push(path); continue; }
                                if (!paths.Add(path)) continue;
                                // Metadata-only access also works for files held open by the running game.
                                using var handle = CreateFileW(path, 0, 7, 0, 3, 0x00200000, 0);
                                if (handle.IsInvalid || !GetFileInformationByHandle(handle, out var info)
                                    || !GetFileInformationByHandleEx(handle, 1, out var standard, (uint)Marshal.SizeOf<StandardInfo>()))
                                    throw new IOException("Cannot query file allocation.", new Win32Exception());
                                if ((info.Attributes & (uint)FileAttributes.ReparsePoint) != 0) { unreadable++; continue; }
                                // Resident data belongs to MFT metadata, outside the file-data total.
                                var size = standard.AllocationSize < cluster ? 0 : standard.AllocationSize;
                                if (identities.Add((info.Volume, ((ulong)info.IndexHigh << 32) | info.IndexLow))) allocated += size;
                                else if (primary) savings += size;
                            }
                            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { unreadable++; }
                        }
                    }
                    catch (Exception error) when (error is IOException or UnauthorizedAccessException) { unreadable++; }
                }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException) { unreadable++; }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileInformation
    {
        public uint Attributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME Created, Accessed, Written;
        public uint Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct StandardInfo
    {
        public long AllocationSize, EndOfFile;
        public uint NumberOfLinks;
        public byte DeletePending, Directory;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out FileInformation info);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandleEx(SafeFileHandle handle, int kind, out StandardInfo info, uint size);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetDiskFreeSpaceW(string root, out uint sectors, out uint bytes, out uint free, out uint total);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
    private static extern SafeFileHandle CreateFileW(string path, uint access, uint share, nint security, uint creation, uint flags, nint template);
}
