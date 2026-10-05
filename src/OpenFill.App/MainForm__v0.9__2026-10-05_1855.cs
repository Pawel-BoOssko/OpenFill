// OpenFill - Metadata: wersja 0.9, data 2026-10-05 18:55
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using OpenFill.Core;
using OpenFill.Core.Assets;
using OpenFill.Core.Cdp;
using OpenFill.Core.Config;
using OpenFill.Core.Hosting;
using OpenFill.Core.Mcp;

namespace OpenFill.App;

/// <summary>
/// Main window: the browser on the left (the page the model fills in; you also log in by hand here),
/// the live panel on the right with the version and date at the very top, the task field and the event stream.
/// All logic lives in the core (AppHost) - the window only connects WebView2 to the core.
/// </summary>
public sealed class MainForm : Form
{
    private readonly AppPaths _paths;
    private readonly AppConfig _config;
    private readonly SecretStore _secrets;

    // Browser tabs: one WebView2 per tab (all share one profile), only the active one is visible; the strip lists them.
    private readonly Panel _browserHost = new() { Dock = DockStyle.Fill, Margin = Padding.Empty };
    private readonly FlowLayoutPanel _strip = new() { Dock = DockStyle.Top, Height = 30, WrapContents = false, AutoScroll = false, Padding = new Padding(2, 1, 2, 0), FlowDirection = FlowDirection.LeftToRight };
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, BrowserTab> _tabs = new();
    private readonly SwitchableCdpConnection _switchable = new();
    private readonly ToolTip _tip = new();
    private Font? _fontBold;
    private CoreWebView2Environment? _browserEnv;
    private BrowserTab? _active;
    private TabManager? _tabMgr;

    private sealed class BrowserTab(string id, WebView2 view)
    {
        public string Id { get; } = id;
        public WebView2 View { get; } = view;
        public WebView2CdpConnection? Conn { get; set; }
    }

    private readonly WebView2 _panel = new() { Dock = DockStyle.Fill };
    private readonly TextBox _address = new() { Dock = DockStyle.Fill };
    private readonly Button _back = new() { Text = "◀", Width = 36, Dock = DockStyle.Fill, FlatStyle = FlatStyle.System };
    private readonly Button _forward = new() { Text = "▶", Width = 36, Dock = DockStyle.Fill, FlatStyle = FlatStyle.System };
    private readonly Button _reload = new() { Text = "⟳", Width = 36, Dock = DockStyle.Fill, FlatStyle = FlatStyle.System };
    private readonly Button _go = new() { Text = "Go", Width = 70, Dock = DockStyle.Fill, FlatStyle = FlatStyle.System };
    // Fixed, deterministic layout: the browser on the left (the rest of the width), a fixed-width panel on the right.
    // (Earlier a SplitContainer was used - with DPI scaling it could collapse the panel to a few dozen pixels.)
    private const int PanelWidth = 520;
    private readonly TableLayoutPanel _layout = new() { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty, Padding = Padding.Empty };
    private readonly Panel _left = new() { Dock = DockStyle.Fill, Margin = Padding.Empty };
    private readonly Panel _right = new() { Dock = DockStyle.Fill, Margin = Padding.Empty };

    private AppHost? _host;
    private McpService? _mcp;
    private bool _closing;

