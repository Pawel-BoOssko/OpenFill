// OpenFill - Metadata: wersja 0.27, data 2026-10-28 22:22
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
using OpenFill.Core.Logging;
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

    // Browser tabs: one WebView2 per tab (all share one profile), the active one is on top of the stack; the strip lists them.
    private readonly Panel _browserHost = new() { Dock = DockStyle.Fill, Margin = Padding.Empty };
    private readonly FlowLayoutPanel _strip = new() { Dock = DockStyle.Top, Height = 38, WrapContents = false, AutoScroll = false, Padding = new Padding(2, 1, 2, 0), FlowDirection = FlowDirection.LeftToRight };
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, BrowserTab> _tabs = new();
    private readonly SwitchableCdpConnection _switchable = new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, OpenFill.Core.Agent.ImportedCredentialVault.Account> _basicAuthChoices = new();
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
        /// <summary>What a task in this tab talks to: the tab itself, or - while one is open - the pop-up window the tab opened.</summary>
        public SwitchableCdpConnection TaskConn { get; } = new();
    }

    private readonly WebView2 _panel = new() { Dock = DockStyle.Fill };
    private readonly ComboBox _address = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDown,
        AutoCompleteMode = AutoCompleteMode.SuggestAppend, AutoCompleteSource = AutoCompleteSource.ListItems,
        MaxDropDownItems = 12, IntegralHeight = false }; 
    private readonly AddressHistory _addressHistory;
    private readonly Button _back = new() { Text = "◀", Width = 36, Dock = DockStyle.Fill, FlatStyle = FlatStyle.System };
    private readonly Button _forward = new() { Text = "▶", Width = 36, Dock = DockStyle.Fill, FlatStyle = FlatStyle.System };
    private readonly Button _reload = new() { Text = "⟳", Width = 36, Dock = DockStyle.Fill, FlatStyle = FlatStyle.System };
    private readonly Button _go = new() { Text = "Go", Width = 70, Dock = DockStyle.Fill, FlatStyle = FlatStyle.System };
    private readonly Button _credentials = new() { Text = "Credentials", Width = 112, Dock = DockStyle.Fill, FlatStyle = FlatStyle.System };
    private readonly Button _firefoxImport = new() { Text = "Import Firefox", Width = 132, Dock = DockStyle.Fill, FlatStyle = FlatStyle.System };
    // Fixed, deterministic layout: the browser on the left (the rest of the width), a fixed-width panel on the right.
    // (Earlier a SplitContainer was used - with DPI scaling it could collapse the panel to a few dozen pixels.)
    private const int PanelWidth = 520;
    private readonly TableLayoutPanel _layout = new() { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty, Padding = Padding.Empty };
    private readonly Panel _left = new() { Dock = DockStyle.Fill, Margin = Padding.Empty };
    private TableLayoutPanel? _navigation;
    private bool _navigationCompact;
    private readonly Panel _right = new() { Dock = DockStyle.Fill, Margin = Padding.Empty };

    private AppHost? _host;
    private McpService? _mcp;
    private ResourceTelemetry? _telemetry;
    private bool _closing;
    private int _upgradeDowntimeMinutes = 10;
    private string _shutdownReason = "OpenFill operator-requested upgrade";

    public MainForm(AppPaths paths, AppConfig config, SecretStore secrets)
    {
        _paths = paths;
        _config = config;
        _secrets = secrets;
        _addressHistory = new AddressHistory(Path.Combine(paths.Root, "address_history.json"));

        Text = "OpenFill — " + BuildInfo.Headline() + (paths.Instance != "stable" ? $" [{paths.Instance}]" : "");
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? Icon; } catch { }
        Width = 1560;
        Height = 980;
        StartPosition = FormStartPosition.CenterScreen;
        if (Program.StartMinimized) { WindowState = FormWindowState.Minimized; ShowInTaskbar = true; }
        MinimumSize = new Size(1000, 600);

        var nav = new TableLayoutPanel { Dock = DockStyle.Top, Height = 34, ColumnCount = 7, RowCount = 1, Padding = new Padding(4, 3, 4, 3) };
        nav.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 40));
        nav.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 40));
        nav.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 40));
        nav.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        nav.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 78));
        nav.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118));
        nav.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        nav.Controls.Add(_back, 0, 0);
        nav.Controls.Add(_forward, 1, 0);
        nav.Controls.Add(_reload, 2, 0);
        nav.Controls.Add(_address, 3, 0);
        nav.Controls.Add(_go, 4, 0);
        nav.Controls.Add(_credentials, 5, 0);
        nav.Controls.Add(_firefoxImport, 6, 0);
        _navigation = nav;
        RefreshAddressHistory();

        _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, PanelWidth));
        _layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _left.Controls.Add(_browserHost);
        _left.Controls.Add(_strip);
        _left.Controls.Add(nav);
        _left.Controls.Add(TechnologyAtlas.CreateBanner(() => { using var dialog = new TechnologyAtlasDialog(); dialog.ShowDialog(this); }, () => { using var dialog = new ControlledUpgradeDialog(); if (dialog.ShowDialog(this) != DialogResult.OK) return; _shutdownReason = "OpenFill operator-requested upgrade"; Close(); }));
        _right.Controls.Add(_panel);
        _layout.Controls.Add(_left, 0, 0);
        _layout.Controls.Add(_right, 1, 0);
        Controls.Add(_layout);

        _back.Click += (_, _) => { var c = _active?.View.CoreWebView2; if (c?.CanGoBack == true) c.GoBack(); };
        _forward.Click += (_, _) => { var c = _active?.View.CoreWebView2; if (c?.CanGoForward == true) c.GoForward(); };
        _reload.Click += (_, _) => _active?.View.CoreWebView2?.Reload();
        _go.Click += (_, _) => NavigateFromAddress();
                _credentials.Click += (_, _) =>
        {
            using var dialog = new ImportedAccountsDialog();
            if (dialog.ShowDialog(this) != DialogResult.OK || dialog.SelectedAccount is not { } chosen) return;
            var tab = _active;
            if (tab?.View.CoreWebView2 is not { } browser) return;
            var current = OpenFill.Core.Agent.ImportedCredentialVault.NormalizeOrigin(browser.Source);
            if (!string.Equals(current, chosen.Origin, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this, "Open the exact website for this saved credential first. OpenFill will not send it to a different origin.",
                    "OpenFill - credential origin", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            _basicAuthChoices[tab.Id] = chosen;
            browser.Reload();
        };
        _firefoxImport.Click += (_, _) => { using var picker = new FirefoxPasswordPicker(_active?.View.CoreWebView2?.Source); picker.ShowDialog(this); };
        _address.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; NavigateFromAddress(); } };
        _address.SelectionChangeCommitted += (_, _) => NavigateFromAddress();

        KeyPreview = true;
        KeyDown += (_, e) => { if (e.Control && e.KeyCode == Keys.T) { e.SuppressKeyPress = true; NewUserTab(); } };
        Load += async (_, _) => await InitializeAsync();
        FormClosing += OnFormClosing;
        Resize += (_, _) => { KeepPanelWidth(); UpdateNavigationLayout(); };
        _left.Resize += (_, _) => UpdateNavigationLayout();
        _strip.SizeChanged += (_, _) => { if (_strip.Width != _lastStripWidth && _lastTabs.Count > 0) RebuildStrip(_lastTabs, _lastActiveId); };
        Activated += (_, _) => AcknowledgeAttention();
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

    private void UpdateNavigationLayout()
    {
        var nav = _navigation;
        if (nav is null || _left.Width <= 0) return;
        var scale = DeviceDpi / 96.0;
        var compact = _left.Width < (int)Math.Ceiling(720 * scale);
        if (compact == _navigationCompact) return;

        nav.SuspendLayout();
        try
        {
            if (compact)
            {
                nav.RowCount = 2;
                while (nav.RowStyles.Count < 2)
                    nav.RowStyles.Add(new RowStyle(SizeType.Absolute, (float)(34 * scale)));
                nav.RowStyles[0].SizeType = SizeType.Absolute;
                nav.RowStyles[0].Height = (float)(34 * scale);
                nav.RowStyles[1].SizeType = SizeType.Absolute;
                nav.RowStyles[1].Height = (float)(34 * scale);
                nav.SetCellPosition(_back, new TableLayoutPanelCellPosition(0, 1));
                nav.SetCellPosition(_forward, new TableLayoutPanelCellPosition(1, 1));
                nav.SetCellPosition(_reload, new TableLayoutPanelCellPosition(2, 1));
                nav.SetCellPosition(_go, new TableLayoutPanelCellPosition(4, 1));
                nav.SetCellPosition(_credentials, new TableLayoutPanelCellPosition(5, 1));
                nav.SetCellPosition(_firefoxImport, new TableLayoutPanelCellPosition(6, 1));
                nav.SetCellPosition(_address, new TableLayoutPanelCellPosition(0, 0));
                nav.SetColumnSpan(_address, 7);
                nav.Height = (int)Math.Ceiling(68 * scale);
            }
            else
            {
                nav.SetColumnSpan(_address, 1);
                nav.SetCellPosition(_address, new TableLayoutPanelCellPosition(3, 0));
                nav.SetCellPosition(_back, new TableLayoutPanelCellPosition(0, 0));
                nav.SetCellPosition(_forward, new TableLayoutPanelCellPosition(1, 0));
                nav.SetCellPosition(_reload, new TableLayoutPanelCellPosition(2, 0));
                nav.SetCellPosition(_go, new TableLayoutPanelCellPosition(4, 0));
                nav.SetCellPosition(_credentials, new TableLayoutPanelCellPosition(5, 0));
                nav.SetCellPosition(_firefoxImport, new TableLayoutPanelCellPosition(6, 0));
                nav.RowCount = 1;
                nav.Height = (int)Math.Ceiling(34 * scale);
            }
            _navigationCompact = compact;
        }
        finally { nav.ResumeLayout(true); }
    }
    private void RefreshAddressHistory()
    {
        // Preserve the text being typed while repopulating the most-recent-first suggestions.
        var typed = _address.Text;
        var cursor = _address.SelectionStart;
        _address.BeginUpdate();
        try
        {
            _address.Items.Clear();
            _address.Items.AddRange(_addressHistory.Suggestions.Cast<object>().ToArray());
            _address.Text = typed;
            _address.SelectionStart = Math.Min(cursor, typed.Length);
        }
        finally { _address.EndUpdate(); }
    }

    private void NavigateFromAddress()
    {
        var url = _addressHistory.Resolve(_address.Text);
        var current = _active?.View.CoreWebView2;
        if (url.Length == 0 || current is null) return;
        if (_tabMgr?.ActiveTabBusy() == true)
        {
            MessageBox.Show(this, "A task is working in this tab. Stop it in the panel before changing the page, or pick another tab.", "OpenFill", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
        // Keep completed browser tabs alive for checkout, payment, and other manual handoffs.
        // The existing tab-count limit can still evict the oldest finished tab if all slots are occupied.
        _tabMgr = new TabManager(new TabsAdapter(this), Path.Combine(_paths.Root, "tabs.json"), _config.MaxTabs, releaseFinishedTabs: false);
        _host.Tabs = _tabMgr;
        _tabMgr.RegisterFree(firstTab.Id, "Start");
        await _host.InitAsync();

        _telemetry = new ResourceTelemetry(_paths, _config, () => new ResourceActivity(
            _host?.ActiveTaskCount ?? 0,
            _host?.WaitingTaskCount ?? 0,
            _tabs.Count));
        _telemetry.Start();

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

    /// <summary>A pop-up window of a page (sign-in windows): a small window with its own WebView2 in the shared profile.</summary>
    private readonly OpenFill.Core.Browser.PopupRegistry<Form> _popups = new();

    /// <summary>Discard completed task's sign-in pop-ups as well as its main page; do not leave hidden renderers running.</summary>
    private void ClosePopupsForTab(string tabId)
    {
        foreach (var popup in _popups.ForOwner(tabId))
        {
            try { popup.Close(); }
            catch (Exception ex)
            {
                _host?.Session.Log.Write("browser", "popup", "Could not close finished tab's pop-up: " + ex.Message, status: "warn");
            }
        }
    }

    private async Task OpenPopupAsync(CoreWebView2NewWindowRequestedEventArgs e, CoreWebView2Deferral deferral, int width, int height, BrowserTab opener)
    {
        try
        {
            width = Math.Clamp(width, 360, 1100); height = Math.Clamp(height, 400, 900);
            var uri = e.Uri;
            var view = new WebView2 { Dock = DockStyle.Fill };
            var form = new Form { Text = "OpenFill - pop-up window", Width = width + 16, Height = height + 39, StartPosition = FormStartPosition.CenterParent, ShowInTaskbar = true };
            try { form.Icon = Icon; } catch { }
            form.Controls.Add(view);
            var lastUrl = uri; var closedByPage = false;
            WebView2CdpConnection? pconn = null;
            // The same pop-up opened again replaces the earlier one; only a few stay open.
            foreach (var old in _popups.Add(uri, form, opener.Id))
            {
                _host?.Session.Log.Write("browser", "popup", "Pop-up window replaced by a newer one: " + (old.Text ?? ""));
                try { old.Close(); } catch { }
            }
            form.FormClosed += (_, _) =>
            {
                _popups.Remove(form);
                if (pconn is not null) { opener.TaskConn.DetachPopup(pconn); _ = pconn.DisposeAsync().AsTask(); }
                _host?.Session.Log.Write("browser", "popup", "Pop-up window closed (" + (closedByPage ? "by the page itself, e.g. the sign-in finished" : "by the person or by OpenFill") + "): " + lastUrl);
                try { view.Dispose(); } catch { }
            };
            form.Show(this);
            await view.EnsureCoreWebView2Async(_browserEnv);
            var pc = view.CoreWebView2;
            // Like a tab: dialogs are accepted, the browser may fill in saved passwords, and the task that owns the opener tab works in this window.
            pc.Settings.AreDefaultScriptDialogsEnabled = false;
            pc.Settings.IsPasswordAutosaveEnabled = true;
            pc.Settings.IsGeneralAutofillEnabled = true;
            pc.ScriptDialogOpening += (_, a) => { a.ResultText = a.DefaultText; a.Accept(); };
            pc.DocumentTitleChanged += (_, _) => { try { form.Text = pc.DocumentTitle; } catch { } };
            pc.SourceChanged += (_, _) => { try { lastUrl = pc.Source; } catch { } };
            pc.NavigationCompleted += (_, a) => _host?.Session.Log.Write("browser", "popup", "Pop-up window navigated: " + lastUrl + (a.IsSuccess ? "" : " (failed: " + a.WebErrorStatus + ")"), status: a.IsSuccess ? "info" : "warn");
            pc.WindowCloseRequested += (_, _) => { closedByPage = true; try { form.Close(); } catch { } };
            e.NewWindow = pc;
            pconn = new WebView2CdpConnection(this, pc, () => { form.Activate(); var ok = NativeInput.BringToFront(form.Handle); try { view.Focus(); } catch { } return ok ? form.Handle : IntPtr.Zero; });
            opener.TaskConn.AttachPopup(pconn, opener.Id + ":popup:" + Guid.NewGuid().ToString("N")[..6]);
            form.Activate();
            _host?.Session.Log.Write("browser", "popup", "Pop-up window opened: " + uri);
        }
        catch (Exception ex)
        {
            _host?.Session.Log.Write("browser", "popup", "Pop-up window failed: " + ex.Message, status: "error");
        }
        finally { deferral.Complete(); }
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
        // The person logs in once and lets the browser remember the password; later the fields fill themselves
        // and the model only clicks "Next". The password never goes through the model (the page model hides password fields).
        core.Settings.IsPasswordAutosaveEnabled = true;
        core.Settings.IsGeneralAutofillEnabled = true;
        // A manual account selection can answer one HTTP Basic authentication challenge without
        // sending the password to a model or relying on the browser's separate autofill store.
        core.BasicAuthenticationRequested += (_, e) =>
        {
            if (!_basicAuthChoices.TryGetValue(tab.Id, out var selected)) return;
            var requested = OpenFill.Core.Agent.ImportedCredentialVault.NormalizeOrigin(e.Uri);
            if (!string.Equals(requested, selected.Origin, StringComparison.OrdinalIgnoreCase)) return;
            if (!OpenFill.Core.Agent.BasicAuthRealmMatcher.Matches(e.Challenge, selected.Realm)) return;
            var value = OpenFill.Core.Agent.ImportedCredentialVault.Read(selected);
            if (value is null) return;
            _basicAuthChoices.TryRemove(tab.Id, out var removedChoice);
            e.Response.UserName = selected.Login;
            e.Response.Password = value;
            _host?.Session.Log.Write("browser", "basic-auth", "Submitted a selected imported HTTP Basic credential to its matching origin (secret hidden).");
        };
        core.ScriptDialogOpening += (_, e) =>
        {
            e.ResultText = e.DefaultText;
            e.Accept();
            _host?.Session.Log.Write("browser", "dialog", $"Page dialog ({e.Kind}): {e.Message} - accepted", status: "warn");
        };
        // A plain link to a new window opens in the same tab, so the task does not lose sight of its page.
        // A real pop-up (window.open with a size or position, e.g. the sign-in window of Microsoft, Google, Facebook)
        // opens as its own small window with the same profile, so that the page that opened it gets the result.
        core.NewWindowRequested += (_, e) =>
        {
            var f = e.WindowFeatures;
            if (!(f.HasSize || f.HasPosition || OpenFill.Core.Browser.SignInPopup.Looks(e.Uri))) { e.Handled = true; core.Navigate(e.Uri); return; }
            e.Handled = true;
            var deferral = e.GetDeferral();
            _ = OpenPopupAsync(e, deferral, f.HasSize ? (int)f.Width : 520, f.HasSize ? (int)f.Height : 700, tab);
        };
        core.SourceChanged += (_, _) =>
        {
            if (_addressHistory.Remember(core.Source)) RefreshAddressHistory();
            if (ReferenceEquals(_active, tab) && !_address.Focused) _address.Text = core.Source;
        };
        core.HistoryChanged += (_, _) => { if (ReferenceEquals(_active, tab)) { _back.Enabled = core.CanGoBack; _forward.Enabled = core.CanGoForward; } };
        core.DownloadStarting += (_, e) =>
        {
            var name = Path.GetFileName(e.ResultFilePath);
            e.ResultFilePath = Path.Combine(_paths.Downloads, name);
            _host?.Session.Log.Write("browser", "download", "Download: " + name, new { path = e.ResultFilePath });
        };
        tab.Conn = new WebView2CdpConnection(this, core, () => { ActivateTab(tab); var ok = NativeInput.BringToFront(Handle); try { tab.View.Focus(); } catch { } return ok ? Handle : IntPtr.Zero; });
        tab.TaskConn.Attach(tab.Conn, tab.Id);
        _tabs[tab.Id] = tab;
        if (url is not null) core.Navigate(url);
        return tab;
    }

    /// <summary>Shows the tab and points the core's connection at it. Call on the UI thread.</summary>
    private void ActivateTab(BrowserTab tab)
    {
        // Tabs are stacked, never hidden (Visible=false): a hidden WebView2 stops rendering and would not answer a task working in it.
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
        ClosePopupsForTab(tabId);
        _basicAuthChoices.TryRemove(tabId, out var unusedChoice);
        if (!_tabs.TryRemove(tabId, out var tab)) return;
        var wasActive = ReferenceEquals(_active, tab);
        if (tab.Conn is not null)
        {
            _switchable.Detach(tab.Conn);
            tab.TaskConn.Detach(tab.Conn);
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

    private IReadOnlyList<TabInfo> _lastTabs = Array.Empty<TabInfo>();
    private string? _lastActiveId;
    private int _lastStripWidth;

    private void RebuildStrip(IReadOnlyList<TabInfo> tabs, string? activeId)
    {
        _lastTabs = tabs; _lastActiveId = activeId; _lastStripWidth = _strip.Width;
        _fontBold ??= new Font(Font, FontStyle.Bold);
        // All tabs and the "+" always fit in the strip: the tabs share the width that is left (each at most 230 px, at least 60 px).
        var shown = tabs.Count(t => t.TabId is not null);
        var scale = DeviceDpi / 96.0;
        var xW = (int)(22 * scale) + 6; var plusW = (int)(30 * scale) + 8;
        var room = _strip.ClientSize.Width - _strip.Padding.Horizontal - plusW - 4;
        var tabW = shown == 0 ? 0 : Math.Clamp(room / shown - xW - 2, (int)(60 * scale), (int)(230 * scale));
        _strip.SuspendLayout();
        foreach (var c in _strip.Controls.Cast<Control>().ToList()) { _strip.Controls.Remove(c); c.Dispose(); }
        foreach (var t in tabs)
        {
            if (t.TabId is not { } id) continue;
            var glyph = t.State switch { "running" => "▶", "waiting" => "⚠", "free" => "○", "user" => "◆", _ => "✓" };
            var active = id == activeId;
            var b = new Button
            {
                Text = glyph + " " + t.Label,
                AutoSize = false,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Width = tabW,
                FlatStyle = FlatStyle.Flat,
                Height = 30,
                Margin = new Padding(1, 1, 0, 1),
                Font = active ? _fontBold : Font,
                BackColor = active ? SystemColors.Window : SystemColors.Control
            };
            b.FlatAppearance.BorderColor = t.State == "waiting" ? Color.DarkOrange : SystemColors.ControlDark;
            if (OpenFill.Core.Hosting.RequesterColors.For(t.Requester) is { } who)
            {
                // The tab carries the colour of the chat that asked for the task.
                var wc = ColorTranslator.FromHtml(who);
                // Border: the model's colour; fill: the chat's colour (or the same colour when there is no chat).
                var tc = OpenFill.Core.Hosting.RequesterColors.ForChat(t.Requester) is { } chatHex ? ColorTranslator.FromHtml(chatHex) : wc;
                Color Tint(int pct) => Color.FromArgb(255 - (255 - tc.R) * pct / 100, 255 - (255 - tc.G) * pct / 100, 255 - (255 - tc.B) * pct / 100);
                // Thick frame in the model's colour; it never changes with the task state (the glyph shows the state).
                b.FlatAppearance.BorderSize = 5;
                b.FlatAppearance.BorderColor = wc;
                b.BackColor = Tint(active ? 60 : 35);
            }
            b.Click += (_, _) => UserSelectTab(id);
            _tip.SetToolTip(b, (string.IsNullOrEmpty(t.Requester) ? "" : "From " + t.Requester + "\n") + t.Label + (string.IsNullOrEmpty(t.LastUrl) ? "" : "\n" + t.LastUrl));
            var x = new Button { Text = "✕", Width = 22, Height = 30, FlatStyle = FlatStyle.Flat, Margin = new Padding(0, 1, 6, 1) };
            x.FlatAppearance.BorderColor = SystemColors.ControlDark;
            x.Click += async (_, _) => await UserCloseTabAsync(id);
            _strip.Controls.Add(b);
            _strip.Controls.Add(x);
        }
        // "+": a tab opened by hand (for example to read a verification code in the e-mail). No task takes it over.
        var plus = new Button { Text = "+", Width = 30, Height = 30, FlatStyle = FlatStyle.Flat, Margin = new Padding(2, 1, 0, 1), Font = _fontBold };
        plus.FlatAppearance.BorderColor = SystemColors.ControlDark;
        _tip.SetToolTip(plus, "Open a new tab by hand (Ctrl+T), e.g. to check your e-mail. No task will use it.");
        plus.Click += (_, _) => NewUserTab();
        _strip.Controls.Add(plus);
        _strip.ResumeLayout();
    }

    /// <summary>Opens an empty tab for the person. Tasks never take it over or close it; the person closes it with its x.</summary>
    private async void NewUserTab()
    {
        if (_tabMgr is null || _browserEnv is null) return;
        try
        {
            if (!await _tabMgr.TryOpenUserTabAsync())
            {
                _host?.Session.Log.Write("browser", "tab_limit",
                    "Cannot open a manual tab: all tab slots are occupied by user or active tabs.",
                    status: "warn");
                return;
            }
            _address.Focus();
            _address.SelectAll();
        }
        catch (Exception ex) { _host?.Session.Log.Write("browser", "error", "Could not open a new tab: " + ex.Message, status: "error"); }
    }

    private void UserSelectTab(string id)
    {
        if (!_tabs.TryGetValue(id, out var tab)) return;
        if (ReferenceEquals(_active, tab)) return;
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
                if (url == "about:blank") f.ClosePopupsForTab(tabId);
                if (f._tabs.TryGetValue(tabId, out var tab)) tab.View.CoreWebView2?.Navigate(url);
                return Task.FromResult(true);
            });

        public Task CloseTabAsync(string tabId) => f.OnUi(async () => { await f.CloseTabCoreAsync(tabId); return true; });

        public Task<string?> UrlAsync(string tabId) =>
            f.OnUi(() => Task.FromResult(f._tabs.TryGetValue(tabId, out var tab) ? tab.View.CoreWebView2?.Source : null));

        public bool Exists(string tabId) => f._tabs.ContainsKey(tabId);

        // Every tab has its own CDP connection, so tasks in different tabs do not get in each other's way.
        public bool SupportsParallel => true;

        public ICdpConnection? ConnectionOf(string tabId) => f._tabs.TryGetValue(tabId, out var tab) ? tab.TaskConn : null;

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
            _attentionActive = false;
            _attentionTimer?.Stop();
            Text = _baseTitle;
            var stop = new FLASHWINFO { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<FLASHWINFO>(), hwnd = Handle, dwFlags = 0 };
            try { FlashWindowEx(ref stop); } catch { }
            return;
        }
        Text = "⚠ OpenFill is waiting for you — " + _baseTitle;
        if (_attentionActive) return; // already waiting: do not restart the reminder schedule
        _attentionActive = true;
        _remindersPlayed = 0;
        Notify(first: true);
        ScheduleNextReminder();
    }

    // Reminder schedule (seconds after the previous sound): gentle by design - one chime at once, then
    // a few spaced-out reminders (2 min, 5 min, 15 min, 30 min), then silence. The flashing taskbar icon
    // and the title stay until the card is answered. Being away for a bath gets you a chime; being away
    // for the evening does not get you a chime every 30 seconds.
    private static readonly int[] ReminderDelaysSec = { 120, 300, 900, 1800 };
    private bool _attentionActive;
    private int _remindersPlayed;
    private System.Media.SoundPlayer? _chimePlayer;

    private void ScheduleNextReminder()
    {
        if (!_attentionActive || _remindersPlayed >= ReminderDelaysSec.Length) { _attentionTimer?.Stop(); return; }
        if (_attentionTimer is null)
        {
            _attentionTimer = new System.Windows.Forms.Timer();
            _attentionTimer.Tick += (_, _) =>
            {
                _attentionTimer!.Stop();
                if (!_attentionActive) return;
                _remindersPlayed++;
                Notify(first: false);
                ScheduleNextReminder();
            };
        }
        _attentionTimer.Interval = ReminderDelaysSec[_remindersPlayed] * 1000;
        _attentionTimer.Start();
    }

    /// <summary>The person looked at the window: the card is seen, so no more reminder sounds.</summary>
    private void AcknowledgeAttention()
    {
        if (!_attentionActive) return;
        _remindersPlayed = ReminderDelaysSec.Length;
        _attentionTimer?.Stop();
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    private static TimeSpan IdleTime()
    {
        try
        {
            var info = new LASTINPUTINFO { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<LASTINPUTINFO>() };
            if (!GetLastInputInfo(ref info)) return TimeSpan.Zero;
            return TimeSpan.FromMilliseconds(unchecked((uint)Environment.TickCount - info.dwTime));
        }
        catch { return TimeSpan.Zero; }
    }

    /// <summary>A soft chime (only when the person is not looking at the window, or has been idle for a minute) and a flashing taskbar icon.</summary>
    private void Notify(bool first)
    {
        bool lookingAtIt = ReferenceEquals(Form.ActiveForm, this) && IdleTime() < TimeSpan.FromSeconds(60);
        if (!lookingAtIt)
        {
            try
            {
                _chimePlayer?.Dispose();
                _chimePlayer = new System.Media.SoundPlayer(new System.IO.MemoryStream(Chime.Wav));
                _chimePlayer.Play();
            }
            catch { }
        }
        if (!first) return; // the flashing icon keeps going on its own until the window is brought forward
        try
        {
            var f = new FLASHWINFO { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<FLASHWINFO>(), hwnd = Handle, dwFlags = 3 | 12, uCount = uint.MaxValue, dwTimeout = 0 };
            FlashWindowEx(ref f);
        }
        catch { }
    }

    /// <summary>Our own notification sound, synthesised in memory (no file, independent of the Windows sound scheme):
    /// two soft sine notes, a rising fifth (E5 then B5), quick attack and a smooth decay, about one second in total.</summary>
    private static class Chime
    {
        public static readonly byte[] Wav = Build();

        private static byte[] Build()
        {
            const int rate = 44100;
            const double total = 1.1;
            int n = (int)(rate * total);
            var pcm = new short[n];
            (double freq, double start)[] notes = { (659.25, 0.0), (987.77, 0.22) };
            foreach (var (freq, start) in notes)
            {
                int s0 = (int)(start * rate);
                for (int i = s0; i < n; i++)
                {
                    double t = (i - s0) / (double)rate;
                    double attack = Math.Min(1.0, t / 0.012);
                    double decay = Math.Exp(-t * 4.2);
                    double tone = Math.Sin(2 * Math.PI * freq * t) + 0.18 * Math.Sin(2 * Math.PI * freq * 2 * t);
                    double v = tone * attack * decay * 0.20;
                    pcm[i] = (short)Math.Clamp(pcm[i] + v * short.MaxValue, short.MinValue, short.MaxValue);
                }
            }
            // short fade-out so the last samples never click
            int fade = rate / 50;
            for (int i = 0; i < fade; i++) pcm[n - 1 - i] = (short)(pcm[n - 1 - i] * (i / (double)fade));
            using var ms = new System.IO.MemoryStream();
            using (var w = new System.IO.BinaryWriter(ms, System.Text.Encoding.ASCII, leaveOpen: true))
            {
                int dataLen = n * 2;
                w.Write("RIFF".ToCharArray()); w.Write(36 + dataLen); w.Write("WAVE".ToCharArray());
                w.Write("fmt ".ToCharArray()); w.Write(16); w.Write((short)1); w.Write((short)1);
                w.Write(rate); w.Write(rate * 2); w.Write((short)2); w.Write((short)16);
                w.Write("data".ToCharArray()); w.Write(dataLen);
                foreach (var s in pcm) w.Write(s);
            }
            return ms.ToArray();
        }
    }

    private async void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_closing || _host is null) return;
        e.Cancel = true;
        _closing = true;
        // Show the update notice also when the installer closes the window (without the operator dialog).
        var updateNotice = new Label
        {
            Text = McpTaskManager.UpgradeNotice,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.White,
            ForeColor = Color.Black,
            Font = new Font(Font.FontFamily, 18f, FontStyle.Bold)
        };
        Controls.Add(updateNotice);
        updateNotice.BringToFront();
        updateNotice.Refresh();
        try { _mcp?.Tasks.PrepareShutdown(_shutdownReason, _upgradeDowntimeMinutes); }
        catch (Exception ex)
        {
            _closing = false;
            Controls.Remove(updateNotice);
            updateNotice.Dispose();
            MessageBox.Show(this,
                "OpenFill could not verify that all interrupted tasks were saved. Shutdown has been cancelled. " +
                "Do not deploy until the task history is durable. Details: " + ex.Message,
                "OpenFill: maintenance interrupted", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        // The MCP server uses JSON-RPC long polling: allow pending status replies to observe
        // persisted interrupted notices before closing the HTTP transport.
        if (_mcp is not null) { try { await Task.Delay(1500); } catch { } }
        try { _telemetry?.Dispose(); _telemetry = null; } catch { }
        try { if (_mcp is not null) await _mcp.DisposeAsync(); } catch { }
        try { await Task.WhenAny(_host.DisposeAsync().AsTask(), Task.Delay(4000)); } catch { }
        _host = null;
        Close();
    }
}
