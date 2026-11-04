using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace UPKPro.Controls;

public enum ShapeKind
{
    Rectangle,
    Square,
    RoundRect,
    RoundSquare,
    Ellipse,
    Circle,
}

/// <summary>
/// Pengganti TShape Delphi. Kontrol yang berada di dalam area shape dijadikan child
/// sehingga label transparan tetap menampilkan warna shape.
/// </summary>
public class ShapeControl : Control
{
    private ShapeKind _shape = ShapeKind.Rectangle;
    private Color _brushColor = Color.White;
    private bool _brushClear;
    private Color _penColor = Color.Black;
    private int _penWidth = 1;
    private bool _penClear;

    public ShapeControl()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.UserPaint |
                 ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw, true);
        SetStyle(ControlStyles.Selectable, false);
        BackColor = Color.Transparent;
        TabStop = false;
    }

    [DefaultValue(ShapeKind.Rectangle)]
    public ShapeKind Shape { get => _shape; set { _shape = value; Invalidate(); } }

    public Color BrushColor { get => _brushColor; set { _brushColor = value; Invalidate(); } }
    private bool ShouldSerializeBrushColor() => _brushColor != Color.White;
    private void ResetBrushColor() => BrushColor = Color.White;

    [DefaultValue(false)]
    public bool BrushClear { get => _brushClear; set { _brushClear = value; Invalidate(); } }

    public Color PenColor { get => _penColor; set { _penColor = value; Invalidate(); } }
    private bool ShouldSerializePenColor() => _penColor != Color.Black;
    private void ResetPenColor() => PenColor = Color.Black;

    [DefaultValue(1)]
    public int PenWidth { get => _penWidth; set { _penWidth = Math.Max(0, value); Invalidate(); } }

    [DefaultValue(false)]
    public bool PenClear { get => _penClear; set { _penClear = value; Invalidate(); } }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        int pw = _penClear ? 0 : _penWidth;
        // Stroke pena berada di tengah garis tepi; geser setengah lebar pena agar tidak terpotong.
        float off = pw / 2f;
        int shrink = pw == 0 ? 0 : pw + 1 - pw % 2;
        var rect = new RectangleF(off, off, Width - shrink, Height - shrink);
        if (_shape is ShapeKind.Square or ShapeKind.RoundSquare or ShapeKind.Circle)
        {
            float s = Math.Min(rect.Width, rect.Height);
            rect = new RectangleF(rect.X + (rect.Width - s) / 2, rect.Y + (rect.Height - s) / 2, s, s);
        }
        if (rect.Width <= 0 || rect.Height <= 0)
            return;

        using var path = new GraphicsPath();
        switch (_shape)
        {
            case ShapeKind.Ellipse:
            case ShapeKind.Circle:
                path.AddEllipse(rect);
                break;
            case ShapeKind.RoundRect:
            case ShapeKind.RoundSquare:
                float r = Math.Min(Math.Min(rect.Width, rect.Height) / 4f, 16f);
                float d = r * 2;
                path.AddArc(rect.X, rect.Y, d, d, 180, 90);
                path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
                path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
                path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
                path.CloseFigure();
                break;
            default:
                g.SmoothingMode = SmoothingMode.None;
                path.AddRectangle(rect);
                break;
        }

        if (!_brushClear)
        {
            using var brush = new SolidBrush(_brushColor);
            g.FillPath(brush, path);
        }
        if (pw > 0)
        {
            using var pen = new Pen(_penColor, pw);
            g.DrawPath(pen, path);
        }
    }
}
