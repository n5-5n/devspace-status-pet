using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;

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
            queue.Enqueue(root.ProcessId);

            while (queue.Count > 0)
            {
                var processId = queue.Dequeue();
                var entry = entries.FirstOrDefault(candidate => candidate.ProcessId == processId);
                if (entry is not null && entry.ProcessId != Environment.ProcessId &&
                    !entry.Name.Equals("DevSpaceStatusPet", StringComparison.OrdinalIgnoreCase))
                {
                    processes.Add(entry);
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

    private static IReadOnlyList<ProcessEntry> SnapshotProcesses()
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
                return Array.Empty<ProcessEntry>();
            }

            var result = new List<ProcessEntry>();
            do
            {
                var name = Path.GetFileNameWithoutExtension(native.ExecutableFile ?? string.Empty);
                string? path = null;
                DateTimeOffset? startedAt = null;
                var cpu = TimeSpan.Zero;

                try
                {
                    using var process = Process.GetProcessById(unchecked((int)native.ProcessId));
                    try { path = process.MainModule?.FileName; } catch { }
                    try { startedAt = process.StartTime; } catch { }
                    try { cpu = process.TotalProcessorTime; } catch { }
                }
                catch
                {
                    // The process may have exited between the native snapshot and managed lookup.
                }

                result.Add(new ProcessEntry(
                    unchecked((int)native.ProcessId),
                    unchecked((int)native.ParentProcessId),
                    name,
                    path,
                    startedAt,
                    cpu));
            }
            while (Process32Next(snapshot, ref native));

            return result;
        }
        finally
        {
            CloseHandle(snapshot);
        }
    }

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

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
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

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool Process32First(IntPtr snapshot, ref ProcessEntry32 entry);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool Process32Next(IntPtr snapshot, ref ProcessEntry32 entry);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);
}
