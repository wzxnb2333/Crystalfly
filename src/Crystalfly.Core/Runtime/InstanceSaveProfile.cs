using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Crystalfly.Core.Instances;
using Crystalfly.Core.LocalLow;
using Crystalfly.Core.Models;
using Microsoft.Win32.SafeHandles;

namespace Crystalfly.Core.Runtime;

internal static class InstanceSaveProfile
{
    public static string Prepare(string profilePath, string savePath, GameId gameId)
    {
        profilePath = Path.GetFullPath(profilePath);
        savePath = Path.GetFullPath(savePath);
        InstanceDirectory.RejectReparseAncestors(profilePath);
        InstanceDirectory.RejectReparseAncestors(savePath);
        if (!Directory.Exists(savePath)) throw new DirectoryNotFoundException(savePath);
        _ = LocalLowDirectory.EnumerateRelativeFiles(savePath);
        var parent = Path.Combine(profilePath, "AppData", "LocalLow", "Team Cherry");
        InstanceDirectory.RejectReparseAncestors(parent);
        Directory.CreateDirectory(parent);
        Directory.CreateDirectory(Path.Combine(profilePath, "AppData", "Local"));
        Directory.CreateDirectory(Path.Combine(profilePath, "AppData", "Roaming"));
        var link = Path.Combine(parent, GameDefinition.Get(gameId).LocalLowDirectoryName);
        var info = new DirectoryInfo(link);
        if (info.Exists || info.LinkTarget is not null)
        {
            if (info.LinkTarget is null || !string.Equals(info.ResolveLinkTarget(true)?.FullName,
                    savePath, StringComparison.OrdinalIgnoreCase))
                throw new IOException("The instance save mapping points to a different directory.");
        }
        else
        {
            Directory.CreateDirectory(link);
            try { CreateJunction(link, savePath); }
            catch { Directory.Delete(link); throw; }
        }
        return profilePath;
    }

    private static void CreateJunction(string link, string target)
    {
        var substitute = Encoding.Unicode.GetBytes(@"\??\" + target);
        var display = Encoding.Unicode.GetBytes(target);
        var data = new byte[16 + substitute.Length + 2 + display.Length + 2];
        BitConverter.GetBytes(0xa0000003u).CopyTo(data, 0);
        BitConverter.GetBytes(checked((ushort)(data.Length - 8))).CopyTo(data, 4);
        BitConverter.GetBytes(checked((ushort)substitute.Length)).CopyTo(data, 10);
        BitConverter.GetBytes(checked((ushort)(substitute.Length + 2))).CopyTo(data, 12);
        BitConverter.GetBytes(checked((ushort)display.Length)).CopyTo(data, 14);
        substitute.CopyTo(data, 16);
        display.CopyTo(data, 18 + substitute.Length);
        using var handle = CreateFileW(link, 0x40000000, 0, 0, 3, 0x02200000, 0);
        if (handle.IsInvalid || !DeviceIoControl(handle, 0x000900a4, data, data.Length, 0, 0, out _, 0))
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(string name, uint access, uint share, nint security,
        uint creation, uint flags, nint template);
    [DllImport("kernel32", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(SafeFileHandle file, uint code, byte[] input, int size,
        nint output, int outputSize, out int returned, nint overlapped);
}
