using System.ComponentModel;

namespace UPKPro.Controls;

public enum BevelShape
{
    Box,
    Frame,
    TopLine,
    BottomLine,
    LeftLine,
    RightLine,
    Spacer,
}

/// <summary>Pengganti TBevel Delphi: garis/kotak etched 3D.</summary>
public class Bevel : Control
{
    private BevelShape _shape = BevelShape.Box;
    private bool _raised;

    public Bevel()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.UserPaint |
                 ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw, true);
        SetStyle(ControlStyles.Selectable, false);
        BackColor = Color.Transparent;
        TabStop = false;
    }

    [DefaultValue(BevelShape.Box)]
    public BevelShape Shape { get => _shape; set { _shape = value; Invalidate(); } }

    [DefaultValue(false)]
    public bool Raised { get => _raised; set { _raised = value; Invalidate(); } }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        var c1 = _raised ? SystemColors.ControlLightLight : SystemColors.ControlDark;
        var c2 = _raised ? SystemColors.ControlDark : SystemColors.ControlLightLight;
        using var p1 = new Pen(c1);
        using var p2 = new Pen(c2);
        int w = Width - 1, h = Height - 1;

        switch (_shape)
        {
            case BevelShape.Box:
                g.DrawLine(p1, 0, 0, w, 0);
                g.DrawLine(p1, 0, 0, 0, h);
                g.DrawLine(p2, w, 0, w, h);
                g.DrawLine(p2, 0, h, w, h);
                break;
            case BevelShape.Frame:
                g.DrawRectangle(p1, 0, 0, w - 1, h - 1);
                g.DrawRectangle(p2, 1, 1, w - 1, h - 1);
                break;
            case BevelShape.TopLine:
                g.DrawLine(p1, 0, 0, w, 0);
                g.DrawLine(p2, 0, 1, w, 1);
                break;
            case BevelShape.BottomLine:
                g.DrawLine(p1, 0, h - 1, w, h - 1);
                g.DrawLine(p2, 0, h, w, h);
                break;
            case BevelShape.LeftLine:
                g.DrawLine(p1, 0, 0, 0, h);
                g.DrawLine(p2, 1, 0, 1, h);
                break;
            case BevelShape.RightLine:
                g.DrawLine(p1, w - 1, 0, w - 1, h);
                g.DrawLine(p2, w, 0, w, h);
                break;
        }
    }
}
