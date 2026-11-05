using System.Drawing.Drawing2D;

namespace UPKPro.Forms;

/// <summary>
/// Panel petunjuk pengisian proposal (pengganti Memo2 polos):
/// header berwarna + daftar langkah bernomor dengan scroll vertikal.
/// Dibuat di runtime agar tidak tertimpa saat konverter DFM dijalankan ulang.
/// </summary>
public partial class FProposal
{
    private static readonly PetunjukStep[] PetunjukSteps =
    [
        new("Klik tombol BARU untuk penerimaan proposal."),
        new("Pilih Kategori Kelompok."),
        new("Untuk Kelompok Lama:",
        [
            "Klik pilihan \"Lama\" pada pilihan Pengajuan Kelompok.",
            "Pilih Desa dan masukkan nama kelompok.",
            "Selanjutnya pilih nama kelompok pada Tabel Daftar Kelompok.",
            "Lakukan dobel klik untuk memilih kelompok.",
            "Lengkapi form pengisian bila diperlukan.",
        ]),
        new("Untuk pengajuan kelompok baru, lakukan pengisian pada form isian sesuai pengajuan."),
        new("Masukkan jumlah pengajuan pinjaman."),
        new("Masukkan jangka waktu."),
        new("Pilih jenis pinjaman dan besar bunga per tahun."),
        new("Pilih metode angsuran."),
        new("Masukkan catatan lain-lain bila diperlukan."),
        new("Klik tombol Simpan untuk menyimpan data pengajuan.", IsFinal: true),
    ];

    private void BuildPetunjuk()
    {
        Memo2.Visible = false;

        var header = new PetunjukHeader
        {
            Dock = DockStyle.Top,
            Title = "Petunjuk Pengisian Proposal",
            Subtitle = "Ikuti langkah-langkah berikut secara berurutan",
        };

        var body = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = PetunjukTheme.Body,
        };
        // Hanya scroll vertikal.
        body.HorizontalScroll.Maximum = 0;
        body.HorizontalScroll.Visible = false;

        var view = new PetunjukView(PetunjukSteps) { Dock = DockStyle.Top };
        body.Controls.Add(view);
        // Roda mouse tetap bekerja walau kursor di atas daftar.
        view.MouseEnter += (_, _) => body.Focus();

