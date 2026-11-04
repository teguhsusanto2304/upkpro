namespace UPKPro.Controls;

public enum NavigatorButton
{
    First,
    Prior,
    Next,
    Last,
    Insert,
    Delete,
    Edit,
    Post,
    Cancel,
    Refresh,
}

/// <summary>
/// Pengganti TDBNavigator Delphi. Jika <see cref="BindingSource"/> diisi, tombol
/// navigasi langsung menggerakkan posisi data; event <see cref="ButtonClick"/> selalu dipanggil.
/// </summary>
public class DbNavigator : UserControl
{
    private static readonly (NavigatorButton Button, string Glyph, string Hint)[] Buttons =
    [
        (NavigatorButton.First, "|◄", "Data pertama"),
        (NavigatorButton.Prior, "◄", "Data sebelumnya"),
        (NavigatorButton.Next, "►", "Data berikutnya"),
        (NavigatorButton.Last, "►|", "Data terakhir"),
        (NavigatorButton.Insert, "+", "Tambah"),
        (NavigatorButton.Delete, "−", "Hapus"),
        (NavigatorButton.Edit, "▲", "Ubah"),
        (NavigatorButton.Post, "✓", "Simpan"),
        (NavigatorButton.Cancel, "✗", "Batal"),
        (NavigatorButton.Refresh, "↻", "Muat ulang"),
    ];

    private readonly List<Button> _buttons = [];
    private readonly ToolTip _toolTip = new();

    public event EventHandler<NavigatorButtonEventArgs>? ButtonClick;

    public DbNavigator()
    {
        foreach (var (button, glyph, hint) in Buttons)
        {
            var b = new Button { Text = glyph, Tag = button, TabStop = false, FlatStyle = FlatStyle.System };
            b.Click += (_, _) => OnButtonClick(button);
            _toolTip.SetToolTip(b, hint);
            _buttons.Add(b);
            Controls.Add(b);
        }
    }

    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public BindingSource? BindingSource { get; set; }

    private void OnButtonClick(NavigatorButton button)
    {
        var bs = BindingSource;
        if (bs != null)
        {
            switch (button)
            {
                case NavigatorButton.First: bs.MoveFirst(); break;
                case NavigatorButton.Prior: bs.MovePrevious(); break;
                case NavigatorButton.Next: bs.MoveNext(); break;
                case NavigatorButton.Last: bs.MoveLast(); break;
                case NavigatorButton.Cancel: bs.CancelEdit(); break;
                case NavigatorButton.Post: bs.EndEdit(); break;
            }
        }
        ButtonClick?.Invoke(this, new NavigatorButtonEventArgs(button));
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        if (_buttons.Count == 0)
            return;
        int w = Width / _buttons.Count;
        for (int i = 0; i < _buttons.Count; i++)
        {
            int right = i == _buttons.Count - 1 ? Width : (i + 1) * w;
            _buttons[i].Bounds = new Rectangle(i * w, 0, right - i * w, Height);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _toolTip.Dispose();
        base.Dispose(disposing);
    }
}

public sealed class NavigatorButtonEventArgs(NavigatorButton button) : EventArgs
{
    public NavigatorButton Button { get; } = button;
}
