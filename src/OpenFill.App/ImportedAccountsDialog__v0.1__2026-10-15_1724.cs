// OpenFill - Metadata: version 0.1, 2026-10-15 17:24
using OpenFill.Core.Agent;

namespace OpenFill.App;

/// <summary>Read-only list of accounts imported into the OpenFill Windows credential vault.
/// The UI never requests or displays passwords or credential targets.</summary>
internal sealed class ImportedAccountsDialog : Form
{
    private readonly TextBox _search = new()
    {
        Dock = DockStyle.Top, PlaceholderText = "Search website or login..."
    };
    private readonly ListView _list = new()
    {
        Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true,
        MultiSelect = false, HideSelection = false
    };
    private readonly Label _count = new() { Dock = DockStyle.Top, Height = 27 };
    private readonly Button _use = new() { Text = "Use selected", Width = 150, Enabled = false };
    public ImportedCredentialVault.Account? SelectedAccount { get; private set; }
    private readonly Button _refresh = new() { Text = "Refresh", Width = 100 };
    private readonly Button _close = new() { Text = "Close", Width = 100 };
    private IReadOnlyList<ImportedCredentialVault.Account> _accounts =
        Array.Empty<ImportedCredentialVault.Account>();

    public ImportedAccountsDialog()
    {
        Text = "OpenFill - imported accounts";
        Width = 1150;
        Height = 540;
        MinimumSize = new Size(850, 350);
        StartPosition = FormStartPosition.CenterParent;

        _list.Columns.Add("Website", 260);
        _list.Columns.Add("Device / realm", 235);
        _list.Columns.Add("Login", 180);
        _list.Columns.Add("Created", 130);
        _list.Columns.Add("Last used", 130);
        _list.Columns.Add("Changed", 130);

        var explanation = new Label
        {
            Dock = DockStyle.Top, Height = 48,
            Text = "Accounts imported into OpenFill's Windows Credential Manager. " +
                   "Only website origins, device labels and login names are displayed. Select a credential to use in HTTP Basic authentication on the currently open website. Passwords stay hidden."
        };
        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom, Height = 46,
            FlowDirection = FlowDirection.RightToLeft
        };
        footer.Controls.Add(_close);
        footer.Controls.Add(_refresh);
        footer.Controls.Add(_use);
        Controls.Add(_list);
        Controls.Add(_count);
        Controls.Add(_search);
        Controls.Add(explanation);
        Controls.Add(footer);

        _search.TextChanged += (_, _) => Render();
        _refresh.Click += (_, _) => Reload();
        _close.Click += (_, _) => Close();
        _list.SelectedIndexChanged += (_, _) => _use.Enabled = _list.SelectedItems.Count > 0;
        _use.Click += (_, _) =>
        {
            SelectedAccount = _list.SelectedItems.Count > 0 ? _list.SelectedItems[0].Tag as ImportedCredentialVault.Account : null;
            if (SelectedAccount is not null) { DialogResult = DialogResult.OK; Close(); }
        };
        Shown += (_, _) => Reload();
    }

    private void Reload()
    {
        try { _accounts = ImportedCredentialVault.ListAllMetadata(); }
        catch (Exception ex)
        {
            _accounts = Array.Empty<ImportedCredentialVault.Account>();
            MessageBox.Show(this, "Cannot read account metadata: " + ex.Message,
                "OpenFill", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        Render();
    }

    private static string DateOf(long ms) => ms > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(ms).ToLocalTime().ToString("yyyy-MM-dd HH:mm") : "-";

    private void Render()
    {
        var filter = _search.Text.Trim();
        _list.BeginUpdate();
        try
        {
            _list.Items.Clear();
            foreach (var account in _accounts)
            {
                if (filter.Length > 0 &&
                    !account.Origin.Contains(filter, StringComparison.OrdinalIgnoreCase) &&
                    !account.Login.Contains(filter, StringComparison.OrdinalIgnoreCase) &&
                    !(account.Realm?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false)) continue;
                var row = new ListViewItem(account.Origin) { Tag = account };
                row.SubItems.Add(account.Realm ?? "(no device label)");
                row.SubItems.Add(string.IsNullOrEmpty(account.Login) ? "(no login)" : account.Login);
                row.SubItems.Add(DateOf(account.CreatedMillis));
                row.SubItems.Add(DateOf(account.LastUsedMillis));
                row.SubItems.Add(DateOf(account.ChangedMillis));
                _list.Items.Add(row);
            }
        }
        finally { _list.EndUpdate(); }
        _count.Text = _list.Items.Count + " matching accounts (total imported: " + _accounts.Count + ")";
    }
}
