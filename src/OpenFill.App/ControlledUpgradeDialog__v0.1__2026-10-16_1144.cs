using System.Drawing;
using System.Windows.Forms;

namespace OpenFill.App;

/// <summary>Operator-only controlled upgrade. Does not send credentials or close the process itself.</summary>
internal sealed class ControlledUpgradeDialog : Form
{
    private readonly NumericUpDown _minutes = new()
    {
        Minimum = 1, Maximum = 120, Value = 10, Width = 90, Dock = DockStyle.Left
    };

    public int EstimatedMinutes => (int)_minutes.Value;

    public ControlledUpgradeDialog()
    {
        Text = "OpenFill - controlled shutdown / upgrade";
        Width = 550;
        Height = 275;
        MinimumSize = new Size(470, 250);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            ColumnCount = 1,
            RowCount = 4
        };
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        content.Controls.Add(new Label
        {
            Text = "Stop OpenFill for an upgrade. ALL ongoing OpenFill tasks will be interrupted. " +
                   "Clients currently waiting for a task status may receive the upgrade notice.",
            Dock = DockStyle.Fill,
            AutoSize = true,
            MaximumSize = new Size(490, 0)
        }, 0, 0);

        var estimate = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = false };
        estimate.Controls.Add(new Label { Text = "Estimated downtime (minutes):", AutoSize = true, Margin = new Padding(0, 8, 8, 0) });
        estimate.Controls.Add(_minutes);
        content.Controls.Add(estimate, 0, 1);

        content.Controls.Add(new Label
        {
            Text = "Other clients may already be disconnected and cannot be notified proactively. " +
                   "They can check persisted interrupted task status after OpenFill returns. " +
                   "Do not repeat purchases, submissions or messages without checking previous results.",
            AutoSize = true,
            Dock = DockStyle.Fill,
            MaximumSize = new Size(490, 0)
        }, 0, 2);

        var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var proceed = new Button { Text = "Interrupt tasks and close", DialogResult = DialogResult.OK, AutoSize = true };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
        buttons.Controls.Add(proceed);
        buttons.Controls.Add(cancel);
        content.Controls.Add(buttons, 0, 3);

        AcceptButton = proceed;
        CancelButton = cancel;
        Controls.Add(content);
    }
}