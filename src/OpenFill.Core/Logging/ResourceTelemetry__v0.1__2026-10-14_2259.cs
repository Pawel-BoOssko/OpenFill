// OpenFill - Metadata: wersja 0.1, data 2026-10-14 22:59
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using OpenFill.Core.Config;

namespace OpenFill.Core.Logging;

public sealed record ResourceActivity(int ActiveTasks, int WaitingTasks, int Tabs);

/// <summary>
/// Low-overhead process-tree telemetry for diagnosing long-running OpenFill RAM/CPU usage.
/// It samples OpenFill.exe and only its actual descendant processes, including WebView2 children.
/// CPU is reported in two explicit scales:
/// cpuPctMachine = share of total logical-CPU capacity, 0..100;
/// cpuPctCoreSum = one-core scale, where one fully busy logical CPU is 100 and N CPUs can reach N*100.
/// </summary>
public sealed class ResourceTelemetry : IDisposable
{
    private const uint Th32csSnapProcess = 0x00000002;
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct ProcessEntry32
    {
        public uint dwSize;
        public uint cntUsage;
        public uint th32ProcessID;
        public IntPtr th32DefaultHeapID;
        public uint th32ModuleID;
        public uint cntThreads;
        public uint th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szExeFile;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);
    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    private static extern bool Process32First(IntPtr snapshot, ref ProcessEntry32 entry);
    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    private static extern bool Process32Next(IntPtr snapshot, ref ProcessEntry32 entry);
    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);

    private sealed record Previous(long StartTicks, double CpuMs, DateTime SampledUtc);

    private readonly AppPaths _paths;
    private readonly AppConfig _config;
    private readonly Func<ResourceActivity>? _activity;
    private readonly int _rootPid;
    private readonly DateTime _rootStartedUtc;
    private readonly Dictionary<int, Previous> _previous = new();
    private readonly object _writeLock = new();
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private DateTime _nextCleanupUtc = DateTime.MinValue;

    public ResourceTelemetry(AppPaths paths, AppConfig config, Func<ResourceActivity>? activity = null)
    {
        _paths = paths;
        _config = config;
        _activity = activity;
        using var root = Process.GetCurrentProcess();
        _rootPid = root.Id;
        try { _rootStartedUtc = root.StartTime.ToUniversalTime(); }
        catch { _rootStartedUtc = DateTime.UtcNow; }
    }

    public string CurrentLogFile => Path.Combine(_paths.Telemetry, "resources_" + DateTime.Now.ToString("yyyyMMdd") + ".ndjson");

    public void Start()
    {
        if (!_config.ResourceTelemetryEnabled || _cts is not null) return;
        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => LoopAsync(_cts.Token));
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { SampleNow(); } catch { }
            var seconds = Math.Clamp(_config.ResourceTelemetrySeconds, 15, 3600);
            try { await Task.Delay(TimeSpan.FromSeconds(seconds), ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    /// <summary>Writes one sample synchronously. Public primarily for diagnostics and deterministic tests.</summary>
    public void SampleNow()
    {
        var now = DateTime.UtcNow;
        var parentMap = ProcessParents();
        var pids = Descendants(parentMap, _rootPid);
        pids.Add(_rootPid);

        var processRows = new List<object>();
        long ws = 0, priv = 0, handles = 0, threads = 0;
        double machineCpu = 0, coreCpu = 0;
        var webviews = 0;
        var live = new HashSet<int>();

        foreach (var pid in pids.OrderBy(x => x))
        {
            try
            {
                using var p = Process.GetProcessById(pid);
                p.Refresh();
                var start = p.StartTime.ToUniversalTime();
                var cpuMs = p.TotalProcessorTime.TotalMilliseconds;
                var wsBytes = Math.Max(0, p.WorkingSet64);
                var privateBytes = Math.Max(0, p.PrivateMemorySize64);
                var handleCount = Math.Max(0, p.HandleCount);
                var threadCount = p.Threads.Count;
                var name = p.ProcessName;

                double? procCore = null, procMachine = null;
                if (_previous.TryGetValue(pid, out var prev) && prev.StartTicks == start.Ticks)
                {
                    var elapsedMs = (now - prev.SampledUtc).TotalMilliseconds;
                    var deltaCpuMs = Math.Max(0, cpuMs - prev.CpuMs);
                    if (elapsedMs > 50)
                    {
                        procCore = deltaCpuMs / elapsedMs * 100.0;
                        procMachine = procCore / Math.Max(1, Environment.ProcessorCount);
                        coreCpu += procCore.Value;
                        machineCpu += procMachine.Value;
                    }
                }

                _previous[pid] = new Previous(start.Ticks, cpuMs, now);
                live.Add(pid);
                ws += wsBytes;
                priv += privateBytes;
                handles += handleCount;
                threads += threadCount;
                if (name.Equals("msedgewebview2", StringComparison.OrdinalIgnoreCase)) webviews++;

                parentMap.TryGetValue(pid, out var parentPid);
                processRows.Add(new
                {
                    pid,
                    parentPid,
                    name,
                    startUtc = start,
                    workingSetMb = Mb(wsBytes),
                    privateBytesMb = Mb(privateBytes),
                    handles = handleCount,
                    threads = threadCount,
                    cpuPctMachine = procMachine is null ? (double?)null : Math.Round(procMachine.Value, 3),
                    cpuPctCoreSum = procCore is null ? (double?)null : Math.Round(procCore.Value, 3)
                });
            }
            catch { }
        }

        foreach (var dead in _previous.Keys.Where(x => !live.Contains(x)).ToList()) _previous.Remove(dead);

        var activity = _activity?.Invoke() ?? new ResourceActivity(0, 0, 0);
        var gc = GC.GetGCMemoryInfo();
        var row = new
        {
            tsUtc = now,
            rootPid = _rootPid,
            rootStartedUtc = _rootStartedUtc,
            uptimeSeconds = Math.Max(0, (long)(now - _rootStartedUtc).TotalSeconds),
            logicalProcessors = Environment.ProcessorCount,
            cpuScale = "cpuPctMachine=share of total logical CPU capacity; cpuPctCoreSum=one-core scale, 100 per fully busy logical CPU",
            activeTasks = activity.ActiveTasks,
            waitingTasks = activity.WaitingTasks,
            tabs = activity.Tabs,
            processCount = processRows.Count,
            webViewProcessCount = webviews,
            workingSetMb = Mb(ws),
            privateBytesMb = Mb(priv),
            handles,
            threads,
            gcManagedMb = Mb(GC.GetTotalMemory(false)),
            gcHeapSizeMb = Mb(gc.HeapSizeBytes),
            gcCommittedMb = Mb(gc.TotalCommittedBytes),
            cpuPctMachine = Math.Round(machineCpu, 3),
            cpuPctCoreSum = Math.Round(coreCpu, 3),
            processes = processRows
        };

        var line = JsonSerializer.Serialize(row, Json) + Environment.NewLine;
        lock (_writeLock)
        {
            Directory.CreateDirectory(_paths.Telemetry);
            File.AppendAllText(CurrentLogFile, line, new UTF8Encoding(false));
        }

        if (now >= _nextCleanupUtc)
        {
            Cleanup(now);
            _nextCleanupUtc = now.AddHours(1);
        }
    }

    private static double Mb(long bytes) => Math.Round(bytes / 1024.0 / 1024.0, 3);

    private static Dictionary<int, int> ProcessParents()
    {
        var map = new Dictionary<int, int>();
        if (!OperatingSystem.IsWindows()) return map;
        var snapshot = CreateToolhelp32Snapshot(Th32csSnapProcess, 0);
        if (snapshot == IntPtr.Zero || snapshot == new IntPtr(-1)) return map;
        try
        {
            var e = new ProcessEntry32 { dwSize = (uint)Marshal.SizeOf<ProcessEntry32>() };
            if (!Process32First(snapshot, ref e)) return map;
            do
            {
                map[(int)e.th32ProcessID] = (int)e.th32ParentProcessID;
                e.dwSize = (uint)Marshal.SizeOf<ProcessEntry32>();
            } while (Process32Next(snapshot, ref e));
            return map;
        }
        finally { CloseHandle(snapshot); }
    }

    private static HashSet<int> Descendants(IReadOnlyDictionary<int, int> parentMap, int root)
    {
        var result = new HashSet<int>();
        var queue = new Queue<int>();
        queue.Enqueue(root);
        while (queue.Count > 0)
        {
            var parent = queue.Dequeue();
            foreach (var kv in parentMap)
            {
                if (kv.Value != parent || !result.Add(kv.Key)) continue;
                queue.Enqueue(kv.Key);
            }
        }
        result.Remove(root);
        return result;
    }

    private void Cleanup(DateTime nowUtc)
    {
        try
        {
            if (!Directory.Exists(_paths.Telemetry)) return;
            var files = new DirectoryInfo(_paths.Telemetry)
                .EnumerateFiles("resources_*.ndjson")
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .ToList();

            var retention = Math.Clamp(_config.ResourceTelemetryRetentionDays, 1, 90);
            foreach (var f in files.Where(f => f.LastWriteTimeUtc < nowUtc.AddDays(-retention)).ToList())
            {
                try { f.Delete(); } catch { }
            }

            files = new DirectoryInfo(_paths.Telemetry)
                .EnumerateFiles("resources_*.ndjson")
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .ToList();
            long maxBytes = (long)Math.Clamp(_config.ResourceTelemetryMaxMb, 10, 2048) * 1024 * 1024;
            long total = 0;
            foreach (var f in files)
            {
                total += f.Length;
                if (total <= maxBytes) continue;
                try { f.Delete(); } catch { }
            }
        }
        catch { }
    }

    public void Dispose()
    {
        var cts = Interlocked.Exchange(ref _cts, null);
        if (cts is null) return;
        try { cts.Cancel(); } catch { }
        try { _loop?.Wait(TimeSpan.FromSeconds(2)); } catch { }
        cts.Dispose();
        _loop = null;
    }
}
