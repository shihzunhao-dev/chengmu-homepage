using System;
using System.Drawing;
using System.Windows.Forms;

namespace ChengMuConsole
{
    // =============================================================================
    //  內容區的小元件（從 SE-Deploy 的 UiCards.cs 複製；狀態卡、詳細狀況列這支工具用不到，拿掉了）。
    //  都是自己畫字，字型與 Label 相同（GDI+ DrawString，見 Ui.cs 的說明）。
    // =============================================================================

    // 頁面標題：字下面墊一層淡淡的落影，看起來微微浮起（設計稿 text-shadow 0 2px 4px）
    public sealed class TitleLabel : Control
    {
        public TitleLabel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            TabStop = false;
            Font = Ui.Font(18, true);
            ForeColor = DeepTheme.Title;
        }

        protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(RailTheme.ParentBack(this));
            using (var sf = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            {
                // 模糊的落影用三份錯開一點點的淡色字湊出來
                using (var sb = new SolidBrush(Color.FromArgb(20, 15, 42, 74)))
                    foreach (var o in new[] { new PointF(0, 3), new PointF(0.8f, 2), new PointF(-0.8f, 2) })
                        g.DrawString(Text, Font, sb, new RectangleF(o.X, o.Y, Width, Height), sf);
                using (var b = new SolidBrush(ForeColor))
                    g.DrawString(Text, Font, b, new RectangleF(0, 0, Width, Height), sf);
            }
        }
    }

    // 單獨的一個圖示（卡片標題、搜尋框前面）
    public sealed class IconBox : Control
    {
        public IconKind Icon { get; set; }
        public float Stroke { get; set; }

        public IconBox(IconKind icon, Color ink)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            TabStop = false;
            Icon = icon;
            ForeColor = ink;
            Stroke = 1.6f;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(RailTheme.ParentBack(this));
            UiIcons.Draw(e.Graphics, Icon, new RectangleF(0, 0, Width, Height), ForeColor, Stroke);
        }
    }

    // 卡片的標題列：圖示＋粗體標題＋右邊淡色的補充說明（例如「11/04（三）・3 位客戶」）
    public sealed class CardHeader : Control
    {
        readonly Font titleFont = Ui.Font(12, true), noteFont = Ui.Font(10.5f);
        string note = "";

        public IconKind Icon { get; set; }
        public string Note { get { return note; } set { note = value ?? ""; Invalidate(); } }
        // 右邊要留給按鈕的寬度
        public int ReserveRight { get; set; }

        public CardHeader(IconKind icon, string title)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            TabStop = false;
            Icon = icon;
            Text = title;
        }

        protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(RailTheme.ParentBack(this));
            UiIcons.Draw(g, Icon, new RectangleF(0, (Height - 20) / 2f, 20, 20), RailTheme.Text, 1.8f);
            using (var sf = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            {
                var tw = g.MeasureString(Text, titleFont).Width;
                using (var b = new SolidBrush(DeepTheme.Title)) g.DrawString(Text, titleFont, b, new RectangleF(28, 0, Width - 28, Height), sf);
                var nx = 28 + tw + 8;
                using (var b = new SolidBrush(RailTheme.Muted))
                    g.DrawString(note, noteFont, b, new RectangleF(nx, 0, Math.Max(0, Width - ReserveRight - nx), Height), sf);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { titleFont.Dispose(); noteFont.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
