using System.Runtime.InteropServices;
using Crystalfly.Core.Models;
using Crystalfly.Core.Runtime;

namespace Crystalfly.Core.Tests.Runtime;

public sealed class IsolatedGameLaunchTests
{
    [Fact]
    public void Dynamic_folder_lookup_is_redirected_but_other_exports_are_preserved()
    {
        if (!OperatingSystem.IsWindows() || !Environment.Is64BitProcess) return;
        var shell = NativeLibrary.Load("shell32.dll");
        var kernel = NativeLibrary.Load("kernel32.dll");
        nint code = 0;
        try
        {
            var original = NativeLibrary.GetExport(kernel, "GetProcAddress");
            var bytes = WindowsIsolatedGameLauncher.BuildExportRedirect(original, shell,
                new Dictionary<string, long> { ["SHGetKnownFolderPath"] = 0x12345678, ["SHGetFolderPathW"] = 0x22345678 });
            code = VirtualAlloc(0, (nuint)bytes.Length, 0x3000, 4);
            Marshal.Copy(bytes, 0, code, bytes.Length);
            Assert.True(VirtualProtect(code, (nuint)bytes.Length, 0x20, out _));
            var lookup = Marshal.GetDelegateForFunctionPointer<ExportLookup>(code);
            Assert.Equal((nint)0x12345678, lookup(shell, "SHGetKnownFolderPath"));
            Assert.Equal((nint)0x22345678, lookup(shell, "SHGetFolderPathW"));
            Assert.Equal(NativeLibrary.GetExport(shell, "SHGetFolderPathA"), lookup(shell, "SHGetFolderPathA"));
            Assert.Equal(NativeLibrary.GetExport(kernel, "GetCurrentProcessId"), lookup(kernel, "GetCurrentProcessId"));
            Assert.Equal(0, lookup(kernel, "SHGetKnownFolderPath"));
            Assert.Equal(0, lookup(shell, "a"));
        }
        finally
        {
            if (code != 0) VirtualFree(code, 0, 0x8000);
            NativeLibrary.Free(shell); NativeLibrary.Free(kernel);
        }
    }

    [Fact]
    public void Folder_redirects_use_private_paths_and_preserve_unrelated_folder_queries()
    {
        if (!OperatingSystem.IsWindows() || !Environment.Is64BitProcess) return;
        var profile = Path.Combine(Path.GetTempPath(), "Crystalfly 隔离路径");
        var shell = NativeLibrary.Load("shell32.dll");
        var ole = NativeLibrary.Load("ole32.dll");
        try
        {
            var alloc = NativeLibrary.GetExport(ole, "CoTaskMemAlloc");
            foreach (var import in new[] { "SHGetFolderPathW", "SHGetKnownFolderPath" })
            {
                var original = NativeLibrary.GetExport(shell, import);
                var bytes = WindowsIsolatedGameLauncher.BuildRedirect(import, profile, original, alloc);
                var code = VirtualAlloc(0, (nuint)bytes.Length, 0x3000, 0x04);
                Assert.NotEqual(0, code);
                try
                {
                    Marshal.Copy(bytes, 0, code, bytes.Length);
                    Assert.True(VirtualProtect(code, (nuint)bytes.Length, 0x20, out _));
                    if (import == "SHGetFolderPathW")
                    {
                        var hook = Marshal.GetDelegateForFunctionPointer<FolderPath>(code);
                        var fallback = Marshal.GetDelegateForFunctionPointer<FolderPath>(original);
                        foreach (var (id, suffix) in new[] { (0x1c, "AppData\\Local"), (0x1a, "AppData\\Roaming"), (0x28, "") })
                        {
                            var buffer = new char[260];
                            Assert.Equal(0, hook(0, id | 0x8000, 0, 0, buffer));
                            Assert.Equal(Path.TrimEndingDirectorySeparator(Path.Combine(profile, suffix)), new string(buffer).TrimEnd('\0'));
                        }
                        var actual = new char[260]; var expected = new char[260];
                        Assert.Equal(fallback(0, 0x24, 0, 0, expected), hook(0, 0x24, 0, 0, actual));
                        Assert.Equal(expected, actual);
                    }
                    else
                    {
                        var hook = Marshal.GetDelegateForFunctionPointer<KnownFolder>(code);
                        var fallback = Marshal.GetDelegateForFunctionPointer<KnownFolder>(original);
                        foreach (var (id, suffix) in new[] {
                            ("A520A1A4-1780-4FF6-BD18-167343C5AF16", "AppData\\LocalLow"),
                            ("F1B32785-6FBA-4FCF-9D55-7B8E7F157091", "AppData\\Local"),
                            ("3EB685DB-65F9-4CF6-A03A-E3EF65729F3D", "AppData\\Roaming"),
                            ("5E6C858F-0E22-4760-9AFE-EA3317B67173", "") })
                        {
                            var guid = Guid.Parse(id);
                            Assert.Equal(0, hook(ref guid, 0, 0, out var path));
                            try { Assert.Equal(Path.TrimEndingDirectorySeparator(Path.Combine(profile, suffix)), Marshal.PtrToStringUni(path)); }
                            finally { Marshal.FreeCoTaskMem(path); }
                        }
                        var windows = Guid.Parse("F38BF404-1D43-42F2-9305-67DE0B28FC23");
                        Assert.Equal(fallback(ref windows, 0, 0, out var expected), hook(ref windows, 0, 0, out var actual));
                        try { Assert.Equal(Marshal.PtrToStringUni(expected), Marshal.PtrToStringUni(actual)); }
                        finally { Marshal.FreeCoTaskMem(expected); Marshal.FreeCoTaskMem(actual); }
                    }
                }
                finally { VirtualFree(code, 0, 0x8000); }
            }
        }
        finally { NativeLibrary.Free(shell); NativeLibrary.Free(ole); }
    }

