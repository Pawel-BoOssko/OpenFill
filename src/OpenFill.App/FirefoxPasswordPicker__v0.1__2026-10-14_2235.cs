// OpenFill - Metadata: wersja 0.1, data 2026-10-14 22:35
// OpenFill - Metadata: version 0.2, 2026-10-14 22:24
using System.Text;
using OpenFill.Core.Agent;

namespace OpenFill.App;

/// <summary>Lets the person select one or many Firefox credentials and approve one batch import.</summary>
internal sealed class FirefoxPasswordPicker : Form
{
    private readonly TextBox _search = new() { Dock = DockStyle.Top, PlaceholderText = "Search website or login..." };
    private readonly ListView _list = new()
    {
        Dock = DockStyle.Fill,
        FullRowSelect = true,
        MultiSelect = true,
        CheckBoxes = true,
        View = View.Details,
        HideSelection = false
    };
    private readonly Label _count = new() { Text = "Opening Firefox credential list...", Dock = DockStyle.Top, Height = 26 };
    private readonly Button _import = new() { Text = "Import checked accounts", Enabled = false, Width = 190, Height = 34 };
    private readonly Button _selectVisible = new() { Text = "Check visible", Width = 110, Height = 34 };
    private readonly Button _clear = new() { Text = "Clear selection", Width = 115, Height = 34 };
    private readonly Button _cancel = new() { Text = "Cancel", Width = 90, Height = 34 };
    private readonly List<(FirefoxLogins.Item Entry, string Login)> _entries = new();
    private readonly HashSet<int> _checked = new();
    private FirefoxNssSession? _nss;
    private bool _rebuilding;

    public FirefoxPasswordPicker(string? websiteUrl)
    {
        Text = "OpenFill - import saved Firefox passwords";
        Width = 1190;
        Height = 600;
        MinimumSize = new Size(850, 440);
        StartPosition = FormStartPosition.CenterParent;

        _list.Columns.Add("Website", 260);
        _list.Columns.Add("Device / realm", 280);
        _list.Columns.Add("Login", 180);
        _list.Columns.Add("Created", 135);
        _list.Columns.Add("Last used", 135);
        _list.Columns.Add("Changed", 135);

        var header = new Label
        {
            Dock = DockStyle.Top,
            Height = 72,
            Text = "Check any accounts you want to import. You can change the search/domain filter and keep checking more accounts.\n" +
                   "One confirmation imports the whole checked set. Passwords are never shown here, sent to Luna, or included in diagnostic logs."
        };

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 48,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };
        footer.Controls.Add(_cancel);
        footer.Controls.Add(_import);
        footer.Controls.Add(_clear);
        footer.Controls.Add(_selectVisible);

        Controls.Add(_list);
        Controls.Add(_count);
        Controls.Add(_search);
        Controls.Add(header);
        Controls.Add(footer);

