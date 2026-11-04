using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ChengMuConsole
{
    // =============================================================================
    //  可捲動的清單（自己畫）：側欄的日期、中間的客人。
    //  不用 ListBox：ListBox 的底色只能是單色，放在漸層側欄上會蓋出一塊方形，DrawToBitmap 也不可靠（見 UiDeep.cs）。
    //  操作跟 ListBox 一樣：點一下選取、滾輪捲動、上下鍵／Home／End／PageUp／PageDown 換選取，右邊細捲軸可以拖。
    // =============================================================================
    public abstract class RowList : Control
    {
        int count, selected = -1, hover = -1, scroll;
        bool dragging;
        int dragStartY, dragStartScroll;

        public int RowHeight { get; set; }
        public int PadTop { get; set; }
        public int PadX { get; set; }
        public string EmptyText { get; set; }
        public event EventHandler SelectedIndexChanged;

        protected abstract Color ThumbColor { get; }
        protected abstract Color EmptyInk { get; }
        protected abstract void PaintBack(Graphics g);
        protected abstract void PaintRow(Graphics g, int index, Rectangle r, bool isSelected, bool isHover);

        protected RowList()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            TabStop = true;
            RowHeight = 56;
            PadTop = 4;
            PadX = 8;
            EmptyText = "";
        }

        public int Count { get { return count; } }

        public int SelectedIndex
        {
            get { return selected; }
            set
            {
                var v = count == 0 ? -1 : Math.Max(-1, Math.Min(count - 1, value));
                if (v == selected) return;
                selected = v;
                EnsureVisible(v);
                Invalidate();
                var h = SelectedIndexChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
        }

        /// <summary>換一批資料：選取保留在 keep（超出範圍就選第一列），不觸發兩次事件。</summary>
        protected void ResetRows(int n, int keep)
        {
            count = n;
            hover = -1;
            selected = -2;   // 一定跟新值不同，SelectedIndex 才會發事件
            scroll = 0;
            SelectedIndex = n == 0 ? -1 : (keep >= 0 && keep < n ? keep : 0);
            if (n == 0) Invalidate();
        }

        int ContentHeight { get { return PadTop * 2 + count * RowHeight; } }
        int MaxScroll { get { return Math.Max(0, ContentHeight - Height); } }
        bool CanScroll { get { return MaxScroll > 0; } }
        protected int RowWidth { get { return Width - PadX * 2 - (CanScroll ? 8 : 0); } }

        void SetScroll(int v)
        {
            v = Math.Max(0, Math.Min(MaxScroll, v));
            if (v == scroll) return;
            scroll = v;
            Invalidate();
        }

        void EnsureVisible(int i)
        {
            if (i < 0) return;
            var top = PadTop + i * RowHeight;
            if (top < scroll) SetScroll(top - PadTop);
            else if (top + RowHeight > scroll + Height) SetScroll(top + RowHeight - Height + PadTop);
        }

        int IndexAt(int y)
        {
            var i = (y + scroll - PadTop) / RowHeight;
            return y + scroll - PadTop >= 0 && i >= 0 && i < count ? i : -1;
        }

        Rectangle ThumbRect
        {
            get
            {
                if (!CanScroll) return Rectangle.Empty;
                var h = Math.Max(30, Height * Height / ContentHeight);
                var y = (Height - h) * scroll / MaxScroll;
                return new Rectangle(Width - 8, y, 6, h);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            PaintBack(g);
            if (count == 0)
            {
                using (var b = new SolidBrush(EmptyInk))
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near })
                    g.DrawString(EmptyText, Font, b, new RectangleF(PadX, 24, Width - PadX * 2, Height - 24), sf);
                return;
            }
            var first = Math.Max(0, (scroll - PadTop) / RowHeight);
            for (int i = first; i < count; i++)
            {
                var y = PadTop + i * RowHeight - scroll;
                if (y > Height) break;
                PaintRow(g, i, new Rectangle(PadX, y, RowWidth, RowHeight), i == selected, i == hover);
            }
            if (CanScroll)
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                RailTheme.FillRound(g, ThumbColor, null, ThumbRect, 3);
            }
            if (Focused && ShowFocusCues && selected >= 0)
            {
                var y = PadTop + selected * RowHeight - scroll;
                ControlPaint.DrawFocusRectangle(g, new Rectangle(PadX + 2, y + 2, RowWidth - 4, RowHeight - 6));
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            var t = ThumbRect;
            if (CanScroll && e.X >= Width - 12)
            {
                if (t.Contains(new Point(t.X, e.Y))) { dragging = true; dragStartY = e.Y; dragStartScroll = scroll; Capture = true; }
                else SetScroll(scroll + (e.Y < t.Y ? -Height : Height));
                return;
            }
            var i = IndexAt(e.Y);
            if (i >= 0) SelectedIndex = i;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (dragging)
            {
                var track = Height - ThumbRect.Height;
                if (track > 0) SetScroll(dragStartScroll + (e.Y - dragStartY) * MaxScroll / track);
                return;
            }
            var i = e.X >= Width - 12 && CanScroll ? -1 : IndexAt(e.Y);
            if (i != hover) { hover = i; Invalidate(); }
            Cursor = i >= 0 ? Cursors.Hand : Cursors.Default;
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (dragging) { dragging = false; Capture = false; }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hover != -1) { hover = -1; Invalidate(); }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            SetScroll(scroll - e.Delta / 120 * RowHeight * 2);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            SetScroll(scroll);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData & Keys.KeyCode)
            {
                case Keys.Up: case Keys.Down: case Keys.Home: case Keys.End: case Keys.PageUp: case Keys.PageDown: return true;
            }
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (count == 0) return;
            var page = Math.Max(1, Height / RowHeight - 1);
            switch (e.KeyCode)
            {
                case Keys.Up: SelectedIndex = Math.Max(0, selected - 1); break;
                case Keys.Down: SelectedIndex = Math.Min(count - 1, selected + 1); break;
                case Keys.Home: SelectedIndex = 0; break;
                case Keys.End: SelectedIndex = count - 1; break;
                case Keys.PageUp: SelectedIndex = Math.Max(0, selected - page); break;
                case Keys.PageDown: SelectedIndex = Math.Min(count - 1, selected + page); break;
                default: return;
            }
            e.Handled = true;
        }

        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        protected static readonly StringFormat LeftOneLine = new StringFormat
        {
            LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap,
        };
        protected static readonly StringFormat RightOneLine = new StringFormat
        {
            Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap,
        };
    }

    /// <summary>側欄的日期清單：選中的那天用家族的膠囊（綠到藍漸層＋黃綠邊條）。</summary>
    public sealed class DateList : RowList
    {
        readonly Font dayFont = Ui.Font(12, true), countFont = Ui.Font(10.5f), tagFont = Ui.Font(10, true);
        List<Day> days = new List<Day>();

        public DateList()
        {
            RowHeight = 60;
            PadX = 12;
            Font = Ui.Font(11);
            EmptyText = "沒有對話紀錄";
        }

        public Day SelectedDay { get { return SelectedIndex >= 0 && SelectedIndex < days.Count ? days[SelectedIndex] : null; } }

        public void SetDays(List<Day> list, DateTime? keepDate)
        {
            days = list ?? new List<Day>();
            var keep = keepDate.HasValue ? days.FindIndex(d => d.Date == keepDate.Value) : 0;
            ResetRows(days.Count, keep);
        }

        protected override Color ThumbColor { get { return Color.FromArgb(90, 255, 255, 255); } }
        protected override Color EmptyInk { get { return DeepTheme.RailSub; } }
        protected override void PaintBack(Graphics g) { RailPanel.PaintUnder(this, g); }

        protected override void PaintRow(Graphics g, int index, Rectangle r, bool isSelected, bool isHover)
        {
            var d = days[index];
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var box = new RectangleF(r.X + 0.5f, r.Y + 0.5f, r.Width - 1.5f, r.Height - 5.5f);
            if (isSelected) DeepTheme.FillPill(g, box, RailVariant.Normal);
            else if (isHover) RailTheme.FillRound(g, Color.FromArgb(26, 255, 255, 255), null, box, 8);

            var ink = isSelected ? Color.White : RailTheme.RailText;
            var sub = isSelected ? Color.FromArgb(235, 255, 255, 255) : DeepTheme.RailSub;
            var half = (r.Height - 5) / 2f;
            var today = TwTime.Now.Date;
            var tag = d.Date == today ? "今天" : d.Date == today.AddDays(-1) ? "昨天" : "";
            using (var b = new SolidBrush(ink))
                g.DrawString(TwTime.DayLabel(d.Date), dayFont, b, new RectangleF(r.X + 14, r.Y + 5, r.Width - 64, half), LeftOneLine);
            if (tag != "")
                using (var b = new SolidBrush(isSelected ? Color.White : RailTheme.RailText))
                    g.DrawString(tag, tagFont, b, new RectangleF(r.Right - 60, r.Y + 5, 50, half), RightOneLine);
            using (var b = new SolidBrush(sub))
                g.DrawString(string.Format("{0} 位客戶・{1} 則提問", d.Customers.Count, d.QuestionCount), countFont, b,
                    new RectangleF(r.X + 14, r.Y + half - 1, r.Width - 20, half), LeftOneLine);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { dayFont.Dispose(); countFont.Dispose(); tagFont.Dispose(); }
            base.Dispose(disposing);
        }
    }

    /// <summary>中間的客人清單：一個 IP 一列，第一行 IP＋最後提問時間，第二行地區・對話數・提問數。</summary>
    public sealed class CustomerList : RowList
    {
        readonly Font ipFont = Ui.Latin(12, true), noIpFont = Ui.Font(11.5f, true), timeFont = Ui.Latin(11), subFont = Ui.Font(10.5f), smallFont = Ui.Font(10);
        List<Customer> customers = new List<Customer>();

        public CustomerList()
        {
            RowHeight = 68;
            PadX = 10;
            PadTop = 2;
            Font = Ui.Font(11);
            BackColor = Color.White;
            EmptyText = "這一天沒有客人";
        }

        public Customer SelectedCustomer { get { return SelectedIndex >= 0 && SelectedIndex < customers.Count ? customers[SelectedIndex] : null; } }

        public void SetCustomers(List<Customer> list, string keepIp)
        {
            customers = list ?? new List<Customer>();
            ResetRows(customers.Count, keepIp == null ? 0 : customers.FindIndex(c => c.IpLabel == keepIp));
        }

        protected override Color ThumbColor { get { return Color.FromArgb(197, 206, 217); } }
        protected override Color EmptyInk { get { return RailTheme.Muted; } }
        protected override void PaintBack(Graphics g) { g.Clear(BackColor); }

        protected override void PaintRow(Graphics g, int index, Rectangle r, bool isSelected, bool isHover)
        {
            var c = customers[index];
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var box = new RectangleF(r.X + 0.5f, r.Y + 3.5f, r.Width - 1.5f, r.Height - 7);
            if (isSelected)
            {
                RailTheme.FillRound(g, RailTheme.AccentSoft, Color.FromArgb(211, 230, 248), box, 8);
                RailTheme.FillRound(g, RailTheme.Accent, null, new RectangleF(box.X, box.Y + (box.Height - 30) / 2, 3.5f, 30), 1.5f);
            }
            else if (isHover) RailTheme.FillRound(g, Color.FromArgb(246, 249, 252), null, box, 8);
            else if (index > 0)
                using (var pen = new Pen(DeepTheme.RowLine)) g.DrawLine(pen, r.X + 12, r.Y + 0.5f, r.Right - 12, r.Y + 0.5f);

            var half = (r.Height - 8) / 2f;
            var top = r.Y + 5;
            var timeText = "最後 " + c.LastQuestionTw.ToString("HH:mm");
            var tw = g.MeasureString(timeText, timeFont).Width + 4;
            using (var b = new SolidBrush(c.Ip == null ? RailTheme.Muted : DeepTheme.Title))
                g.DrawString(c.IpLabel, c.Ip == null ? noIpFont : ipFont, b, new RectangleF(r.X + 14, top, r.Width - 28 - tw, half), LeftOneLine);
            using (var b = new SolidBrush(RailTheme.Muted))
            {
                g.DrawString(timeText, timeFont, b, new RectangleF(r.Right - 12 - tw, top, tw, half), RightOneLine);
                g.DrawString(string.Format("{0}・{1} 段對話・{2} 則提問", c.Region, c.Conversations.Count, c.QuestionCount), subFont, b,
                    new RectangleF(r.X + 14, top + half, r.Width - 26, half), LeftOneLine);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { ipFont.Dispose(); noIpFont.Dispose(); timeFont.Dispose(); subFont.Dispose(); smallFont.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