    [Theory]
    [InlineData(GameId.HollowKnight)]
    [InlineData(GameId.Silksong)]
    public void Profiles_write_directly_to_their_own_saves_without_writeback(GameId game)
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = Path.Combine(Path.GetTempPath(), "Crystalfly.Tests", Guid.NewGuid().ToString("N"));
        var links = new List<string>();
        Directory.CreateDirectory(root);
        try
        {
            for (var i = 0; i < 2; i++)
            {
                var save = Path.Combine(root, "save" + i);
                Directory.CreateDirectory(save);
                var profile = Path.Combine(root, "profile" + i);
                InstanceSaveProfile.Prepare(profile, save, game);
                var link = Path.Combine(profile, "AppData", "LocalLow", "Team Cherry", GameDefinition.Get(game).LocalLowDirectoryName);
                links.Add(link);
                File.WriteAllText(Path.Combine(link, "user1.dat"), "instance" + i);
                Assert.Equal("instance" + i, File.ReadAllText(Path.Combine(save, "user1.dat")));
                InstanceSaveProfile.Prepare(profile, save, game);
                Assert.Throws<IOException>(() => InstanceSaveProfile.Prepare(profile, root, game));
            }
            Assert.Equal("instance0", File.ReadAllText(Path.Combine(root, "save0", "user1.dat")));
            Assert.Equal("instance1", File.ReadAllText(Path.Combine(root, "save1", "user1.dat")));
        }
        finally
        {
            foreach (var link in links) Directory.Delete(link);
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void A_save_tree_containing_a_link_to_another_directory_is_rejected()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = Path.Combine(Path.GetTempPath(), "Crystalfly.Tests", Guid.NewGuid().ToString("N"));
        var save = Path.Combine(root, "save");
        var outside = Path.Combine(root, "outside");
        Directory.CreateDirectory(save);
        Directory.CreateDirectory(outside);
        var link = Path.Combine(save, "linked");
        try
        {
            Directory.CreateSymbolicLink(link, outside);
            Assert.Throws<IOException>(() => InstanceSaveProfile.Prepare(Path.Combine(root, "profile"), save, GameId.HollowKnight));
            Assert.False(Directory.Exists(Path.Combine(root, "profile")));
        }
        finally
        {
            if (new DirectoryInfo(link).LinkTarget is not null) Directory.Delete(link);
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Unsupported_player_is_stopped_before_entry_and_does_not_fall_back_to_shared_saves()
    {
        if (!OperatingSystem.IsWindows() || !Environment.Is64BitProcess) return;
        var root = Path.Combine(Path.GetTempPath(), "Crystalfly.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var executable = Path.Combine(root, "probe.exe");
        File.Copy(Path.Combine(Environment.SystemDirectory, "where.exe"), executable);
        var save = Path.Combine(root, "save");
        Directory.CreateDirectory(save);
        File.WriteAllText(Path.Combine(save, "user1.dat"), "unchanged");
        try
        {
            await Assert.ThrowsAnyAsync<NotSupportedException>(() => WindowsIsolatedGameLauncher.StartAsync(
                new System.Diagnostics.ProcessStartInfo(executable) { WorkingDirectory = root }, save,
                Path.Combine(root, "profile"), GameId.HollowKnight));
            Assert.False(new InstanceProcessProbe(executable).IsRunning());
            Assert.Equal("unchanged", File.ReadAllText(Path.Combine(save, "user1.dat")));
        }
        finally
        {
            var link = Path.Combine(root, "profile", "AppData", "LocalLow", "Team Cherry", "Hollow Knight");
            if (new DirectoryInfo(link).LinkTarget is not null) Directory.Delete(link);
            Directory.Delete(root, true);
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi, CharSet = CharSet.Unicode)]
    private delegate int FolderPath(nint window, int folder, nint token, uint flags, [Out] char[] buffer);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int KnownFolder(ref Guid folder, uint flags, nint token, out nint path);
    [UnmanagedFunctionPointer(CallingConvention.Winapi, CharSet = CharSet.Ansi)]
    private delegate nint ExportLookup(nint module, string name);
    [DllImport("kernel32")] private static extern nint VirtualAlloc(nint address, nuint size, uint allocation, uint protection);
    [DllImport("kernel32")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool VirtualProtect(nint address, nuint size, uint protection, out uint old);
    [DllImport("kernel32")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool VirtualFree(nint address, nuint size, uint type);
}
