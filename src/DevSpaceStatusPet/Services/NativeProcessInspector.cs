using System.ComponentModel;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;

namespace DevSpaceStatusPet.Services;

public sealed record ProcessEntry(
    int ProcessId,
    int ParentProcessId,
    string Name,
    string? ExecutablePath,
    DateTimeOffset? StartedAt,
    TimeSpan CpuTime);

public sealed record ProcessGroup(int RootProcessId, IReadOnlyList<ProcessEntry> Processes);

public sealed class NativeProcessInspector
{
    private const uint Th32CsSnapProcess = 0x00000002;
    private const uint ProcessQueryLimitedInformation = 0x00001000;
    private const int DefaultProcessPathCharacters = 1024;
    private const int MaximumProcessPathCharacters = 32768;
    private static readonly IntPtr InvalidHandleValue = new(-1);

    public int? FindListeningProcessId(int port)
    {
        var size = 0;
        var result = GetExtendedTcpTable(
            IntPtr.Zero,
            ref size,
            true,
            2,
            TcpTableClass.TcpTableOwnerPidListener,
            0);

        if (result != 0 && result != 122)
        {
            return null;
        }

        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            result = GetExtendedTcpTable(
                buffer,
                ref size,
                true,
                2,
                TcpTableClass.TcpTableOwnerPidListener,
                0);
            if (result != 0)
            {
                return null;
            }

            var count = Marshal.ReadInt32(buffer);
            var rowPointer = IntPtr.Add(buffer, sizeof(int));
            var rowSize = Marshal.SizeOf<MibTcpRowOwnerPid>();
            for (var index = 0; index < count; index++)
            {
                var row = Marshal.PtrToStructure<MibTcpRowOwnerPid>(rowPointer);
                var rowPort = unchecked((ushort)IPAddress.NetworkToHostOrder((short)row.LocalPort));
                if (rowPort == port)
                {
                    return unchecked((int)row.OwningPid);
                }

                rowPointer = IntPtr.Add(rowPointer, rowSize);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }

        return null;
    }

