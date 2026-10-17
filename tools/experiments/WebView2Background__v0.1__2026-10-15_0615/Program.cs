// Isolated WebView2 background throttling experiment v0.1, 2026-10-15.
// Never uses OpenFill's production browser profile or processes.
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Windows.Forms;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var mode = args.Length > 0 ? args[0].Trim().ToLowerInvariant() : "default";
        if (mode != "default" && mode != "forced")
        {
            Console.Error.WriteLine("Usage: WebView2Background.exe default|forced");
            Environment.ExitCode = 2;
            return;
        }
        ApplicationConfiguration.Initialize();
        Application.Run(new ProbeForm(mode));
    }
}

internal sealed class ProbeForm : Form
{
    private readonly string _mode;
    private readonly WebView2 _view = new() { Dock = DockStyle.Fill };
    private const string Html = """
<!doctype html><html><head><title>Isolated OpenFill timer probe</title></head>
<body><div id="state">WebView2 background timer experiment</div><script>
window.__ticks = 0;
window.__started = performance.now();
setInterval(() => {
  const end = performance.now() + 6;
  while (performance.now() < end) { Math.sqrt(Math.random()); }
  window.__ticks++;
  document.getElementById('state').textContent = 'Ticks: ' + window.__ticks;
}, 50);
</script></body></html>
""";

    internal ProbeForm(string mode)
    {
        _mode = mode;
        Text = "Isolated WebView2 timer probe (" + mode + ")";
        Width = 640;
        Height = 400;
        Controls.Add(_view);
        Shown += async (_, _) =>
        {
            try { await RunAsync(); }
            catch (Exception ex)
            {
                Console.WriteLine(JsonSerializer.Serialize(new { mode = _mode, error = ex.ToString() }));
                Environment.ExitCode = 1;
            }
            finally { Close(); }
        };
    }

    private static HashSet<int> WebViewPids()
    {
        return Process.GetProcessesByName("msedgewebview2").Select(p => p.Id).ToHashSet();
    }

    private static Dictionary<int, double> CpuSeconds(IEnumerable<int> pids)
    {
        var result = new Dictionary<int, double>();
        foreach (var pid in pids)
        {
            try { using var p = Process.GetProcessById(pid); result[pid] = p.TotalProcessorTime.TotalSeconds; }
            catch (ArgumentException) { }
            catch (System.ComponentModel.Win32Exception) { }
            catch (InvalidOperationException) { }
        }
        return result;
    }

    private async Task<int> TicksAsync()
    {
        var raw = await _view.CoreWebView2.ExecuteScriptAsync("window.__ticks || 0");
        return JsonSerializer.Deserialize<int>(raw);
    }

    private async Task RunAsync()
    {
        var old = WebViewPids();
        var profile = Path.Combine(Path.GetTempPath(), "OpenFill_WebView2Background_v01_" + Guid.NewGuid().ToString("N"));
        var options = new CoreWebView2EnvironmentOptions();
        if (_mode == "forced")
            options.AdditionalBrowserArguments =
                "--disable-features=CalculateNativeWinOcclusion --disable-backgrounding-occluded-windows " +
                "--disable-renderer-backgrounding --disable-background-timer-throttling";
        var env = await CoreWebView2Environment.CreateAsync(null, profile, options);
        await _view.EnsureCoreWebView2Async(env);
        var navigation = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _view.CoreWebView2.NavigationCompleted += (_, e) => navigation.TrySetResult(e.IsSuccess);
        _view.CoreWebView2.NavigateToString(Html);
        if (!await navigation.Task.WaitAsync(TimeSpan.FromSeconds(20)))
            throw new InvalidOperationException("Probe page navigation failed.");
        await Task.Delay(3000);
        var visibleTicks = await TicksAsync();
        var pids = WebViewPids().Except(old).ToArray();
        if (pids.Length < 3) throw new InvalidOperationException("Cannot isolate experiment WebView2 processes.");
        WindowState = FormWindowState.Minimized;
        await Task.Delay(1500);
        var startTicks = await TicksAsync();
        var before = CpuSeconds(pids);
        var start = Stopwatch.StartNew();
        await Task.Delay(30000);
        var endTicks = await TicksAsync();
        start.Stop();
        var after = CpuSeconds(pids);
        var cpuSec = after.Where(x => before.ContainsKey(x.Key))
            .Sum(x => Math.Max(0, x.Value - before[x.Key]));
        var privateMb = 0.0;
        foreach (var pid in pids)
        {
            try { using var p = Process.GetProcessById(pid); privateMb += p.PrivateMemorySize64 / 1048576.0; }
            catch (ArgumentException) { }
            catch (InvalidOperationException) { }
        }
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            mode = _mode,
            startedLocal = DateTime.Now.ToString("O"),
            durationSeconds = Math.Round(start.Elapsed.TotalSeconds, 2),
            visibleTicks,
            minimizedStartTicks = startTicks,
            minimizedEndTicks = endTicks,
            minimizedTicksPerSecond = Math.Round((endTicks - startTicks) / start.Elapsed.TotalSeconds, 3),
            cpuPctMachine = Math.Round(100 * cpuSec / start.Elapsed.TotalSeconds / Environment.ProcessorCount, 3),
            processCount = pids.Length,
            privateMb = Math.Round(privateMb, 2),
            isolatedProfile = profile
        }));
        Console.Out.Flush();
    }
}
