using System.ComponentModel;

namespace UPKPro.Controls;

/// <summary>Pengganti TRadioGroup Delphi: GroupBox berisi daftar RadioButton dari Items.</summary>
public class RadioGroup : GroupBox
{
    private string[] _items = [];
    private int _itemIndex = -1;
    private int _columns = 1;
    private readonly List<RadioButton> _buttons = [];
    private readonly TableLayoutPanel _table;
    private bool _updating;

    public event EventHandler? SelectedIndexChanged;

    public RadioGroup()
    {
        // TableLayoutPanel menangani penskalaan DPI dan perubahan ukuran secara otomatis.
        _table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = new Padding(4, 0, 0, 0),
            BackColor = Color.Transparent,
        };
        Controls.Add(_table);
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    [Editor("System.Windows.Forms.Design.StringArrayEditor, System.Design", "System.Drawing.Design.UITypeEditor, System.Drawing")]
    public string[] Items
    {
        get => _items;
        set
        {
            _items = value ?? [];
            RebuildButtons();
        }
    }

    [DefaultValue(-1)]
    public int ItemIndex
    {
        get => _itemIndex;
        set
        {
            if (value < -1 || value >= _items.Length)
                value = -1;
            if (_itemIndex == value)
                return;
            _itemIndex = value;
            _updating = true;
            for (int i = 0; i < _buttons.Count; i++)
                _buttons[i].Checked = i == _itemIndex;
            _updating = false;
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    [DefaultValue(1)]
    public int Columns
    {
        get => _columns;
        set
        {
            _columns = Math.Max(1, value);
            RebuildButtons();
        }
    }

    private void RebuildButtons()
    {
        _table.SuspendLayout();
        foreach (var b in _buttons)
            b.Dispose();
        _buttons.Clear();
        _table.Controls.Clear();
        _table.ColumnStyles.Clear();
        _table.RowStyles.Clear();

        int rows = Math.Max(1, (int)Math.Ceiling(_items.Length / (double)_columns));
        _table.ColumnCount = _columns;
        _table.RowCount = rows;
        for (int c = 0; c < _columns; c++)
            _table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / _columns));
        for (int r = 0; r < rows; r++)
            _table.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / rows));

        for (int i = 0; i < _items.Length; i++)
        {
            int index = i;
            var rb = new RadioButton
            {
                Text = _items[i],
                AutoSize = false,
                Dock = DockStyle.Fill,
                Margin = Padding.Empty,
                Checked = i == _itemIndex,
                TabStop = i == 0,
                UseVisualStyleBackColor = true,
            };
            rb.CheckedChanged += (_, _) =>
            {
                if (!_updating && rb.Checked)
                    ItemIndex = index;
            };
            _buttons.Add(rb);
            // Urutan Delphi: isi kolom pertama dari atas ke bawah, lalu kolom berikutnya.
            _table.Controls.Add(rb, i / rows, i % rows);
        }
        if (_itemIndex >= _items.Length)
            _itemIndex = -1;
        _table.ResumeLayout();
    }
}