    public MainForm(AppPaths paths, AppConfig config, SecretStore secrets)
    {
        _paths = paths;
        _config = config;
        _secrets = secrets;

        Text = "OpenFill — " + BuildInfo.Headline() + (paths.Instance != "stable" ? $" [{paths.Instance}]" : "");
        Width = 1560;
        Height = 980;
        StartPosition = FormStartPosition.CenterScreen;
        if (Program.StartMinimized) { WindowState = FormWindowState.Minimized; ShowInTaskbar = true; }
        MinimumSize = new Size(1000, 600);

        var nav = new TableLayoutPanel { Dock = DockStyle.Top, Height = 34, ColumnCount = 5, RowCount = 1, Padding = new Padding(4, 3, 4, 3) };
        nav.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 40));
        nav.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 40));
        nav.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 40));
        nav.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        nav.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 78));
        nav.Controls.Add(_back, 0, 0);
        nav.Controls.Add(_forward, 1, 0);
        nav.Controls.Add(_reload, 2, 0);
        nav.Controls.Add(_address, 3, 0);
        nav.Controls.Add(_go, 4, 0);

        _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, PanelWidth));
        _layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _left.Controls.Add(_browserHost);
        _left.Controls.Add(_strip);
        _left.Controls.Add(nav);
        _right.Controls.Add(_panel);
        _layout.Controls.Add(_left, 0, 0);
        _layout.Controls.Add(_right, 1, 0);
        Controls.Add(_layout);

        _back.Click += (_, _) => { var c = _active?.View.CoreWebView2; if (c?.CanGoBack == true) c.GoBack(); };
        _forward.Click += (_, _) => { var c = _active?.View.CoreWebView2; if (c?.CanGoForward == true) c.GoForward(); };
        _reload.Click += (_, _) => _active?.View.CoreWebView2?.Reload();
        _go.Click += (_, _) => NavigateFromAddress();
        _address.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; NavigateFromAddress(); } };

        Load += async (_, _) => await InitializeAsync();
        FormClosing += OnFormClosing;
        Resize += (_, _) => KeepPanelWidth();
    }

    private void KeepPanelWidth()
    {
        // Panel: 520 px (scaled by DPI), but at most 55% of the window and at least 380 px.
        if (_layout.Width <= 0) return;
        var scale = DeviceDpi / 96.0;
        var want = (int)(PanelWidth * scale);
        var panelWidth = Math.Max((int)(380 * scale), Math.Min(want, (int)(_layout.Width * 0.55)));
        _layout.ColumnStyles[1].SizeType = SizeType.Absolute;
        _layout.ColumnStyles[1].Width = panelWidth;
    }

    private void NavigateFromAddress()
    {
        var url = _address.Text.Trim();
        var current = _active?.View.CoreWebView2;
        if (url.Length == 0 || current is null) return;
        if (_host?.IsRunning == true)
        {
            MessageBox.Show(this, "A task is running. Stop it in the panel before changing the page.", "OpenFill", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        try { current.Navigate(AppHost.NormalizeUrl(url)); }
        catch (Exception ex) { MessageBox.Show(this, "Invalid address: " + ex.Message, "OpenFill"); }
    }

    private async Task InitializeAsync()
    {
        KeepPanelWidth();
        try
        {
            // Browser: persistent profile (logged-in sessions stay between runs).
            // The page must keep running at full speed when the window is minimized (autostart) or covered by other windows.
            var browserOptions = new CoreWebView2EnvironmentOptions
            {
                AdditionalBrowserArguments = "--disable-features=CalculateNativeWinOcclusion --disable-backgrounding-occluded-windows --disable-renderer-backgrounding --disable-background-timer-throttling"
            };
            _browserEnv = await CoreWebView2Environment.CreateAsync(null, _paths.BrowserProfile, browserOptions);

            // Panel: a separate small profile - it does not mix with the cookies of the pages.
            var panelEnv = await CoreWebView2Environment.CreateAsync(null, Path.Combine(_paths.Runtime, "panel-webview"));
            await _panel.EnsureCoreWebView2Async(panelEnv);
        }
        catch (WebView2RuntimeNotFoundException)
        {
            MessageBox.Show(this,
                "The Microsoft Edge WebView2 Runtime is missing.\n\nRun tools\\install.ps1 again - it installs it automatically.",
                "OpenFill", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Close();
            return;
        }

        // The first tab (belongs to no task yet; the first task takes it over). Its connection feeds the core before the host starts.
        var firstTab = await CreateTabAsync(null);
        ActivateTab(firstTab);

        var cdp = new CdpSession(_switchable);
        var transport = new WebViewPanelTransport(_panel);
        _host = new AppHost(_paths, _config, _secrets, cdp, transport)
        {
            OpenFolder = path =>
            {
                try { Directory.CreateDirectory(path); Process.Start(new ProcessStartInfo("explorer.exe", "\"" + path + "\"") { UseShellExecute = true }); }
                catch { }
            },
            // The dialog must run on the UI thread; AppHost calls this from a background thread.
            PickFile = () => (string?)Invoke(new Func<string?>(() =>
            {
                using var dlg = new OpenFileDialog { Title = "Choose a file for the task", CheckFileExists = true, Multiselect = false };
                return dlg.ShowDialog(this) == DialogResult.OK ? dlg.FileName : null;
            }))
        };
        _host.Attention += OnAttention;
        _tabMgr = new TabManager(new TabsAdapter(this), Path.Combine(_paths.Root, "tabs.json"), _config.MaxTabs);
        _host.Tabs = _tabMgr;
        _tabMgr.RegisterFree(firstTab.Id, "Start");
        await _host.InitAsync();

        _panel.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
        _panel.CoreWebView2.Settings.IsZoomControlEnabled = true;
        // Panel diagnostics: the log shows whether the HTML loaded and what size the control has.
        _panel.CoreWebView2.NavigationCompleted += (_, e) =>
            _host?.Session.Log.Write("app", "panel", $"Panel loaded: success={e.IsSuccess}, size {_panel.Width}x{_panel.Height}, window {Width}x{Height}, DPI {DeviceDpi}", status: e.IsSuccess ? "ok" : "error");
        _panel.CoreWebView2.ProcessFailed += (_, e) =>
            _host?.Session.Log.Write("app", "panel", $"Panel process failed: {e.ProcessFailedKind}", status: "error");
        _panel.CoreWebView2.NavigateToString(AssetLoader.Load("panel.html"));
        KeepPanelWidth();

        var home = string.IsNullOrWhiteSpace(_config.HomeUrl) || _config.HomeUrl == "about:blank" ? "https://www.google.com" : _config.HomeUrl;
        firstTab.View.CoreWebView2.Navigate(home);
        if (_config.McpEnabled)
        {
            var mcpHost = _host!;
            _ = Task.Run(async () =>
            {
                try
                {
                    _mcp = await McpService.StartAsync(mcpHost, _paths, _config, mcpHost.Session.Log,
                        url => BeginInvoke(new Action(() => { try { Clipboard.SetText(url); } catch { } })));
                }
                catch (Exception ex) { mcpHost.Session.Log.Write("mcp", "error", "MCP failed to start: " + ex.Message, status: "error"); }
            });
        }
        _host.Session.Log.Write("app", "start", $"OpenFill started - {BuildInfo.Headline()}, instance {_paths.Instance}");
    }

    // ------------------------------------------------------------------ browser tabs (one per task)

    /// <summary>Runs on the UI thread (inline when already there) and returns the result to any caller.</summary>
    private Task<T> OnUi<T>(Func<Task<T>> work)
    {
        if (!InvokeRequired) return work();
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            BeginInvoke(new Action(async () =>
            {
                try { tcs.TrySetResult(await work()); }
                catch (Exception ex) { tcs.TrySetException(ex); }
            }));
        }
        catch (Exception ex) { tcs.TrySetException(ex); }
        return tcs.Task;
    }

    /// <summary>Creates a tab (a WebView2 in the shared profile) and makes it the visible one at the top of the stack. Call on the UI thread.</summary>
    private async Task<BrowserTab> CreateTabAsync(string? url)
    {
        var view = new WebView2 { Dock = DockStyle.Fill };
        var tab = new BrowserTab("t" + Guid.NewGuid().ToString("N")[..8], view);
        _browserHost.Controls.Add(view);
        view.BringToFront();
        await view.EnsureCoreWebView2Async(_browserEnv);
        var core = view.CoreWebView2;
        core.Settings.AreDevToolsEnabled = true;
        core.Settings.IsStatusBarEnabled = true;
        // alert/confirm/prompt windows must not block the page while the model works: we accept them
        // (the core gets the same information through CDP and shows it to the model in get_page).
        core.Settings.AreDefaultScriptDialogsEnabled = false;
        core.ScriptDialogOpening += (_, e) =>
        {
            e.ResultText = e.DefaultText;
            e.Accept();
            _host?.Session.Log.Write("browser", "dialog", $"Page dialog ({e.Kind}): {e.Message} - accepted", status: "warn");
        };
        // New windows open in the same tab, so the task does not lose sight of its page.
        core.NewWindowRequested += (_, e) => { e.Handled = true; core.Navigate(e.Uri); };
        core.SourceChanged += (_, _) => { if (ReferenceEquals(_active, tab) && !_address.Focused) _address.Text = core.Source; };
        core.HistoryChanged += (_, _) => { if (ReferenceEquals(_active, tab)) { _back.Enabled = core.CanGoBack; _forward.Enabled = core.CanGoForward; } };
        core.DownloadStarting += (_, e) =>
        {
            var name = Path.GetFileName(e.ResultFilePath);
            e.ResultFilePath = Path.Combine(_paths.Downloads, name);
            _host?.Session.Log.Write("browser", "download", "Download: " + name, new { path = e.ResultFilePath });
        };
        tab.Conn = new WebView2CdpConnection(this, core);
        _tabs[tab.Id] = tab;
        if (url is not null) core.Navigate(url);
        return tab;
    }

    /// <summary>Shows the tab and points the core's connection at it. Call on the UI thread.</summary>
    private void ActivateTab(BrowserTab tab)
    {
        foreach (var other in _tabs.Values)
            if (!ReferenceEquals(other, tab)) other.View.Visible = false;
        tab.View.Visible = true;
        tab.View.BringToFront();
        _active = tab;
        _switchable.Attach(tab.Conn!, tab.Id);
        var core = tab.View.CoreWebView2;
        if (core is not null)
        {
            _address.Text = core.Source ?? "";
            _back.Enabled = core.CanGoBack;
            _forward.Enabled = core.CanGoForward;
        }
    }

    private async Task CloseTabCoreAsync(string tabId)
    {
        if (!_tabs.TryRemove(tabId, out var tab)) return;
        var wasActive = ReferenceEquals(_active, tab);
        if (tab.Conn is not null)
        {
            _switchable.Detach(tab.Conn);
            await tab.Conn.DisposeAsync();
        }
        _browserHost.Controls.Remove(tab.View);
        try { tab.View.Dispose(); } catch { }
        if (wasActive)
        {
            _active = null;
            var next = _tabs.Values.FirstOrDefault();
            if (next is not null) ActivateTab(next);
        }
    }

    private void RebuildStrip(IReadOnlyList<TabInfo> tabs, string? activeId)
    {
        _fontBold ??= new Font(Font, FontStyle.Bold);
        _strip.SuspendLayout();
        foreach (var c in _strip.Controls.Cast<Control>().ToList()) { _strip.Controls.Remove(c); c.Dispose(); }
        foreach (var t in tabs)
        {
            if (t.TabId is not { } id) continue;
            var glyph = t.State switch { "running" => "▶", "waiting" => "⚠", "free" => "○", _ => "✓" };
            var active = id == activeId;
            var b = new Button
            {
                Text = glyph + " " + t.Label,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlatStyle = FlatStyle.Flat,
                Height = 24,
                Margin = new Padding(1, 1, 0, 1),
                Font = active ? _fontBold : Font,
                BackColor = active ? SystemColors.Window : SystemColors.Control
            };
            b.FlatAppearance.BorderColor = t.State == "waiting" ? Color.DarkOrange : SystemColors.ControlDark;
            b.Click += (_, _) => UserSelectTab(id);
            _tip.SetToolTip(b, t.Label + (string.IsNullOrEmpty(t.LastUrl) ? "" : "\n" + t.LastUrl));
            var x = new Button { Text = "✕", Width = 22, Height = 24, FlatStyle = FlatStyle.Flat, Margin = new Padding(0, 1, 6, 1) };
            x.FlatAppearance.BorderColor = SystemColors.ControlDark;
            x.Click += async (_, _) => await UserCloseTabAsync(id);
            _strip.Controls.Add(b);
            _strip.Controls.Add(x);
        }
        _strip.ResumeLayout();
    }

    private void UserSelectTab(string id)
    {
        if (!_tabs.TryGetValue(id, out var tab)) return;
        if (ReferenceEquals(_active, tab)) return;
        if (_host?.IsRunning == true)
        {
            MessageBox.Show(this, "A task is running in the current tab. Switch tabs after it finishes.", "OpenFill", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        ActivateTab(tab);
        _tabMgr?.UserSelected(id);
    }

    private async Task UserCloseTabAsync(string id)
    {
        if (_tabMgr is null) return;
        if (!await _tabMgr.UserCloseAsync(id))
        {
            MessageBox.Show(this, "A task is running in this tab (or waits for you). Stop it in the panel first.", "OpenFill", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (_tabs.IsEmpty)
        {
            var blank = await CreateTabAsync("about:blank");
            ActivateTab(blank);
            _tabMgr.RegisterFree(blank.Id, "New tab");
        }
    }

    /// <summary>What the core's TabManager uses to drive the window.</summary>
    private sealed class TabsAdapter(MainForm f) : IBrowserTabs
    {
        public Task<string> OpenTabAsync(string? url, string label, CancellationToken ct) =>
            f.OnUi(async () =>
            {
                var tab = await f.CreateTabAsync(url ?? "about:blank");
                f.ActivateTab(tab);
                return tab.Id;
            });

        public Task SelectTabAsync(string tabId, CancellationToken ct) =>
            f.OnUi(() =>
            {
                if (f._tabs.TryGetValue(tabId, out var tab)) f.ActivateTab(tab);
                return Task.FromResult(true);
            });

        public Task NavigateAsync(string tabId, string url, CancellationToken ct) =>
            f.OnUi(() =>
            {
                if (f._tabs.TryGetValue(tabId, out var tab)) tab.View.CoreWebView2?.Navigate(url);
                return Task.FromResult(true);
            });

        public Task CloseTabAsync(string tabId) => f.OnUi(async () => { await f.CloseTabCoreAsync(tabId); return true; });

        public Task<string?> UrlAsync(string tabId) =>
            f.OnUi(() => Task.FromResult(f._tabs.TryGetValue(tabId, out var tab) ? tab.View.CoreWebView2?.Source : null));

        public bool Exists(string tabId) => f._tabs.ContainsKey(tabId);

        public void Strip(IReadOnlyList<TabInfo> tabs, string? activeTabId)
        {
            try { f.BeginInvoke(new Action(() => f.RebuildStrip(tabs, activeTabId))); } catch { }
        }
    }

    // ------------------------------------------------------------------ "OpenFill waits for you": sound, flashing icon, title

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct FLASHWINFO { public uint cbSize; public IntPtr hwnd; public uint dwFlags; public uint uCount; public uint dwTimeout; }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

    private System.Windows.Forms.Timer? _attentionTimer;
    private string? _baseTitle;

    /// <summary>Called from any thread when a card in the panel needs the person (needed=true) or none is open any more.</summary>
    private void OnAttention(bool needed, string text)
    {
        try { BeginInvoke(new Action(() => ShowAttention(needed, text))); } catch { }
    }

    private void ShowAttention(bool needed, string text)
    {
        _baseTitle ??= Text;
        if (!needed)
        {
            _attentionTimer?.Stop();
            Text = _baseTitle;
            var stop = new FLASHWINFO { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<FLASHWINFO>(), hwnd = Handle, dwFlags = 0 };
            try { FlashWindowEx(ref stop); } catch { }
            return;
        }
        Text = "⚠ OpenFill is waiting for you — " + _baseTitle;
        Notify();
        if (_attentionTimer is null)
        {
            _attentionTimer = new System.Windows.Forms.Timer { Interval = 30000 };
            _attentionTimer.Tick += (_, _) => Notify();
        }
        _attentionTimer.Start();
    }

    /// <summary>One sound and a flashing taskbar icon (until the window is brought forward). Repeats every 30 s while a card is open.</summary>
    private void Notify()
    {
        try { System.Media.SystemSounds.Exclamation.Play(); } catch { }
        try
        {
            var f = new FLASHWINFO { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<FLASHWINFO>(), hwnd = Handle, dwFlags = 3 | 12, uCount = uint.MaxValue, dwTimeout = 0 };
            FlashWindowEx(ref f);
        }
        catch { }
    }

    private async void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_closing || _host is null) return;
        e.Cancel = true;
        _closing = true;
        try { if (_mcp is not null) await _mcp.DisposeAsync(); } catch { }
        try { await Task.WhenAny(_host.DisposeAsync().AsTask(), Task.Delay(4000)); } catch { }
        _host = null;
        Close();
    }
}
