using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace UPKPro.Controls;

public enum PanelBevel
{
    None,
    Lowered,
    Raised,
}

/// <summary>
/// Pengganti TPanel Delphi: panel yang menampilkan Caption (Text) dan bevel 3D.
/// </summary>
public class CaptionPanel : Panel
{
    private PanelBevel _bevelOuter = PanelBevel.Raised;
    private PanelBevel _bevelInner = PanelBevel.None;
    private int _bevelWidth = 1;
    private ContentAlignment _textAlign = ContentAlignment.MiddleCenter;

    public CaptionPanel()
    {
        SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
    }

    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    [AllowNull]
    public override string Text
    {
        get => base.Text;
        set
        {
            base.Text = value;
            Invalidate();
        }
    }

    [DefaultValue(PanelBevel.Raised)]
    public PanelBevel BevelOuter
    {
        get => _bevelOuter;
        set { _bevelOuter = value; Invalidate(); }
    }

    [DefaultValue(PanelBevel.None)]
    public PanelBevel BevelInner
    {
        get => _bevelInner;
        set { _bevelInner = value; Invalidate(); }
    }

    [DefaultValue(1)]
    public int BevelWidth
    {
        get => _bevelWidth;
        set { _bevelWidth = Math.Max(1, value); Invalidate(); }
    }

    [DefaultValue(ContentAlignment.MiddleCenter)]
    public ContentAlignment TextAlign
    {
        get => _textAlign;
        set { _textAlign = value; Invalidate(); }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var rect = ClientRectangle;

        rect = DrawBevel(e.Graphics, rect, _bevelOuter);
        rect = DrawBevel(e.Graphics, rect, _bevelInner);

        if (!string.IsNullOrEmpty(Text))
        {
            var flags = TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
            flags |= _textAlign switch
            {
                ContentAlignment.TopLeft => TextFormatFlags.Top | TextFormatFlags.Left,
                ContentAlignment.TopCenter => TextFormatFlags.Top | TextFormatFlags.HorizontalCenter,
                ContentAlignment.TopRight => TextFormatFlags.Top | TextFormatFlags.Right,
                ContentAlignment.MiddleLeft => TextFormatFlags.VerticalCenter | TextFormatFlags.Left,
                ContentAlignment.MiddleRight => TextFormatFlags.VerticalCenter | TextFormatFlags.Right,
                ContentAlignment.BottomLeft => TextFormatFlags.Bottom | TextFormatFlags.Left,
                ContentAlignment.BottomCenter => TextFormatFlags.Bottom | TextFormatFlags.HorizontalCenter,
                ContentAlignment.BottomRight => TextFormatFlags.Bottom | TextFormatFlags.Right,
                _ => TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter,
            };
            var color = Enabled ? ForeColor : SystemColors.GrayText;
            TextRenderer.DrawText(e.Graphics, Text, Font, rect, color, flags);
        }
    }

    private Rectangle DrawBevel(Graphics g, Rectangle rect, PanelBevel bevel)
    {
        if (bevel == PanelBevel.None)
            return rect;

        var light = bevel == PanelBevel.Raised ? SystemColors.ControlLightLight : SystemColors.ControlDark;
        var dark = bevel == PanelBevel.Raised ? SystemColors.ControlDark : SystemColors.ControlLightLight;
        for (int i = 0; i < _bevelWidth; i++)
        {
            using var lightPen = new Pen(light);
            using var darkPen = new Pen(dark);
            g.DrawLine(lightPen, rect.Left, rect.Top, rect.Right - 1, rect.Top);
            g.DrawLine(lightPen, rect.Left, rect.Top, rect.Left, rect.Bottom - 1);
            g.DrawLine(darkPen, rect.Right - 1, rect.Top, rect.Right - 1, rect.Bottom - 1);
            g.DrawLine(darkPen, rect.Left, rect.Bottom - 1, rect.Right - 1, rect.Bottom - 1);
            rect.Inflate(-1, -1);
        }
        return rect;
    }
}