    public IReadOnlyList<ProcessGroup> GetDescendantGroups(int serverProcessId)
    {
        var entries = SnapshotProcesses();
        var entriesById = entries.ToDictionary(entry => entry.ProcessId);
        var childrenByParent = entries
            .GroupBy(entry => entry.ParentProcessId)
            .ToDictionary(group => group.Key, group => group.ToArray());

        if (!childrenByParent.TryGetValue(serverProcessId, out var roots))
        {
            return Array.Empty<ProcessGroup>();
        }

        var groups = new List<ProcessGroup>();
        foreach (var root in roots)
        {
            var processes = new List<ProcessEntry>();
            var queue = new Queue<int>();
            var visited = new HashSet<int>();
            queue.Enqueue(root.ProcessId);

            while (queue.Count > 0)
            {
                var processId = queue.Dequeue();
                if (!visited.Add(processId))
                {
                    continue;
                }

                if (entriesById.TryGetValue(processId, out var entry) &&
                    entry.ProcessId != Environment.ProcessId &&
                    !entry.Name.Equals("DevSpaceStatusPet", StringComparison.OrdinalIgnoreCase))
                {
                    processes.Add(ReadProcessDetails(entry));
                }

                if (!childrenByParent.TryGetValue(processId, out var children))
                {
                    continue;
                }

                foreach (var child in children)
                {
                    queue.Enqueue(child.ProcessId);
                }
            }

            var meaningful = processes
                .Where(entry => !entry.Name.Equals("conhost", StringComparison.OrdinalIgnoreCase) &&
                                !entry.Name.Equals("OpenConsole", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (meaningful.Length > 0)
            {
                groups.Add(new ProcessGroup(root.ProcessId, meaningful));
            }
        }

        return groups;
    }

    private static ProcessEntry ReadProcessDetails(NativeProcessEntry entry)
    {
        string? path = null;
        DateTimeOffset? startedAt = null;
        var cpu = TimeSpan.Zero;

        var processHandle = OpenProcess(
            ProcessQueryLimitedInformation,
            false,
            unchecked((uint)entry.ProcessId));
        if (processHandle != IntPtr.Zero)
        {
            try
            {
                path = TryGetProcessPath(processHandle);

                if (GetProcessTimes(
                        processHandle,
                        out var creationTime,
                        out _,
                        out var kernelTime,
                        out var userTime))
                {
                    var creationFileTime = ToUInt64(creationTime);
                    if (creationFileTime is > 0 and <= long.MaxValue)
                    {
                        startedAt = new DateTimeOffset(
                            DateTime.FromFileTimeUtc((long)creationFileTime));
                    }

                    var totalCpuTicks = ToUInt64(kernelTime) + ToUInt64(userTime);
                    cpu = TimeSpan.FromTicks((long)Math.Min(totalCpuTicks, (ulong)long.MaxValue));
                }
            }
            finally
            {
                _ = CloseHandle(processHandle);
            }
        }

        return new ProcessEntry(
            entry.ProcessId,
            entry.ParentProcessId,
            entry.Name,
            path,
            startedAt,
            cpu);
    }

    private static string? TryGetProcessPath(IntPtr processHandle)
    {
        var capacity = DefaultProcessPathCharacters;
        while (capacity <= MaximumProcessPathCharacters)
        {
            var pathBuilder = new StringBuilder(capacity);
            var length = pathBuilder.Capacity;
            if (QueryFullProcessImageName(processHandle, 0, pathBuilder, ref length))
            {
                return pathBuilder.ToString();
            }

            if (Marshal.GetLastWin32Error() != 122 || capacity == MaximumProcessPathCharacters)
            {
                return null;
            }
            capacity = MaximumProcessPathCharacters;
        }

        return null;
    }

    private static IReadOnlyList<NativeProcessEntry> SnapshotProcesses()
    {
        var snapshot = CreateToolhelp32Snapshot(Th32CsSnapProcess, 0);
        if (snapshot == InvalidHandleValue)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        try
        {
            var native = new ProcessEntry32 { Size = (uint)Marshal.SizeOf<ProcessEntry32>() };
            if (!Process32First(snapshot, ref native))
            {
                return Array.Empty<NativeProcessEntry>();
            }

            var result = new List<NativeProcessEntry>();
            do
            {
                result.Add(new NativeProcessEntry(
                    unchecked((int)native.ProcessId),
                    unchecked((int)native.ParentProcessId),
                    Path.GetFileNameWithoutExtension(native.ExecutableFile ?? string.Empty)));
            }
            while (Process32Next(snapshot, ref native));

            return result;
        }
        finally
        {
            _ = CloseHandle(snapshot);
        }
    }

    private static ulong ToUInt64(FileTime fileTime) =>
        ((ulong)fileTime.HighDateTime << 32) | fileTime.LowDateTime;

    private readonly record struct NativeProcessEntry(
        int ProcessId,
        int ParentProcessId,
        string Name);

    private enum TcpTableClass
    {
        TcpTableBasicListener,
        TcpTableBasicConnections,
        TcpTableBasicAll,
        TcpTableOwnerPidListener,
        TcpTableOwnerPidConnections,
        TcpTableOwnerPidAll
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MibTcpRowOwnerPid
    {
        public uint State;
        public uint LocalAddress;
        public uint LocalPort;
        public uint RemoteAddress;
        public uint RemotePort;
        public uint OwningPid;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProcessEntry32
    {
        public uint Size;
        public uint Usage;
        public uint ProcessId;
        public IntPtr DefaultHeapId;
        public uint ModuleId;
        public uint Threads;
        public uint ParentProcessId;
        public int PriorityClassBase;
        public uint Flags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string? ExecutableFile;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct FileTime
    {
        public readonly uint LowDateTime;
        public readonly uint HighDateTime;
    }

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedTcpTable(
        IntPtr tcpTable,
        ref int size,
        bool order,
        int ipVersion,
        TcpTableClass tableClass,
        uint reserved);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);

    [DllImport("kernel32.dll", EntryPoint = "Process32FirstW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool Process32First(IntPtr snapshot, ref ProcessEntry32 entry);

    [DllImport("kernel32.dll", EntryPoint = "Process32NextW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool Process32Next(IntPtr snapshot, ref ProcessEntry32 entry);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(
        uint desiredAccess,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandle,
        uint processId);

    [DllImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageName(
        IntPtr processHandle,
        uint flags,
        StringBuilder executablePath,
        ref int size);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessTimes(
        IntPtr processHandle,
        out FileTime creationTime,
        out FileTime exitTime,
        out FileTime kernelTime,
        out FileTime userTime);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}