        _cancel.Click += (_, _) => Close();
        _search.TextChanged += (_, _) => RebuildList();
        _list.ItemCheck += (_, e) =>
        {
            if (_rebuilding) return;
            if (_list.Items[e.Index].Tag is not int sourceIndex) return;
            if (e.NewValue == CheckState.Checked) _checked.Add(sourceIndex);
            else _checked.Remove(sourceIndex);
            UpdateSelectionUi();
        };
        _list.DoubleClick += (_, _) =>
        {
            foreach (ListViewItem item in _list.SelectedItems)
                item.Checked = !item.Checked;
        };
        _selectVisible.Click += (_, _) =>
        {
            foreach (ListViewItem item in _list.Items)
                item.Checked = true;
        };
        _clear.Click += (_, _) =>
        {
            _checked.Clear();
            RebuildList();
        };
        _import.Click += (_, _) => Import();
        Shown += (_, _) => LoadEntries(websiteUrl);
        FormClosed += (_, _) => _nss?.Dispose();
    }

    private bool UnlockPrimaryPassword()
    {
        if (_nss is null || !_nss.RequiresPrimaryPassword) return true;
        if (_nss.Unlock("")) return true;

        using var dialog = new Form
        {
            Text = "Firefox primary password",
            Width = 430,
            Height = 180,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false
        };
        var info = new Label
        {
            Dock = DockStyle.Top,
            Height = 50,
            Text = "Firefox has a primary password. Enter it to view account names and import selected credentials."
        };
        var password = new TextBox { Dock = DockStyle.Top, UseSystemPasswordChar = true };
        var ok = new Button { Text = "Unlock", DialogResult = DialogResult.OK, Dock = DockStyle.Bottom, Height = 34 };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Dock = DockStyle.Bottom, Height = 34 };

        dialog.Controls.Add(password);
        dialog.Controls.Add(info);
        dialog.Controls.Add(cancel);
        dialog.Controls.Add(ok);
        dialog.AcceptButton = ok;

        if (dialog.ShowDialog(this) != DialogResult.OK) return false;
        if (_nss.Unlock(password.Text)) return true;

        MessageBox.Show(this, "Incorrect Firefox primary password.", "OpenFill", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return false;
    }

    private void LoadEntries(string? currentUrl)
    {
        try
        {
            var profile = FirefoxLogins.FindProfile();
            if (profile is null) throw new FileNotFoundException("No Firefox profile with saved logins found");

            var list = FirefoxLogins.List(profile);
            _nss = new FirefoxNssSession(profile);
            if (!UnlockPrimaryPassword())
            {
                Close();
                return;
            }

            foreach (var item in list)
            {
                try
                {
                    var name = _nss.Decrypt(item.EncryptedUsername);
                    _entries.Add((item, name));
                }
                catch
                {
                    _entries.Add((item, "(username unavailable)"));
                }
            }

            if (Uri.TryCreate(currentUrl, UriKind.Absolute, out var active))
                _search.Text = active.Host;

            RebuildList();
            _search.Focus();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Firefox import is unavailable: " + ex.Message,
                "OpenFill", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            Close();
        }
    }

    private static string DateOf(long ms) => ms > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(ms).ToLocalTime().ToString("yyyy-MM-dd HH:mm") : "-";

    private void RebuildList()
    {
        _rebuilding = true;
        _list.BeginUpdate();
        try
        {
            _list.Items.Clear();
            var filter = _search.Text.Trim();
            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                if (filter.Length > 0 &&
                    !entry.Entry.Host.Contains(filter, StringComparison.OrdinalIgnoreCase) &&
                    !(entry.Entry.Realm?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false) &&
                    !entry.Login.Contains(filter, StringComparison.OrdinalIgnoreCase))
                    continue;

                var row = new ListViewItem(entry.Entry.Host) { Tag = i, Checked = _checked.Contains(i) };
                row.SubItems.Add(entry.Entry.Realm ?? "(no device label)");
                row.SubItems.Add(string.IsNullOrEmpty(entry.Login) ? "(no login)" : entry.Login);
                row.SubItems.Add(DateOf(entry.Entry.CreatedMillis));
                row.SubItems.Add(DateOf(entry.Entry.LastUsedMillis));
                row.SubItems.Add(DateOf(entry.Entry.ChangedMillis));
                _list.Items.Add(row);
            }
        }
        finally
        {
            _list.EndUpdate();
            _rebuilding = false;
        }
        UpdateSelectionUi();
    }

    private void UpdateSelectionUi()
    {
        _count.Text = _list.Items.Count + " matching accounts (total Firefox logins: " + _entries.Count +
                      "; checked across all filters: " + _checked.Count + ")";
        _import.Enabled = _checked.Count > 0;
        _import.Text = _checked.Count > 0 ? "Import checked (" + _checked.Count + ")" : "Import checked accounts";
        _clear.Enabled = _checked.Count > 0;
        _selectVisible.Enabled = _list.Items.Count > 0;
    }

    private void Import()
    {
        if (_nss is null || _checked.Count == 0) return;

        var selected = _checked.OrderBy(i => _entries[i].Entry.Host, StringComparer.OrdinalIgnoreCase)
            .ThenBy(i => _entries[i].Login, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var preview = new StringBuilder();
        foreach (var index in selected)
        {
            var chosen = _entries[index];
            preview.AppendLine(chosen.Entry.Host + " | " + (chosen.Entry.Realm ?? "(no device label)") + " | " + (string.IsNullOrEmpty(chosen.Login) ? "(no login)" : chosen.Login) + " | used: " + DateOf(chosen.Entry.LastUsedMillis));
        }

        var confirmation = MessageBox.Show(this,
            "Import " + selected.Count + " checked Firefox account(s)?\n\n" + preview +
            "\nPasswords will be saved to Windows Credential Manager for use by OpenFill." +
            "\nPasswords will not appear in chat or logs.",
            "Confirm Firefox credential import", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

        if (confirmation != DialogResult.Yes) return;

        var savedCount = 0;
        var failures = new List<string>();
        var successes = new List<string>();
        foreach (var index in selected)
        {
            var chosen = _entries[index];
            try
            {
                var username = chosen.Login == "(username unavailable)"
                    ? _nss.Decrypt(chosen.Entry.EncryptedUsername)
                    : chosen.Login;
                // HTTP Basic can legitimately have an empty username.

                var secret = _nss.Decrypt(chosen.Entry.EncryptedPassword);
                var saved = ImportedCredentialVault.Save(chosen.Entry.Host, username, secret,
                    chosen.Entry.Realm, chosen.Entry.Id, chosen.Entry.CreatedMillis,
                    chosen.Entry.LastUsedMillis, chosen.Entry.ChangedMillis);
                if (!saved.Saved) throw new InvalidOperationException(saved.Message);
                savedCount++;
                successes.Add(saved.Message);
            }
            catch (Exception ex)
            {
                failures.Add(chosen.Entry.Host + " | " + chosen.Login + ": " + ex.Message);
            }
        }

        if (failures.Count == 0)
        {
            MessageBox.Show(this,
                "Imported " + savedCount + " account(s).\n\nPasswords are stored in Windows Credential Manager. " +
                "Luna can list available logins and fill the selected imported credential.\n\nSaved and verified:\n" + string.Join("\n", successes),
                "OpenFill - import complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
            Close();
            return;
        }

        MessageBox.Show(this,
            "Imported " + savedCount + " of " + selected.Count + " account(s).\n\nSaved and verified:\n" + string.Join("\n", successes) + "\n\nFailed:\n" + string.Join("\n", failures),
            "OpenFill - import partially complete", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
}
