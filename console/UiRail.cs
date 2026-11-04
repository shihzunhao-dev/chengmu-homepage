using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ChengMuConsole
{
    // =============================================================================
    //  家族的顏色、按鈕、圓角框（從 SE-Deploy 的 UiRail.cs 複製；側欄的項目改成 UiLists.cs 的日期清單，
    //  精靈步驟、進度條、步驟清單這支工具用不到，拿掉了）。
    //  全部用 GDI+ 自己畫，字一律用 Graphics.DrawString，跟 Label 預設的畫字方式相同。
    // =============================================================================
    public static class RailTheme
    {
        public static readonly Color Accent = Color.FromArgb(31, 111, 209);
        public static readonly Color AccentHover = Color.FromArgb(42, 123, 219);
        public static readonly Color AccentPressed = Color.FromArgb(26, 93, 180);
        public static readonly Color AccentSoft = Color.FromArgb(238, 246, 253);
        public static readonly Color AccentDisabled = Color.FromArgb(169, 195, 219);

        public static readonly Color RailText = Color.FromArgb(227, 238, 246);

        public static readonly Color Text = Color.FromArgb(27, 43, 75);
        public static readonly Color Muted = Color.FromArgb(74, 91, 120);
        public static readonly Color Grey = Color.FromArgb(135, 150, 171);
        public static readonly Color Border = Color.FromArgb(227, 234, 242);
        public static readonly Color FooterBg = Color.FromArgb(238, 244, 250);
        public static readonly Color FooterLine = Color.FromArgb(224, 232, 241);

        public static readonly Color Red = Color.FromArgb(198, 52, 52);

        public static readonly Color AmberBg = Color.FromArgb(255, 248, 235);
        public static readonly Color AmberLine = Color.FromArgb(232, 200, 140);
        public static readonly Color AmberText = Color.FromArgb(110, 69, 0);
        public static readonly Color DangerBg = Color.FromArgb(255, 241, 240);
        public static readonly Color DangerLine = Color.FromArgb(194, 65, 59);
        public static readonly Color DangerText = Color.FromArgb(143, 29, 23);

        public static GraphicsPath Round(RectangleF r, float radius)
        {
            var p = new GraphicsPath();
            var d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            if (d <= 0) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void FillRound(Graphics g, Color fill, Color? border, RectangleF r, float radius)
        {
            using (var path = Round(r, radius))
            {
                using (var b = new SolidBrush(fill)) g.FillPath(b, path);
                if (border.HasValue) using (var pen = new Pen(border.Value)) g.DrawPath(pen, path);
            }
        }

        // 父容器的底色（自己畫的元件四個圓角外要露出它）
        public static Color ParentBack(Control c)
        {
            return c.Parent != null ? c.Parent.BackColor : SystemColors.Control;
        }

        // 浮起的框：下緣深一點、上緣內側一條白線（設計稿的 border-bottom 與 inset 0 1px 0 #fff）
        public static void DrawLift(Graphics g, int width, int height, float radius, Color bottom)
        {
            using (var pen = new Pen(bottom)) g.DrawLine(pen, radius, height - 1.5f, width - radius - 1, height - 1.5f);
            using (var pen = new Pen(Color.FromArgb(230, 255, 255, 255))) g.DrawLine(pen, radius, 1.5f, width - radius - 1, 1.5f);
        }
    }

    // Outline：框線的小按鈕（「複製全部」），可以帶圖示
    public enum ButtonTone { Primary, Secondary, Outline }

    // 圓角、微微凸起的按鈕。主要動作用 Primary。
    public sealed class FlatButton : Button
    {
        bool hover, pressed;
        ButtonTone tone;

        public ButtonTone Tone { get { return tone; } set { tone = value; Invalidate(); } }
        public IconKind? Icon { get; set; }

        public FlatButton(string text, ButtonTone tone)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Text = text;
            Tone = tone;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            // 家族是 9／10pt，這支工具放大一號（老花友善）
            Font = tone == ButtonTone.Outline ? Ui.Font(10, true) : Ui.Font(10.5f, true);
            Cursor = Cursors.Hand;
            Height = 34;
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { pressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

        // top／bottom：上淺下深的底色；edge：下緣較深的那條線（null＝不畫）
        void Colors(out Color top, out Color bottom, out Color? border, out Color? edge, out Color ink)
        {
            var on = Enabled;
            switch (Tone)
            {
                case ButtonTone.Primary:
                    if (!on) { top = bottom = RailTheme.AccentDisabled; border = null; edge = null; ink = Color.White; return; }
                    var c = pressed ? RailTheme.AccentPressed : hover ? RailTheme.AccentHover : RailTheme.Accent;
                    top = Color.FromArgb(Math.Min(255, c.R + 14), Math.Min(255, c.G + 14), Math.Min(255, c.B + 10)); bottom = c;
                    border = null; edge = Color.FromArgb(22, 82, 160); ink = Color.White; return;
                case ButtonTone.Outline:
                    top = Color.White;
                    bottom = !on ? Color.White : hover ? Color.FromArgb(220, 234, 252) : Color.FromArgb(232, 241, 253);
                    border = on ? Color.FromArgb(141, 184, 236) : Color.FromArgb(214, 222, 232);
                    edge = on ? Color.FromArgb(110, 159, 221) : (Color?)null;
                    ink = on ? DeepTheme.Link : Color.FromArgb(167, 179, 195); return;
                default:
                    top = Color.White;
                    bottom = !on ? Color.White : hover ? RailTheme.AccentSoft : Color.FromArgb(246, 249, 252);
                    border = on ? Color.FromArgb(201, 211, 222) : Color.FromArgb(214, 222, 232);
                    edge = on ? Color.FromArgb(181, 193, 207) : (Color?)null;
                    ink = on ? RailTheme.Text : Color.FromArgb(167, 179, 195); return;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(RailTheme.ParentBack(this));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Color top, bottom, ink; Color? border, edge;
            Colors(out top, out bottom, out border, out edge, out ink);
            var r = new RectangleF(0.5f, 0.5f, Width - 2, Height - 2);
            using (var path = RailTheme.Round(r, 7))
            {
                using (var b = new LinearGradientBrush(RectangleF.Inflate(r, 0, 1), top, bottom, LinearGradientMode.Vertical) { WrapMode = WrapMode.TileFlipXY })
                    g.FillPath(b, path);
                if (border.HasValue) using (var pen = new Pen(border.Value)) g.DrawPath(pen, path);
            }
            if (edge.HasValue) RailTheme.DrawLift(g, Width, Height, 7, edge.Value);

            var textBox = new RectangleF(4, 0, Width - 8, Height);
            if (Icon.HasValue)
            {
                // 圖示接在字的左邊，整組置中
                var tw = TextRenderer.MeasureText(Text, Font).Width;
                var left = (Width - (16 + 6 + tw)) / 2f;
                UiIcons.Draw(g, Icon.Value, new RectangleF(left, (Height - 16) / 2f, 16, 16), ink, 1.6f);
                textBox = new RectangleF(left + 22, 0, Width - left - 22, Height);
            }
            using (var b = new SolidBrush(ink))
            using (var sf = new StringFormat { Alignment = Icon.HasValue ? StringAlignment.Near : StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                g.DrawString(Text, Font, b, textBox, sf);
            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(g, Rectangle.Inflate(ClientRectangle, -3, -3));
        }
    }

    // 圓角的框，微微浮起（落影由上一層容器畫，見 Raised.Attach）。
    // 底色同時當子元件（Label）的背景，所以只能是單色：漸層會被 Label 蓋出一條條色塊。
    public sealed class CardPanel : Panel, IRaised
    {
        Color border;
        readonly List<int> rules = new List<int>();

        public Color Border { get { return border; } set { border = value; Invalidate(); } }
        // 下緣較深的那條線
        public Color BorderBottom { get; set; }
        public bool HasShadow { get; set; }
        public int Radius { get; set; }
        // 列與列之間的細分隔線（離框左右各 RuleInset）
        public IList<int> Rules { get { return rules; } }
        public int RuleInset { get; set; }

        public CardPanel(Color fill, Color border)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = fill;
            this.border = border;
            BorderBottom = DeepTheme.BorderBottom;
            HasShadow = true;
            Radius = 12;
            RuleInset = 18;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(RailTheme.ParentBack(this));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            RailTheme.FillRound(g, BackColor, border, new RectangleF(0.5f, 0.5f, Width - 2, Height - 2), Radius);
            RailTheme.DrawLift(g, Width, Height, Radius, BorderBottom);
            g.SmoothingMode = SmoothingMode.None;
            using (var pen = new Pen(DeepTheme.RowLine))
                foreach (var y in rules) g.DrawLine(pen, RuleInset, y, Width - RuleInset - 1, y);
        }
    }
}
