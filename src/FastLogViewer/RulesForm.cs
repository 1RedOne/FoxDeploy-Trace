namespace FastLogViewer;

internal sealed class RulesForm : Form
{
    private readonly DataGridView _grid;
    public List<HighlightRule> Rules { get; private set; } = [];

    public RulesForm(IEnumerable<HighlightRule> rules)
    {
        Text = "Highlight rules";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(600, 350);
        ClientSize = new Size(730, 420);
        ShowInTaskbar = false;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            RowCount = 3,
            ColumnCount = 1
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        layout.Controls.Add(new Label
        {
            Text = "Case-insensitive text matches. First matching enabled rule wins.\r\nUse Up / Down to change priority.",
            Dock = DockStyle.Fill
        }, 0, 0);

        _grid = new DataGridView
        {
            Name = "RulesGrid",
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            RowHeadersVisible = false,
            MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = SystemColors.Window
        };
        _grid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            Name = "Enabled",
            HeaderText = "On",
            FillWeight = 15
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "MatchText",
            HeaderText = "Text to match",
            FillWeight = 65,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });
        var colors = new DataGridViewComboBoxColumn
        {
            Name = "HighlightColor",
            HeaderText = "Row color",
            FillWeight = 30,
            FlatStyle = FlatStyle.Flat
        };
        colors.Items.AddRange(DisplaySettings.ColorNames);
        _grid.Columns.Add(colors);
        foreach (var rule in rules)
        {
            _grid.Rows.Add(rule.Enabled, rule.Text, rule.ColorName);
        }
        _grid.CellFormatting += (_, e) =>
        {
            if (e.ColumnIndex == 2 && e.Value is string colorName && e.CellStyle is not null)
            {
                e.CellStyle.BackColor = DisplaySettings.GetColor(colorName);
                e.CellStyle.ForeColor = Color.Black;
            }
        };
        layout.Controls.Add(_grid, 0, 1);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 0) };
        AddButton(buttons, "Add", () =>
        {
            var row = _grid.Rows.Add(true, "", "Yellow");
            _grid.CurrentCell = _grid.Rows[row].Cells[1];
            _grid.BeginEdit(true);
        });
        AddButton(buttons, "Remove", () =>
        {
            if (_grid.CurrentRow is { } row)
            {
                _grid.Rows.Remove(row);
            }
        });
        AddButton(buttons, "Up", () => MoveRule(-1));
        AddButton(buttons, "Down", () => MoveRule(1));
        var apply = AddButton(buttons, "Apply", Apply);
        var cancel = AddButton(buttons, "Cancel", () => DialogResult = DialogResult.Cancel);
        AcceptButton = apply;
        CancelButton = cancel;
        layout.Controls.Add(buttons, 0, 2);
        Controls.Add(layout);
    }

    private static Button AddButton(Control parent, string text, Action action)
    {
        var button = new Button { Text = text, AutoSize = true };
        button.Click += (_, _) => action();
        parent.Controls.Add(button);
        return button;
    }

    private void MoveRule(int delta)
    {
        _grid.EndEdit();
        if (_grid.CurrentRow is not { } row)
        {
            return;
        }
        var target = row.Index + delta;
        if (target < 0 || target >= _grid.Rows.Count)
        {
            return;
        }
        _grid.Rows.Remove(row);
        _grid.Rows.Insert(target, row);
        _grid.CurrentCell = row.Cells[1];
    }

    private void Apply()
    {
        _grid.EndEdit();
        var rules = new List<HighlightRule>();
        foreach (DataGridViewRow row in _grid.Rows)
        {
            var text = Convert.ToString(row.Cells[1].Value);
            var color = Convert.ToString(row.Cells[2].Value);
            if (string.IsNullOrWhiteSpace(text) || !DisplaySettings.ColorNames.Contains(color))
            {
                MessageBox.Show(this, "Every rule needs non-empty text and a color.", "Invalid rule",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _grid.CurrentCell = row.Cells[1];
                return;
            }
            rules.Add(new HighlightRule(text, color!, row.Cells[0].Value is true));
        }
        Rules = rules;
        DialogResult = DialogResult.OK;
    }
}
