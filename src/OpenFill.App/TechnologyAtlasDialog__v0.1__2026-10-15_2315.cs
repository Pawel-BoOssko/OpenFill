// OpenFill Technology Atlas dialog v0.1, 2026-10-15 23:15; read-only.
using System.Text;
using OpenFill.Core;

namespace OpenFill.App;

internal sealed class TechnologyAtlasDialog : Form
{
    private readonly TextBox _search = new() { Dock = DockStyle.Top, PlaceholderText = "Search technology, use, English term or interview answer..." };
    private readonly TreeView _index = new() { Dock = DockStyle.Fill, HideSelection = false, ShowLines = false, BorderStyle = BorderStyle.None };
    private readonly RichTextBox _detail = new() { Dock = DockStyle.Fill, ReadOnly = true, BorderStyle = BorderStyle.None, DetectUrls = false, Font = new Font("Segoe UI", 11) };
    private readonly Label _count = new() { AutoSize = true, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Button _copy = new() { Text = "Copy explanation", Width = 150 };
    private readonly Button _close = new() { Text = "Close", Width = 90 };

    public TechnologyAtlasDialog()
    {
        Text = "OpenFill — Technology Atlas / Interview Preparation";
        Width = 1130; Height = 720; MinimumSize = new Size(780, 470);
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 10);
        var heading = new Label
        {
            Text = "What OpenFill is built with — how each technology works and how to explain it",
            Dock = DockStyle.Top, Height = 42, Font = new Font("Segoe UI", 13, FontStyle.Bold)
        };
        var intro = new Label
        {
            Text = "Facts verified from OpenFill source code. Technical vocabulary + your concrete implementation. " +
                   "Secrets, individual accounts, passwords and private MCP URLs are intentionally omitted.",
            Dock = DockStyle.Top, Height = 45
        };
        var tools = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 47, FlowDirection = FlowDirection.RightToLeft };
        tools.Controls.Add(_close);
        tools.Controls.Add(_copy);
        tools.Controls.Add(_count);
        var browser = new SplitContainer { Width = 1080, Height = 540, Dock = DockStyle.Fill, Panel1MinSize = 220, Panel2MinSize = 360, SplitterDistance = 330, FixedPanel = FixedPanel.Panel1 };
        browser.Panel1.Controls.Add(_index);
        browser.Panel2.Controls.Add(_detail);
        Controls.Add(browser);
        Controls.Add(_search);
        Controls.Add(intro);
        Controls.Add(heading);
        Controls.Add(tools);
        _search.TextChanged += (_, _) => Rebuild();
        _index.AfterSelect += (_, e) => ShowNode(e.Node);
        _copy.Click += (_, _) => Clipboard.SetText(_detail.Text);
        _close.Click += (_, _) => Close();
        Shown += (_, _) => Rebuild();
    }

    private void Rebuild()
    {
        var find = _search.Text.Trim();
        var oldName = _index.SelectedNode?.Name;
        _index.BeginUpdate();
        _index.Nodes.Clear();
        var filtered = TechnologyAtlas.Topics
            .Select((topic, index) => (topic, index))
            .Where(x => find.Length == 0 ||
                (x.topic.Category + " " + x.topic.Name + " " + x.topic.Technology + " " +
                 x.topic.Component + " " + x.topic.Explanation + " " + x.topic.Interview)
                    .Contains(find, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        foreach (var group in filtered.GroupBy(x => x.topic.Category))
        {
            var category = new TreeNode(group.Key);
            foreach (var item in group)
                category.Nodes.Add(new TreeNode(item.topic.Name) { Name = item.index.ToString(), Tag = item.topic });
            _index.Nodes.Add(category);
        }
        _index.ExpandAll();
        _index.EndUpdate();
        _count.Text = $"{filtered.Length} topics  |  .NET runtime {Environment.Version}";
        TreeNode? restored = null;
        foreach (TreeNode parent in _index.Nodes)
            foreach (TreeNode node in parent.Nodes)
                if (node.Name == oldName) restored = node;
        _index.SelectedNode = restored ?? (_index.Nodes.Count == 0 ? null : _index.Nodes[0].FirstNode);
        if (_index.SelectedNode is null) _detail.Text = "No matches. Try a broader technology name.";
    }

    private void ShowNode(TreeNode? node)
    {
        if (node?.Tag is not TechnologyAtlas.Topic topic) return;
        var content = new StringBuilder()
            .AppendLine(topic.Name.ToUpperInvariant())
            .AppendLine(new string('─', 50)).AppendLine()
            .AppendLine("Technology / English term")
            .AppendLine(topic.Technology).AppendLine()
            .AppendLine("What it does in OpenFill")
            .AppendLine(topic.Explanation).AppendLine()
            .AppendLine("Where to find it")
            .AppendLine(topic.Component).AppendLine()
            .AppendLine("How to explain this in an interview")
            .AppendLine(topic.Interview).AppendLine()
            .AppendLine("Architecture at a glance")
            .AppendLine("ChatGPT / Claude / Networking Radar")
            .AppendLine("  -> MCP (HTTP / JSON-RPC)")
            .AppendLine("  -> OpenFill C# / .NET task manager + Luna")
            .AppendLine("  -> CDP + embedded Chromium / Edge WebView2")
            .AppendLine("  -> the real website and verified result")
            .AppendLine("  -> persistent status, logs and domain notes").ToString();
        _detail.Text = content;
        _detail.SelectionStart = 0;
        _detail.ScrollToCaret();
    }
}