        Panel12.SuspendLayout();
        Panel12.BackColor = PetunjukTheme.Border;
        Panel12.Padding = new Padding(1);
        Panel12.Controls.Add(body);
        Panel12.Controls.Add(header);
        Panel12.ResumeLayout(true);
    }

    private sealed record PetunjukStep(string Text, string[]? SubItems = null, bool IsFinal = false);

    private static class PetunjukTheme
    {
        public static readonly Color Accent = Color.FromArgb(21, 101, 192);
        public static readonly Color AccentDark = Color.FromArgb(13, 71, 161);
        public static readonly Color AccentSoft = Color.FromArgb(227, 238, 252);
        public static readonly Color Success = Color.FromArgb(46, 125, 50);
        public static readonly Color Body = Color.White;
        public static readonly Color Border = Color.FromArgb(197, 211, 232);
        public static readonly Color Text = Color.FromArgb(38, 50, 56);
        public static readonly Color SubText = Color.FromArgb(69, 90, 100);
        public static readonly Color SubBox = Color.FromArgb(245, 248, 252);
        public static readonly Color Connector = Color.FromArgb(200, 216, 238);
        public static readonly Color Hover = Color.FromArgb(240, 246, 255);

        public static GraphicsPath RoundRect(RectangleF r, float radius)
        {
            var d = radius * 2;
            var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }

    /// <summary>Header bergradasi dengan ikon info, judul, dan subjudul.</summary>
    private sealed class PetunjukHeader : Control
    {
        private readonly Font _titleFont = new("Segoe UI", 10.5F, FontStyle.Bold);
        private readonly Font _subFont = new("Segoe UI", 8.5F);
        private readonly Font _iconFont = new("Georgia", 11F, FontStyle.Bold | FontStyle.Italic);

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public string Title { get; set; } = "";
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public string Subtitle { get; set; } = "";

        public PetunjukHeader()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            Height = Scale(52);
        }

        private int Scale(int v) => (int)Math.Round(v * DeviceDpi / 96f);

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var rc = ClientRectangle;
            if (rc.Width <= 0 || rc.Height <= 0) return;

            using (var bg = new LinearGradientBrush(rc, PetunjukTheme.Accent, PetunjukTheme.AccentDark, LinearGradientMode.Horizontal))
                g.FillRectangle(bg, rc);

            g.SmoothingMode = SmoothingMode.AntiAlias;
            var icon = Scale(30);
            var iconRect = new Rectangle(Scale(14), (rc.Height - icon) / 2, icon, icon);
            using (var b = new SolidBrush(Color.FromArgb(60, Color.White)))
                g.FillEllipse(b, iconRect);
            using (var pen = new Pen(Color.FromArgb(160, Color.White), 1.5f))
                g.DrawEllipse(pen, iconRect);
            TextRenderer.DrawText(g, "i", _iconFont, iconRect, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            var x = iconRect.Right + Scale(12);
            var textRect = new Rectangle(x, Scale(8), rc.Width - x - Scale(10), rc.Height - Scale(16));
            var titleH = TextRenderer.MeasureText(g, Title, _titleFont).Height;
            TextRenderer.DrawText(g, Title, _titleFont, new Rectangle(textRect.X, textRect.Y, textRect.Width, titleH),
                Color.White, TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, Subtitle, _subFont,
                new Rectangle(textRect.X, textRect.Y + titleH + Scale(1), textRect.Width, textRect.Height - titleH),
                Color.FromArgb(210, 228, 255), TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _titleFont.Dispose();
                _subFont.Dispose();
                _iconFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Daftar langkah bernomor (owner-drawn). Tinggi kontrol menyesuaikan isi,
    /// sehingga panel induk yang AutoScroll menampilkan scrollbar vertikal.
    /// </summary>
    private sealed class PetunjukView : Control
    {
        private const TextFormatFlags WrapFlags =
            TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPadding | TextFormatFlags.Left;

        private readonly PetunjukStep[] _steps;
        private readonly Font _textFont = new("Segoe UI", 9.25F);
        private readonly Font _subFont = new("Segoe UI", 8.75F);
        private readonly Font _badgeFont = new("Segoe UI", 9F, FontStyle.Bold);

        private sealed class StepLayout
        {
            public Rectangle Row;
            public Rectangle Badge;
            public Rectangle Text;
            public Rectangle SubBox;
            public Rectangle[] Subs = [];
        }

        private StepLayout[] _layout = [];
        private int _layoutWidth = -1;
        private int _hover = -1;

        public PetunjukView(PetunjukStep[] steps)
        {
            _steps = steps;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = PetunjukTheme.Body;
        }

        private int Scale(int v) => (int)Math.Round(v * DeviceDpi / 96f);

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (Width != _layoutWidth) PerformStepLayout();
        }

        private void PerformStepLayout()
        {
            _layoutWidth = Width;
            if (Width <= 0) return;

            int padX = Scale(12), padTop = Scale(12), badge = Scale(26), gap = Scale(12);
            int rowPad = Scale(6), stepGap = Scale(4), subPad = Scale(8), bulletIndent = Scale(14);

            int textX = padX + badge + gap;
            int textW = Math.Max(Scale(60), Width - textX - padX);
            int y = padTop;

            _layout = new StepLayout[_steps.Length];
            for (int i = 0; i < _steps.Length; i++)
            {
                var s = _steps[i];
                var l = new StepLayout();
                int top = y;
                y += rowPad;

                var textH = TextRenderer.MeasureText(s.Text, _textFont, new Size(textW, int.MaxValue), WrapFlags).Height;
                int lineH = Math.Max(badge, textH);
                l.Badge = new Rectangle(padX, y, badge, badge);
                l.Text = new Rectangle(textX, y + Math.Max(0, (badge - textH) / 2), textW, textH);
                y += lineH;

                if (s.SubItems is { Length: > 0 } subs)
                {
                    y += Scale(6);
                    int boxTop = y;
                    int subTextX = textX + subPad + bulletIndent;
                    int subTextW = Math.Max(Scale(40), textX + textW - subPad - subTextX);
                    y += subPad;
                    l.Subs = new Rectangle[subs.Length];
                    for (int j = 0; j < subs.Length; j++)
                    {
                        var h = TextRenderer.MeasureText(subs[j], _subFont, new Size(subTextW, int.MaxValue), WrapFlags).Height;
                        l.Subs[j] = new Rectangle(subTextX, y, subTextW, h);
                        y += h + (j < subs.Length - 1 ? Scale(6) : 0);
                    }
                    y += subPad;
                    l.SubBox = new Rectangle(textX, boxTop, textW, y - boxTop);
                }

                y += rowPad;
                l.Row = new Rectangle(Scale(4), top, Width - Scale(8), y - top);
                y += stepGap;
                _layout[i] = l;
            }

            var newHeight = y + padTop;
            if (Height != newHeight) Height = newHeight;
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var idx = Array.FindIndex(_layout, l => l.Row.Contains(e.Location));
            if (idx != _hover)
            {
                _hover = idx;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hover != -1)
            {
                _hover = -1;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (_layout.Length != _steps.Length) return;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Garis penghubung antar nomor langkah.
            using (var linePen = new Pen(PetunjukTheme.Connector, Scale(2)))
            {
                for (int i = 0; i < _layout.Length - 1; i++)
                {
                    var a = _layout[i].Badge;
                    var b = _layout[i + 1].Badge;
                    var cx = a.X + a.Width / 2f;
                    g.DrawLine(linePen, cx, a.Bottom + Scale(2), cx, b.Top - Scale(2));
                }
            }

            for (int i = 0; i < _layout.Length; i++)
            {
                var s = _steps[i];
                var l = _layout[i];
                if (!e.ClipRectangle.IntersectsWith(l.Row)) continue;

                if (i == _hover)
                {
                    using var hp = PetunjukTheme.RoundRect(l.Row, Scale(6));
                    using var hb = new SolidBrush(PetunjukTheme.Hover);
                    g.FillPath(hb, hp);
                }

                // Nomor langkah.
                var badgeColor = s.IsFinal ? PetunjukTheme.Success : PetunjukTheme.Accent;
                using (var bb = new SolidBrush(badgeColor))
                    g.FillEllipse(bb, l.Badge);
                using (var ring = new Pen(i == _hover ? PetunjukTheme.Border : Color.White, Scale(2)))
                    g.DrawEllipse(ring, l.Badge);
                TextRenderer.DrawText(g, (i + 1).ToString(), _badgeFont, l.Badge, Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

                TextRenderer.DrawText(g, s.Text, _textFont, l.Text, PetunjukTheme.Text, WrapFlags);

                if (s.SubItems is { Length: > 0 } subs)
                {
                    using (var path = PetunjukTheme.RoundRect(l.SubBox, Scale(6)))
                    using (var fill = new SolidBrush(PetunjukTheme.SubBox))
                    using (var border = new Pen(PetunjukTheme.Border))
                    {
                        g.FillPath(fill, path);
                        g.DrawPath(border, path);
                    }
                    // Aksen kiri.
                    using (var accent = new SolidBrush(PetunjukTheme.Accent))
                        g.FillRectangle(accent, l.SubBox.X, l.SubBox.Y + Scale(6), Scale(3), l.SubBox.Height - Scale(12));

                    var dot = Scale(6);
                    using var dotBrush = new SolidBrush(PetunjukTheme.Accent);
                    for (int j = 0; j < subs.Length; j++)
                    {
                        var r = l.Subs[j];
                        var lineH = TextRenderer.MeasureText(g, "Ag", _subFont, Size.Empty, TextFormatFlags.NoPadding).Height;
                        g.FillEllipse(dotBrush, r.X - Scale(12), r.Y + (lineH - dot) / 2f, dot, dot);
                        TextRenderer.DrawText(g, subs[j], _subFont, r, PetunjukTheme.SubText, WrapFlags);
                    }
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _textFont.Dispose();
                _subFont.Dispose();
                _badgeFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
