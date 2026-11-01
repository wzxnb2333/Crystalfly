using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Crystalfly.Core.Instances;
using Crystalfly.Core.Models;

namespace Crystalfly.Core.Runtime;

/// <summary>Redirects the Unity player's shell-folder imports before its entry point runs.</summary>
public static class WindowsIsolatedGameLauncher
{
    public static async Task<Process> StartAsync(ProcessStartInfo startInfo, string savePath,
        string profilePath, GameId gameId, CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows() || !Environment.Is64BitProcess)
            throw new PlatformNotSupportedException("Isolated game launch requires 64-bit Windows.");
        InstanceDirectory.RejectReparseAncestors(startInfo.FileName);
        InstanceDirectory.RejectReparseAncestors(savePath);
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(savePath).ToUpperInvariant())));
        using var gate = new Mutex(false, @"Global\Crystalfly.Save." + key, out var created);
        if (!created || new InstanceProcessProbe(startInfo.FileName).IsRunning())
            throw new InvalidOperationException("This instance is already running.");
        cancellationToken.ThrowIfCancellationRequested();
        var profile = InstanceSaveProfile.Prepare(profilePath, savePath, gameId);
        startInfo.UseShellExecute = false;
        startInfo.Environment["USERPROFILE"] = profile;
        startInfo.Environment["LOCALAPPDATA"] = Path.Combine(profile, "AppData", "Local");
        startInfo.Environment["APPDATA"] = Path.Combine(profile, "AppData", "Roaming");
        // The debug API is thread-affine. Detach after patching; no debugger remains during play.
        return await Task.Factory.StartNew(() => StartCore(startInfo, profile, gate.SafeWaitHandle.DangerousGetHandle()), cancellationToken,
            TaskCreationOptions.LongRunning, TaskScheduler.Default).ConfigureAwait(false);
    }

    private static Process StartCore(ProcessStartInfo info, string profile, nint lease)
    {
        using var inherited = new InheritedLease(lease);
        var startup = new StartupInfoEx
        {
            Startup = new StartupInfo { Size = Marshal.SizeOf<StartupInfoEx>() },
            Attributes = inherited.Attributes
        };
        var command = new StringBuilder(Quote(info.FileName));
        foreach (var argument in info.ArgumentList) command.Append(' ').Append(Quote(argument));
        if (!string.IsNullOrWhiteSpace(info.Arguments)) throw new ArgumentException("Use ArgumentList for isolated launch.");
        var environment = string.Join('\0', info.Environment.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => pair.Key + "=" + pair.Value)) + "\0\0";
        if (!CreateProcessW(info.FileName, command, 0, 0, true, 0x80402, environment,
                info.WorkingDirectory, ref startup, out var created)) ThrowLastError();
        Process? process = null;
        var detached = false;
        try
        {
            process = Process.GetProcessById((int)created.ProcessId);
            var deadline = Stopwatch.StartNew();
            while (deadline.Elapsed < TimeSpan.FromSeconds(30))
            {
                if (!WaitForDebugEvent(out var debug, 1000))
                {
                    if (Marshal.GetLastWin32Error() == 121) continue;
                    ThrowLastError();
                }
                var status = 0x00010002u;
                if (debug.Code == 3 && debug.File != 0) CloseHandle(debug.File);
                if (debug.Code == 6 && debug.File != 0) CloseHandle(debug.File);
                if (debug.Code == 5) throw new IOException("The game exited before save isolation was established.");
                if (debug.Code == 1)
                {
                    if (debug.ExceptionCode == 0x80000003)
                    {
                        InstallFolderRedirects(process, profile);
                        if (!ContinueDebugEvent(debug.ProcessId, debug.ThreadId, status)) ThrowLastError();
                        if (!DebugActiveProcessStop(created.ProcessId)) ThrowLastError();
                        detached = true;
                        return process;
                    }
                    status = 0x80010001;
                }
                if (!ContinueDebugEvent(debug.ProcessId, debug.ThreadId, status)) ThrowLastError();
            }
            throw new TimeoutException("The game did not reach its isolated startup entry point.");
        }
        finally
        {
            if (!detached)
            {
                TerminateProcess(created.Process, 1);
                DebugActiveProcessStop(created.ProcessId);
                process?.Dispose();
            }
            CloseHandle(created.Thread);
            CloseHandle(created.Process);
        }
    }

    private static void InstallFolderRedirects(Process process, string profile)
    {
        var modules = process.Modules.Cast<ProcessModule>().ToArray();
        var allocator = RemoteExport(modules, "ole32.dll", "CoTaskMemAlloc");
        var handle = process.Handle;
        var redirects = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var function in new[] { "SHGetFolderPathW", "SHGetKnownFolderPath" })
            redirects[function] = AllocateCode(handle, BuildRedirect(function, profile,
                RemoteExport(modules, "shell32.dll", function), allocator));
        var shell = modules.Single(module => module.ModuleName.Equals("shell32.dll", StringComparison.OrdinalIgnoreCase));
        redirects["GetProcAddress"] = AllocateCode(handle, BuildExportRedirect(
            RemoteExport(modules, "kernel32.dll", "GetProcAddress"), shell.BaseAddress, redirects));
        var enginePatched = false;
        foreach (var module in modules)
        {
            // Keep Windows' own shell/COM implementation untouched.
            var gameRoot = Path.GetDirectoryName(process.MainModule!.FileName)! + Path.DirectorySeparatorChar;
            if (!module.FileName.StartsWith(gameRoot, StringComparison.OrdinalIgnoreCase)) continue;
            var image = module.BaseAddress;
            var dos = Read(handle, image, 64);
            if (BitConverter.ToUInt16(dos) != 0x5a4d) continue;
            var pe = Read(handle, image + BitConverter.ToInt32(dos, 60), 264);
            if (BitConverter.ToUInt16(pe, 24) != 0x20b)
                throw new PlatformNotSupportedException("This game's architecture does not support save isolation.");
            var importRva = BitConverter.ToInt32(pe, 144);
            if (importRva == 0) continue;
            for (var index = 0; index < 4096; index++)
            {
                var descriptor = Read(handle, image + importRva + index * 20, 20);
                var names = BitConverter.ToInt32(descriptor);
                var dllName = BitConverter.ToInt32(descriptor, 12);
                var thunks = BitConverter.ToInt32(descriptor, 16);
                if (dllName == 0) break;
                if (names == 0) continue;
                for (var entry = 0; entry < 65536; entry++)
                {
                    var name = BitConverter.ToUInt64(Read(handle, image + names + entry * 8, 8));
                    if (name == 0) break;
                    if ((name & 0x8000000000000000) != 0) continue;
                    var import = ReadAscii(handle, image + checked((nint)name) + 2);
                    if (!redirects.TryGetValue(import, out var remote)) continue;
                    var slot = image + thunks + entry * 8;
                    if (!VirtualProtectEx(handle, slot, 8, 0x04, out var old)) ThrowLastError();
                    try { Write(handle, slot, BitConverter.GetBytes(remote)); }
                    finally { if (!VirtualProtectEx(handle, slot, 8, old, out _)) ThrowLastError(); }
                    if (import != "GetProcAddress" && (module.ModuleName.Equals("UnityPlayer.dll", StringComparison.OrdinalIgnoreCase)
                        || module.BaseAddress == process.MainModule?.BaseAddress))
                        enginePatched = true;
                }
            }
        }
        if (!enginePatched) throw new NotSupportedException("No supported Unity save-path imports were found. Launch was stopped to protect saves.");
    }

    private static long AllocateCode(nint process, byte[] code)
    {
        var remote = VirtualAllocEx(process, 0, (nuint)code.Length, 0x3000, 0x04);
        if (remote == 0) ThrowLastError();
        Write(process, remote, code);
        if (!VirtualProtectEx(process, remote, (nuint)code.Length, 0x20, out _)) ThrowLastError();
        if (!FlushInstructionCache(process, remote, (nuint)code.Length)) ThrowLastError();
        return remote;
    }

    internal static byte[] BuildExportRedirect(long original, long shellModule, IReadOnlyDictionary<string, long> redirects)
    {
        var code = new List<byte>();
        void Emit(params byte[] bytes) => code.AddRange(bytes);
        void Long(long value) => code.AddRange(BitConverter.GetBytes(value));
        int Jump(byte condition) { Emit(0x0f, condition); var at = code.Count; code.AddRange(new byte[4]); return at; }
        void Fix(int at) { var bytes = BitConverter.GetBytes(code.Count - at - 4); for (var i = 0; i < 4; i++) code[at + i] = bytes[i]; }
        Emit(0x48, 0xb8); Long(shellModule); Emit(0x48, 0x39, 0xc1); var wrongModule = Jump(0x85);
        Emit(0x48, 0x81, 0xfa, 0, 0, 1, 0); var ordinal = Jump(0x82);
        foreach (var (name, target) in redirects)
        {
            var skips = new List<int>();
            var bytes = Encoding.ASCII.GetBytes(name + "\0");
            for (var i = 0; i < bytes.Length; i++)
            {
                Emit(0x80, 0x7a, checked((byte)i), bytes[i]); skips.Add(Jump(0x85));
            }
            Emit(0x48, 0xb8); Long(target); Emit(0xc3);
            foreach (var skip in skips) Fix(skip);
        }
        Fix(wrongModule); Fix(ordinal);
        Emit(0x48, 0xb8); Long(original); Emit(0xff, 0xe0);
        return code.ToArray();
    }

    // Windows x64 ABI: leaf copies for CSIDL; COM-owned buffers for KNOWNFOLDERID.
    internal static byte[] BuildRedirect(string import, string profile, long original, long allocator)
    {
        var code = new List<byte>();
        var paths = new[] { Path.Combine(profile, "AppData", "LocalLow"),
            Path.Combine(profile, "AppData", "Local"), Path.Combine(profile, "AppData", "Roaming"), profile };
        var ids = new[] { "A520A1A4-1780-4FF6-BD18-167343C5AF16", "F1B32785-6FBA-4FCF-9D55-7B8E7F157091",
            "3EB685DB-65F9-4CF6-A03A-E3EF65729F3D", "5E6C858F-0E22-4760-9AFE-EA3317B67173" };
        var csidls = new[] { -1, 0x1c, 0x1a, 0x28 };
        void Emit(params byte[] bytes) => code.AddRange(bytes);
        void Int64(long value) => code.AddRange(BitConverter.GetBytes(value));
        void Int32(int value) => code.AddRange(BitConverter.GetBytes(value));
        int Jump(byte condition) { Emit(0x0f, condition); var at = code.Count; Int32(0); return at; }
        void Fix(int at, int target) { var bytes = BitConverter.GetBytes(target - at - 4); for (var i = 0; i < 4; i++) code[at + i] = bytes[i]; }
        for (var i = import == "SHGetFolderPathW" ? 1 : 0; i < paths.Length; i++)
        {
            var skips = new List<int>();
            if (import == "SHGetKnownFolderPath")
            {
                var guid = Guid.Parse(ids[i]).ToByteArray();
                Emit(0x48, 0xb8); Int64(BitConverter.ToInt64(guid)); Emit(0x48, 0x39, 0x01); skips.Add(Jump(0x85));
                Emit(0x48, 0xb8); Int64(BitConverter.ToInt64(guid, 8)); Emit(0x48, 0x39, 0x41, 0x08); skips.Add(Jump(0x85));
            }
            else
            {
                Emit(0x89, 0xd0, 0x25); Int32(0xff); Emit(0x3d); Int32(csidls[i]); skips.Add(Jump(0x85));
            }
            var text = Encoding.Unicode.GetBytes(paths[i] + "\0");
            if (paths[i].Length >= 260) throw new PathTooLongException("The isolated profile must fit the Windows shell-folder path limit.");
            int oom = -1;
            if (import == "SHGetKnownFolderPath")
            {
                Emit(0x53, 0x48, 0x83, 0xec, 0x20, 0x4c, 0x89, 0xcb, 0xb9); Int32(text.Length);
                Emit(0x48, 0xb8); Int64(allocator); Emit(0xff, 0xd0, 0x48, 0x85, 0xc0); oom = Jump(0x84);
                Emit(0x48, 0x89, 0x03, 0x56, 0x57, 0x48, 0x89, 0xc7);
            }
            else Emit(0x56, 0x57, 0x48, 0x8b, 0x7c, 0x24, 0x38);
            Emit(0x48, 0x8d, 0x35); var textOffset = code.Count; Int32(0);
            Emit(0xb9); Int32(text.Length / 2); Emit(0xfc, 0x66, 0xf3, 0xa5, 0x5f, 0x5e, 0x31, 0xc0);
            if (import == "SHGetKnownFolderPath") Emit(0x48, 0x83, 0xc4, 0x20, 0x5b);
            Emit(0xc3);
            if (oom >= 0)
            {
                Fix(oom, code.Count); Emit(0x48, 0xc7, 0x03, 0, 0, 0, 0, 0xb8); Int32(unchecked((int)0x8007000e));
                Emit(0x48, 0x83, 0xc4, 0x20, 0x5b, 0xc3);
            }
            Fix(textOffset, code.Count); code.AddRange(text);
            foreach (var skip in skips) Fix(skip, code.Count);
        }
        Emit(0x48, 0xb8); Int64(original); Emit(0xff, 0xe0);
        return code.ToArray();
    }

    private static long RemoteExport(ProcessModule[] modules, string library, string export)
    {
        var local = NativeLibrary.Load(library);
        try
        {
            var address = NativeLibrary.GetExport(local, export);
            if (!GetModuleHandleExW(6, address, out var owner)) ThrowLastError();
            var path = new StringBuilder(32768);
            if (GetModuleFileNameW(owner, path, path.Capacity) == 0) ThrowLastError();
            var remote = modules.SingleOrDefault(module => string.Equals(module.FileName, path.ToString(), StringComparison.OrdinalIgnoreCase))
                ?? throw new NotSupportedException("The game's COM allocator is not loaded.");
            return (long)remote.BaseAddress + ((long)address - (long)owner);
        }
        finally { NativeLibrary.Free(local); }
    }

    private static byte[] Read(nint process, nint address, int length)
    {
        var data = new byte[length];
        if (!ReadProcessMemory(process, address, data, (nuint)length, out var count) || count != (nuint)length) ThrowLastError();
        return data;
    }
    private static string ReadAscii(nint process, nint address)
    {
        var bytes = new List<byte>();
        for (var i = 0; i < 256; i++) { var value = Read(process, address + i, 1)[0]; if (value == 0) return Encoding.ASCII.GetString(bytes.ToArray()); bytes.Add(value); }
        throw new InvalidDataException("Invalid game import name.");
    }
    private static void Write(nint process, nint address, byte[] data)
    {
        if (!WriteProcessMemory(process, address, data, (nuint)data.Length, out var count) || count != (nuint)data.Length) ThrowLastError();
    }
    private static string Quote(string argument)
    {
        var result = new StringBuilder("\"");
        var slashes = 0;
        foreach (var character in argument)
        {
            if (character == '\\') { slashes++; continue; }
            result.Append('\\', character == '"' ? slashes * 2 + 1 : slashes);
            result.Append(character);
            slashes = 0;
        }
        return result.Append('\\', slashes * 2).Append('"').ToString();
    }
    private static void ThrowLastError() => throw new Win32Exception(Marshal.GetLastWin32Error());

    // The child owns a handle to the save lease, even if the launcher crashes or is closed.
    private sealed class InheritedLease : IDisposable
    {
        public nint Attributes { get; private set; }
        private nint handle;
        private nint handles;
        private bool initialized;
        public InheritedLease(nint source)
        {
            try
            {
                var current = GetCurrentProcess();
                if (!DuplicateHandle(current, source, current, out handle, 0, true, 2)) ThrowLastError();
                nuint size = 0;
                InitializeProcThreadAttributeList(0, 1, 0, ref size);
                Attributes = Marshal.AllocHGlobal(checked((int)size));
                if (!InitializeProcThreadAttributeList(Attributes, 1, 0, ref size)) ThrowLastError();
                initialized = true;
                handles = Marshal.AllocHGlobal(nint.Size);
                Marshal.WriteIntPtr(handles, handle);
                if (!UpdateProcThreadAttribute(Attributes, 0, 0x20002, handles, (nuint)nint.Size, 0, 0)) ThrowLastError();
            }
            catch { Dispose(); throw; }
        }
        public void Dispose()
        {
            if (Attributes != 0)
            {
                if (initialized) DeleteProcThreadAttributeList(Attributes);
                Marshal.FreeHGlobal(Attributes);
                Attributes = 0;
            }
            if (handles != 0) { Marshal.FreeHGlobal(handles); handles = 0; }
            if (handle != 0) { CloseHandle(handle); handle = 0; }
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        public int Size; public nint Reserved, Desktop, Title;
        public uint X, Y, XSize, YSize, XCount, YCount, Fill, Flags;
        public ushort Show, ReservedSize; public nint ReservedData, Input, Output, Error;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInfo { public nint Process, Thread; public uint ProcessId, ThreadId; }
    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInfoEx { public StartupInfo Startup; public nint Attributes; }
    [StructLayout(LayoutKind.Explicit, Size = 176)]
    private struct DebugEvent
    {
        [FieldOffset(0)] public uint Code;
        [FieldOffset(4)] public uint ProcessId;
        [FieldOffset(8)] public uint ThreadId;
        [FieldOffset(16)] public nint File;
        [FieldOffset(16)] public uint ExceptionCode;
    }
    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateProcessW(string app, StringBuilder command, nint processSecurity, nint threadSecurity,
        [MarshalAs(UnmanagedType.Bool)] bool inherit, uint flags, string environment, string directory, ref StartupInfoEx startup, out ProcessInfo process);
    [DllImport("kernel32")] private static extern nint GetCurrentProcess();
    [DllImport("kernel32", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DuplicateHandle(nint sourceProcess, nint source, nint targetProcess, out nint target, uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint options);
    [DllImport("kernel32", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool InitializeProcThreadAttributeList(nint list, int count, uint flags, ref nuint size);
    [DllImport("kernel32", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool UpdateProcThreadAttribute(nint list, uint flags, nuint attribute, nint value, nuint size, nint previous, nint returned);
    [DllImport("kernel32")] private static extern void DeleteProcThreadAttributeList(nint list);
    [DllImport("kernel32", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool WaitForDebugEvent(out DebugEvent debug, uint timeout);
    [DllImport("kernel32", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool ContinueDebugEvent(uint process, uint thread, uint status);
    [DllImport("kernel32", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DebugActiveProcessStop(uint process);
    [DllImport("kernel32", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool TerminateProcess(nint process, uint code);
    [DllImport("kernel32")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CloseHandle(nint handle);
    [DllImport("kernel32", SetLastError = true)] private static extern nint VirtualAllocEx(nint process, nint address, nuint size, uint allocation, uint protection);
    [DllImport("kernel32", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool VirtualProtectEx(nint process, nint address, nuint size, uint protection, out uint previous);
    [DllImport("kernel32", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool ReadProcessMemory(nint process, nint address, [Out] byte[] data, nuint size, out nuint read);
    [DllImport("kernel32", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool WriteProcessMemory(nint process, nint address, byte[] data, nuint size, out nuint written);
    [DllImport("kernel32", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool FlushInstructionCache(nint process, nint address, nuint size);
    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetModuleHandleExW(uint flags, nint address, out nint module);
    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)] private static extern uint GetModuleFileNameW(nint module, StringBuilder path, int size);
}

public sealed class InstanceProcessProbe(string executablePath) : IHollowKnightProcessProbe
{
    public bool IsRunning()
    {
        var target = Path.GetFullPath(executablePath);
        var processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(target));
        try
        {
            foreach (var process in processes)
            {
                try { if (string.Equals(process.MainModule?.FileName, target, StringComparison.OrdinalIgnoreCase)) return true; }
                catch (InvalidOperationException) { }
                catch (Win32Exception) { return true; }
            }
            return false;
        }
        finally { foreach (var process in processes) process.Dispose(); }
    }
}
