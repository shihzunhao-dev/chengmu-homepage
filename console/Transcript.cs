using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace ChengMuConsole
{
    // =============================================================================
    //  右邊的對話內容：一段對話一個分隔標題，訊息畫成對話泡泡（客人在左、助理與錯誤在右）。
    //  泡泡裡的字是唯讀的 TextBox：可以反白、Ctrl+C 複製、Ctrl+A 全選這一則。
    //  不用 RichTextBox／WebBrowser：兩者 DrawToBitmap 都畫不出來（--screenshot 要用），WebBrowser 還是 IE 核心。
    // =============================================================================
    public sealed class TranscriptView : Panel
    {
        const int EdgeX = 18, Gap = 10;
        readonly Label empty;
        readonly List<Control> items = new List<Control>();
        Control spacer;
        int laidOutWidth = -1;
        string keyword;

        public TranscriptView()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            AutoScroll = true;
            BackColor = Color.White;
            empty = new Label
            {
                AutoSize = false, TextAlign = ContentAlignment.TopCenter, Font = Ui.Font(11.5f), ForeColor = RailTheme.Muted,
                Location = new Point(0, 40), BackColor = Color.White,
            };
            Controls.Add(empty);
        }

        public void ShowEmpty(string text)
        {
            Clear();
            empty.Text = text;
            empty.Visible = true;
            empty.Size = new Size(Math.Max(100, ClientSize.Width), 80);
        }

        void Clear()
        {
            SuspendLayout();
            AutoScrollPosition = new Point(0, 0);
            foreach (var c in items) { Controls.Remove(c); c.Dispose(); }
            items.Clear();
            if (spacer != null) { Controls.Remove(spacer); spacer.Dispose(); spacer = null; }
            laidOutWidth = -1;
            ResumeLayout();
        }

        public void ShowCustomer(Customer c, string highlight)
        {
            Clear();
            keyword = string.IsNullOrWhiteSpace(highlight) ? null : highlight.Trim();
            if (c == null) { ShowEmpty("請在中間選一位客人。"); return; }
            empty.Visible = false;
            SuspendLayout();
            var n = 0;
            foreach (var conv in c.Conversations)
            {
                n++;
                items.Add(new ConvHeader(string.Format("對話 {0}・{1:HH:mm} 開始・{2} 則提問", n, conv.StartTw, conv.QuestionCount), conv.Id));
                foreach (var m in conv.Messages)
                    items.Add(new Bubble(m, ChatGrouping.Contains(m.Content, keyword)));
            }
            spacer = new Panel { Size = new Size(1, EdgeX), BackColor = Color.White };
            foreach (var it in items) Controls.Add(it);
            Controls.Add(spacer);
            ResumeLayout();
            Arrange();
        }

        // 版面寬度固定扣掉直捲軸的寬：不然捲軸出現／消失會讓泡泡換行跟著變，來回重排
        int LayoutWidth { get { return Math.Max(200, ClientSize.Width - (VerticalScroll.Visible ? 0 : SystemInformation.VerticalScrollBarWidth)); } }

        void Arrange()
        {
            if (items.Count == 0) return;
            var w = LayoutWidth;
            laidOutWidth = w;
            SuspendLayout();
            var oy = AutoScrollPosition.Y;
            var y = 12;
            var maxBubble = (int)((w - EdgeX * 2) * 0.76);
            foreach (var it in items)
            {
                var h = it as ConvHeader;
                if (h != null)
                {
                    if (y > 12) y += 8;
                    h.Bounds = new Rectangle(EdgeX, y + oy, w - EdgeX * 2, 34);
                    y += 34 + Gap;
                    continue;
                }
                var b = (Bubble)it;
                b.Arrange(maxBubble);
                var x = b.IsRight ? w - EdgeX - b.Width : EdgeX;
                b.Location = new Point(x, y + oy);
                y += b.Height + Gap;
            }
            spacer.Location = new Point(0, y + oy);
            ResumeLayout();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (empty.Visible) empty.Width = Math.Max(100, ClientSize.Width);
            if (items.Count > 0 && LayoutWidth != laidOutWidth) Arrange();
        }

        public void ScrollBy(int delta)
        {
            var y = -AutoScrollPosition.Y - delta / 120 * 72;
            AutoScrollPosition = new Point(0, Math.Max(0, y));
        }

        /// <summary>「複製全部」：整位客人當天的對話，純文字。</summary>
        public static string PlainText(Day day, Customer c)
        {
            var sb = new StringBuilder();
            sb.AppendFormat("{0:yyyy-MM-dd}（{1}）　{2}（{3}）\r\n", day.Date, TwTime.Week(day.Date), c.IpLabel, c.Region);
            var n = 0;
            foreach (var conv in c.Conversations)
            {
                n++;
                sb.AppendFormat("\r\n【對話 {0}・{1:HH:mm} 開始】\r\n", n, conv.StartTw);
                foreach (var m in conv.Messages)
                    sb.AppendFormat("\r\n{0}　{1:HH:mm:ss}\r\n{2}\r\n", m.RoleLabel, m.Tw, m.Content);
            }
            return sb.ToString();
        }
    }

    /// <summary>每段對話前面的分隔：左邊粗體字、右邊淡色的對話編號，底下一條線。</summary>
    sealed class ConvHeader : Control
    {
        readonly Font font = Ui.Font(11, true), idFont = Ui.Latin(10);
        readonly string convId;

        public ConvHeader(string text, string convId)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Text = text;
            this.convId = convId ?? "";
            BackColor = Color.White;
            TabStop = false;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            UiIcons.Draw(g, IconKind.Chat, new RectangleF(0, 6, 18, 18), DeepTheme.Icon, 1.5f);
            var tw = g.MeasureString(Text, font).Width;
            using (var b = new SolidBrush(DeepTheme.Title))
            using (var sf = new StringFormat { LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap })
                g.DrawString(Text, font, b, new RectangleF(24, 0, Width - 24, Height - 4), sf);
            using (var b = new SolidBrush(RailTheme.Grey))
            using (var sf = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                g.DrawString("編號 " + convId, idFont, b, new RectangleF(24 + tw + 12, 0, Math.Max(0, Width - 24 - tw - 12), Height - 4), sf);
            using (var pen = new Pen(RailTheme.Border)) g.DrawLine(pen, 0, Height - 1.5f, Width, Height - 1.5f);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { font.Dispose(); idFont.Dispose(); }
            base.Dispose(disposing);
        }
    }

    /// <summary>一則訊息：上面一行「客人　15:30:12」，下面圓角泡泡，泡泡裡是唯讀 TextBox。</summary>
    sealed class Bubble : Panel
    {
        const int MetaH = 24, PadX = 14, PadY = 10;
        readonly ChatRow row;
        readonly bool match;
        readonly BubbleText box;
        readonly Font metaFont = Ui.Font(10, true), timeFont = Ui.Latin(10);

        public bool IsRight { get { return row.Role != "user"; } }

        public Bubble(ChatRow row, bool match)
        {
            this.row = row;
            this.match = match;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.White;
            Color fill, ink;
            Colors(out fill, out ink);
            box = new BubbleText
            {
                Multiline = true, ReadOnly = true, WordWrap = true, BorderStyle = BorderStyle.None, ScrollBars = ScrollBars.None,
                Font = Ui.Font(11.5f), TabStop = false, Text = (row.Content ?? "").Replace("\r\n", "\n").Replace("\n", "\r\n"),
            };
            // ReadOnly 之後要再設一次底色，不然會變成灰底
            box.BackColor = fill;
            box.ForeColor = ink;
            Controls.Add(box);
        }

        void Colors(out Color fill, out Color ink)
        {
            if (row.Role == "user") { fill = Color.FromArgb(232, 242, 253); ink = RailTheme.Text; }
            else if (row.Role == "assistant") { fill = Color.FromArgb(244, 247, 250); ink = RailTheme.Text; }
            else { fill = RailTheme.DangerBg; ink = RailTheme.DangerText; }
        }

        Color BorderColor
        {
            get
            {
                if (match) return Color.FromArgb(201, 138, 27);
                return row.Role == "user" ? Color.FromArgb(196, 220, 246) : row.Role == "assistant" ? Color.FromArgb(221, 229, 238) : Color.FromArgb(232, 180, 176);
            }
        }

        public void Arrange(int maxWidth)
        {
            var maxInner = Math.Max(120, maxWidth - PadX * 2);
            var flags = TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPrefix;
            var natural = TextRenderer.MeasureText(box.Text.Length == 0 ? " " : box.Text, box.Font, new Size(maxInner, 0), flags).Width + 12;
            var inner = Math.Min(maxInner, Math.Max(natural, 90));
            box.Width = inner;
            var lines = Math.Max(1, box.LineCount);
            // 行高要用 TextBox 自己的行距：Font.Height 是 GDI+ 的行距（含行間距），微軟正黑體每行多好幾 px，行數一多泡泡底下就空一截
            box.Height = lines * box.LinePitch + 4;
            // 上面那行（角色＋時間）比泡泡寬的時候以那行為準
            Size = new Size(Math.Max(inner + PadX * 2, 150), MetaH + box.Height + PadY * 2 + 2);
            box.Location = new Point(IsRight ? Width - PadX - inner : PadX, MetaH + PadY);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Color fill, ink;
            Colors(out fill, out ink);
            var bubbleW = box.Width + PadX * 2;
            var x = IsRight ? Width - bubbleW : 0;
            var r = new RectangleF(x + 0.5f, MetaH + 0.5f, bubbleW - 2, Height - MetaH - 2);
            RailTheme.FillRound(g, fill, null, r, 10);
            using (var path = RailTheme.Round(r, 10))
            using (var pen = new Pen(BorderColor, match ? 2f : 1f)) g.DrawPath(pen, path);

            var label = row.RoleLabel;
            var time = row.Tw.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            var lw = g.MeasureString(label, metaFont).Width;
            var tw = g.MeasureString(time, timeFont).Width;
            var labelInk = row.Role == "user" ? RailTheme.Accent : row.Role == "assistant" ? Color.FromArgb(14, 122, 115) : RailTheme.Red;
            var lx = IsRight ? Width - 4 - lw - 6 - tw : 4;
            using (var sf = new StringFormat { LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap })
            {
                using (var b = new SolidBrush(labelInk)) g.DrawString(label, metaFont, b, new RectangleF(lx, 0, lw + 4, MetaH), sf);
                using (var b = new SolidBrush(RailTheme.Muted)) g.DrawString(time, timeFont, b, new RectangleF(lx + lw + 6, 0, tw + 4, MetaH), sf);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { metaFont.Dispose(); timeFont.Dispose(); }
            base.Dispose(disposing);
        }
    }

    /// <summary>泡泡裡的唯讀文字：補上 Ctrl+A 全選（.NET Framework 的多行 TextBox 沒有），滾輪交給外面的捲動區。</summary>
    sealed class BubbleText : TextBox
    {
        const int WM_MOUSEWHEEL = 0x020A, EM_GETLINECOUNT = 0x00BA;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        /// <summary>依目前寬度實際換行後的行數（要有 handle，取 Handle 會自動建立）。</summary>
        public int LineCount { get { return SendMessage(Handle, EM_GETLINECOUNT, IntPtr.Zero, IntPtr.Zero).ToInt32(); } }

        const int EM_LINEINDEX = 0x00BB, EM_POSFROMCHAR = 0x00D6;

        /// <summary>一行的高度：有兩行以上就量第二行與第一行開頭的 y 差（TextBox 實際的行距），只有一行就用 GDI 量的字高。</summary>
        public int LinePitch
        {
            get
            {
                if (LineCount > 1)
                {
                    var second = SendMessage(Handle, EM_LINEINDEX, (IntPtr)1, IntPtr.Zero).ToInt32();
                    var y0 = (short)((SendMessage(Handle, EM_POSFROMCHAR, IntPtr.Zero, IntPtr.Zero).ToInt64() >> 16) & 0xFFFF);
                    var y1 = (short)((SendMessage(Handle, EM_POSFROMCHAR, (IntPtr)second, IntPtr.Zero).ToInt64() >> 16) & 0xFFFF);
                    if (y1 > y0) return y1 - y0;
                }
                return TextRenderer.MeasureText("國", Font).Height;
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.A) { SelectAll(); e.SuppressKeyPress = true; e.Handled = true; return; }
            base.OnKeyDown(e);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_MOUSEWHEEL)
            {
                Control p = Parent;
                while (p != null && !(p is TranscriptView)) p = p.Parent;
                var view = p as TranscriptView;
                if (view != null)
                {
                    var delta = (short)((m.WParam.ToInt64() >> 16) & 0xFFFF);
                    view.ScrollBy(delta);
                    return;
                }
            }
            base.WndProc(ref m);
        }
    }
}
