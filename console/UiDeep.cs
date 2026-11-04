using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ChengMuConsole
{
    // =============================================================================
    //  ★ 從 D:\SE_Dev\DeployTool\UiDeep.cs 原樣複製，只改了 namespace（家族外觀換了要跟著換）。
    //    澄沐中控台只用 Normal 一種側欄；Amber／Red 留著不刪，跟家族那份保持一字不差比較好對照。
    //
    //  方案一「深海藍漸層」側欄（2026-10-19 使用者從三款對照稿選定：Codex 原稿＋S 形波浪）
    //  設計稿：https://claude.ai/artifact/6X1DyaXN8jS97ZPXBbogSp（方案一兩張），色碼整理在 D:\CCIMS_Dev\中控台改版_方案一_實作計畫.md
    //
    //  側欄由上而下漸層，左下角疊四組半透明色塊（斜面、藍綠帶、黃綠帶、底部弧面）和光暈，座標跟設計稿的 SVG 相同
    //  （寬 220、高 300 的區塊貼齊側欄底部）。只用半透明、漸層與細線，不用模糊：GDI+ 畫不出模糊，光暈是幾層由寬到窄的淡線疊出來的。
    //  試裝模式（琥珀）與解除安裝精靈（深紅）換底色，波浪改成白色淡淡的一層：一眼看得出不是平常的那個畫面。
    //
    //  ⚠ 放在側欄上的元件不能靠 BackColor = Transparent：按鈕是不透明元件，不會叫父容器畫底，DrawToBitmap 截圖也不可靠。
    //    一律在自己的 OnPaint 開頭叫 RailPanel.PaintUnder，由側欄把自己那一塊底畫進來，再畫自己。
    // =============================================================================
    public enum RailVariant { Normal, Amber, Red }

    public static class DeepTheme
    {
        public static readonly Color Title = Color.FromArgb(15, 42, 74);
        public static readonly Color RailSub = Color.FromArgb(191, 212, 228);
        public static readonly Color Blue = Color.FromArgb(22, 96, 196);
        public static readonly Color Link = Color.FromArgb(28, 100, 208);
        public static readonly Color Icon = Color.FromArgb(58, 83, 120);
        public static readonly Color RowLine = Color.FromArgb(238, 242, 246);
        public static readonly Color BorderBottom = Color.FromArgb(211, 221, 233);
        public static readonly Color Colon = Color.FromArgb(138, 154, 179);
        public static readonly Color Note = Color.FromArgb(85, 112, 146);
        public static readonly Color TipBg = Color.FromArgb(238, 246, 253);
        public static readonly Color TipLine = Color.FromArgb(216, 231, 246);
        public static readonly Color Bulb = Color.FromArgb(44, 143, 224);
        public static readonly Color FooterInk = Color.FromArgb(51, 80, 122);
        public static readonly Color DoneMark = Color.FromArgb(134, 215, 174);
        public static readonly Color BarLeft = Color.FromArgb(15, 124, 194);
        public static readonly Color BarRight = Color.FromArgb(63, 175, 122);

        // 側欄底色：上、中（52%）、下
        public static Color[] RailColors(RailVariant v)
        {
            switch (v)
            {
                case RailVariant.Amber: return new[] { Color.FromArgb(84, 48, 0), Color.FromArgb(112, 66, 0), Color.FromArgb(146, 90, 8) };
                case RailVariant.Red: return new[] { Color.FromArgb(74, 16, 20), Color.FromArgb(104, 26, 30), Color.FromArgb(138, 42, 42) };
                default: return new[] { Color.FromArgb(3, 48, 77), Color.FromArgb(4, 59, 92), Color.FromArgb(5, 80, 122) };
            }
        }

        // 選中那一項的膠囊（中控台的項目、精靈目前這一步）
        public static void FillPill(Graphics g, RectangleF r, RailVariant v)
        {
            var mode = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            // 下面墊一層淡影，看起來微微浮起（設計稿 0 2px 6px）
            RailTheme.FillRound(g, Color.FromArgb(46, 0, 20, 40), null, new RectangleF(r.X, r.Y + 2, r.Width, r.Height), 8);
            using (var path = RailTheme.Round(r, 8))
            {
                if (v == RailVariant.Normal)
                {
                    using (var b = new LinearGradientBrush(RectangleF.Inflate(r, 1, 1), Color.Empty, Color.Empty, LinearGradientMode.Horizontal))
                    {
                        b.InterpolationColors = new ColorBlend
                        {
                            Colors = new[] { Color.FromArgb(63, 175, 122), Color.FromArgb(28, 140, 192), Color.FromArgb(15, 124, 194) },
                            Positions = new[] { 0f, 0.34f, 1f },
                        };
                        b.WrapMode = WrapMode.TileFlipXY;
                        g.FillPath(b, path);
                    }
                }
                else using (var b = new SolidBrush(Color.FromArgb(52, 255, 255, 255))) g.FillPath(b, path);
                using (var pen = new Pen(Color.FromArgb(56, 255, 255, 255))) g.DrawPath(pen, path);
            }
            var edge = v == RailVariant.Amber ? Color.FromArgb(255, 226, 168) : v == RailVariant.Red ? Color.FromArgb(255, 194, 186) : Color.FromArgb(212, 236, 106);
            RailTheme.FillRound(g, edge, null, new RectangleF(r.X, r.Y + (r.Height - 24) / 2, 3, 24), 1.5f);
            g.SmoothingMode = mode;
        }
    }

    // 側欄：漸層底＋左下角的波浪
    public sealed class RailPanel : Panel
    {
        readonly RailVariant variant;

        public RailVariant Variant { get { return variant; } }

        public RailPanel(RailVariant variant)
        {
            this.variant = variant;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = DeepTheme.RailColors(variant)[1];
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            PaintSurface(e.Graphics, 0, 0);
        }

        // 側欄上的元件畫自己之前先叫這個：側欄就畫元件底下那一塊；不在側欄上就用父容器的底色
        public static void PaintUnder(Control c, Graphics g)
        {
            var rail = c.Parent as RailPanel;
            if (rail != null) rail.PaintSurface(g, -c.Left, -c.Top);
            else g.Clear(RailTheme.ParentBack(c));
        }

        public void PaintSurface(Graphics g, int dx, int dy)
        {
            var state = g.Save();
            g.TranslateTransform(dx, dy);
            var colors = DeepTheme.RailColors(variant);
            using (var b = new LinearGradientBrush(new Rectangle(0, -1, Math.Max(1, Width), Height + 2), colors[0], colors[2], LinearGradientMode.Vertical))
            {
                b.InterpolationColors = new ColorBlend { Colors = colors, Positions = new[] { 0f, 0.52f, 1f } };
                b.WrapMode = WrapMode.TileFlipXY;
                g.FillRectangle(b, 0, 0, Width, Height);
            }
            var k = Width / 220f;
            Waves.Draw(g, 0, Height - 300 * k, k, variant != RailVariant.Normal);
            g.Restore(state);
        }
    }

    // 左下角的波浪。mono：試裝與解除安裝用，同樣的形狀改成白色淡淡的一層
    static class Waves
    {
        public static void Draw(Graphics g, float x0, float y0, float k, bool mono)
        {
            Func<float, float, PointF> P = (x, y) => new PointF(x0 + x * k, y0 + y * k);
            Func<int, int, Color> C = (a, rgb) => mono
                ? Color.FromArgb((int)(a * 0.3f), 255, 255, 255)
                : Color.FromArgb(a, rgb >> 16 & 255, rgb >> 8 & 255, rgb & 255);
            float left = x0 - 1, right = x0 + 220 * k + 1;
            var mode = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // 1. 從左上斜到右下的面
            var sheet = new[] { P(0, 122), P(70, 140), P(150, 232), P(220, 266) };
            using (var path = Closed(sheet, P(220, 300), P(0, 300)))
            using (var b = new LinearGradientBrush(P(0, 121), P(220, 301), C(128, 0x1090CC), C(77, 0x0A5E96)) { WrapMode = WrapMode.TileFlipXY })
                g.FillPath(b, path);
            using (var pen = new Pen(C(71, 0x6FD0F2), 1f)) g.DrawBezier(pen, sheet[0], sheet[1], sheet[2], sheet[3]);

            // 2. 黃綠帶下面的藍綠帶（斜率比黃綠帶平，蓋住黃綠帶下面三分之一，左端伸進底部弧面）
            var tealTop = new[] { P(0, 222), P(80, 206), P(150, 154), P(220, 132) };
            using (var path = Band(tealTop, new[] { P(220, 184), P(150, 196), P(80, 238), P(0, 262) }))
            using (var b = Horizontal(left, right, new[] { C(204, 0x2AC6D2), C(115, 0x1BA2C4), C(56, 0x1584BC) }, new[] { 0f, 0.5f, 1f }))
                g.FillPath(b, path);

            // 3. 黃綠帶：左端最亮，越往右越寬越透明
            var ribbonTop = new[] { P(0, 206), P(70, 194), P(160, 118), P(220, 96) };
            using (var path = Band(ribbonTop, new[] { P(220, 152), P(160, 170), P(80, 216), P(0, 226) }))
            using (var b = Horizontal(left, right, new[] { C(255, 0xE8F95C), C(230, 0xB8E85E), C(148, 0x66C28A), C(82, 0x34A0A0) }, new[] { 0f, 0.4f, 0.72f, 1f }))
                g.FillPath(b, path);

            // 4. 光暈：沿著色帶的線由寬到窄疊幾層很淡的筆
            var ribbonMid = new[] { P(0, 216), P(70, 205), P(160, 140), P(220, 124) };
            var tealHalo = new[] { 0x6FE6F0, 0x4FC0E8 };
            Halo(g, tealTop, left, right, new[] { 16f, 10f, 5f }, new[] { 0.04f, 0.05f, 0.07f }, k, mono, new[] { 1f, 0.3f }, tealHalo, new[] { 0f, 1f });
            var ribbonHalo = new[] { 0xF2FF8A, 0xC8F070, 0x7FE0C0 };
            Halo(g, ribbonTop, left, right, new[] { 22f, 15f, 9f, 4f }, new[] { 0.035f, 0.05f, 0.07f, 0.10f }, k, mono, new[] { 1f, 0.7f, 0.25f }, ribbonHalo, new[] { 0f, 0.5f, 1f });
            Halo(g, ribbonMid, left, right, new[] { 16f, 10f, 5f }, new[] { 0.04f, 0.05f, 0.06f }, k, mono, new[] { 1f, 0.7f, 0.25f }, ribbonHalo, new[] { 0f, 0.5f, 1f });

            // 5. 黃綠帶上緣的亮線，以及左端的一點柔光
            using (var b = Horizontal(left, right, new[] { C(230, 0xF6FFB8), C(20, 0xA6EEDC) }, new[] { 0f, 1f }))
            using (var pen = new Pen(b, 1.2f * k))
                g.DrawBezier(pen, ribbonTop[0], ribbonTop[1], ribbonTop[2], ribbonTop[3]);
            using (var path = new GraphicsPath())
            {
                var glow = new RectangleF(x0 - 10 * k, y0 + 200 * k, 80 * k, 30 * k);
                path.AddEllipse(glow);
                using (var b = new PathGradientBrush(path) { CenterColor = C(80, 0xEEFF7A), SurroundColors = new[] { Color.FromArgb(0, C(80, 0xEEFF7A)) } })
                    g.FillPath(b, path);
            }

            // 6. 最底下的弧面
            var baseTop = new[] { P(0, 248), P(70, 236), P(150, 240), P(220, 254) };
            using (var path = Closed(baseTop, P(220, 300), P(0, 300)))
            using (var b = new LinearGradientBrush(P(0, 234), P(0, 301), C(140, 0x0C88C8), C(230, 0x0866A4)) { WrapMode = WrapMode.TileFlipXY })
                g.FillPath(b, path);
            using (var pen = new Pen(C(89, 0x58C4EE), 1f)) g.DrawBezier(pen, baseTop[0], baseTop[1], baseTop[2], baseTop[3]);

            g.SmoothingMode = mode;
        }

        // 一條曲線往下拉到底邊圍成的面
        static GraphicsPath Closed(PointF[] curve, PointF corner1, PointF corner2)
        {
            var p = new GraphicsPath();
            p.AddBezier(curve[0], curve[1], curve[2], curve[3]);
            p.AddLine(curve[3], corner1);
            p.AddLine(corner1, corner2);
            p.CloseFigure();
            return p;
        }

        // 上緣、下緣兩條曲線圍成的帶子（下緣由右往左）
        static GraphicsPath Band(PointF[] top, PointF[] bottom)
        {
            var p = new GraphicsPath();
            p.AddBezier(top[0], top[1], top[2], top[3]);
            p.AddLine(top[3], bottom[0]);
            p.AddBezier(bottom[0], bottom[1], bottom[2], bottom[3]);
            p.CloseFigure();
            return p;
        }

        static LinearGradientBrush Horizontal(float left, float right, Color[] colors, float[] positions)
        {
            var b = new LinearGradientBrush(new PointF(left, 0), new PointF(right, 0), colors[0], colors[colors.Length - 1]);
            b.InterpolationColors = new ColorBlend { Colors = colors, Positions = positions };
            b.WrapMode = WrapMode.TileFlipXY;
            return b;
        }

        // 一組光暈：每一層的透明度＝層的透明度 × 漸層各點的透明度
        static void Halo(Graphics g, PointF[] curve, float left, float right, float[] widths, float[] layerAlpha, float k, bool mono,
                         float[] stopAlpha, int[] stopRgb, float[] positions)
        {
            for (int i = 0; i < widths.Length; i++)
            {
                var colors = new Color[stopRgb.Length];
                for (int j = 0; j < stopRgb.Length; j++)
                {
                    var a = (int)Math.Round(255 * layerAlpha[i] * stopAlpha[j] * (mono ? 0.6f : 1f));
                    var rgb = mono ? 0xFFFFFF : stopRgb[j];
                    colors[j] = Color.FromArgb(a, rgb >> 16 & 255, rgb >> 8 & 255, rgb & 255);
                }
                using (var b = Horizontal(left, right, colors, positions))
                using (var pen = new Pen(b, widths[i] * k) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    g.DrawBezier(pen, curve[0], curve[1], curve[2], curve[3]);
            }
        }
    }

    // 側欄上的一行字（標題、版號）：自己畫，底由側欄畫進來（Label 透明在截圖時不可靠）
    public sealed class RailLabel : Control
    {
        public RailLabel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            TabStop = false;
            ForeColor = Color.White;
        }

        protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            RailPanel.PaintUnder(this, e.Graphics);
            using (var b = new SolidBrush(ForeColor))
            using (var sf = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                e.Graphics.DrawString(Text, Font, b, new RectangleF(0, 0, Width, Height), sf);
        }
    }

    // 浮起的卡片：控件畫不到自己範圍外，落影由上一層容器在它周圍畫（Raised.Attach）
    public interface IRaised
    {
        bool HasShadow { get; }
        int Radius { get; }
    }

    public static class Raised
    {
        public static void Attach(Control parent)
        {
            parent.Paint += (s, e) =>
            {
                foreach (Control c in parent.Controls)
                {
                    var r = c as IRaised;
                    if (r != null && r.HasShadow && c.Visible) DrawShadow(e.Graphics, c.Bounds, r.Radius);
                }
            };
            // 落影畫在框的外圍（屬於上一層），框隱藏、移動、換大小或被拿掉時，Windows 只會重畫框自己那一塊，
            // 外圍那圈影子會留在畫面上（切換「顯示詳細紀錄」、精靈換頁時看得到）。所以整個上一層重畫。
            // 截圖程式每張都整個 Refresh，看不出這個問題。
            EventHandler repaint = (s, e) => parent.Invalidate();
            parent.ControlAdded += (s, e) =>
            {
                if (!(e.Control is IRaised)) return;
                e.Control.VisibleChanged += repaint;
                e.Control.LocationChanged += repaint;
                e.Control.SizeChanged += repaint;
                parent.Invalidate();
            };
            parent.ControlRemoved += (s, e) =>
            {
                if (!(e.Control is IRaised)) return;
                e.Control.VisibleChanged -= repaint;
                e.Control.LocationChanged -= repaint;
                e.Control.SizeChanged -= repaint;
                parent.Invalidate();
            };
        }

        // 設計稿的 0 1px 2px＋0 4px 10px：往下偏一點、往外擴幾圈，越外圈越淡
        public static void DrawShadow(Graphics g, Rectangle b, int radius)
        {
            var mode = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int[] grow = { 6, 4, 2, 1 }, down = { 4, 3, 2, 1 }, alpha = { 5, 8, 11, 15 };
            for (int i = 0; i < grow.Length; i++)
            {
                var r = new RectangleF(b.X - grow[i], b.Y - grow[i] + down[i], b.Width + grow[i] * 2, b.Height + grow[i] * 2);
                RailTheme.FillRound(g, Color.FromArgb(alpha[i], 16, 42, 74), null, r, radius + grow[i]);
            }
            g.SmoothingMode = mode;
        }
    }
}
