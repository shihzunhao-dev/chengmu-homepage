using System;
using System.Drawing;
using System.Windows.Forms;

namespace ChengMuConsole
{
    // =============================================================================
    //  版面：家族的側欄導覽（SE-Deploy／CCIMS 中控台同一套元件，見 Ui*.cs）
    //
    //  左邊深海藍側欄＝日期清單（台灣時間，新到舊），選中的那天用家族的膠囊。
    //  右邊白色內容區：上面一列是標題＋搜尋＋範圍＋重新整理／匯出／設定，標題下面一行是狀態；
    //  下面兩張卡：中間「客戶」（依 IP 分組）、右邊「對話內容」（泡泡）。
    //  家族的視窗是固定 880×690；這支要同時看三欄，預設 1280×800、可以拉大，元件都用 Anchor 跟著伸縮。
    // =============================================================================
    sealed partial class MainForm
    {
        const int FormW = 1280, FormH = 800, RailW = 220, PageX = 28, CustW = 420, CardsY = 100;

        Panel content;
        TitleLabel title;
        Label lblStatus;
        TextBox txtSearch;
        ComboBox cboRange;
        FlatButton btnRefresh, btnExport, btnSettings, btnCopy;
        DateList dateList;
        CustomerList customerList;
        TranscriptView transcript;
        CardHeader custHeader, chatHeader;

        static readonly string[] RangeNames = { "最近 7 天", "最近 30 天", "最近 90 天", "最近 1 年", "全部紀錄" };

        void BuildLayout()
        {
            Text = "澄沐 中控台 — 官網客服紀錄 v" + Program.Version;
            ClientSize = new Size(FormW, FormH);
            MinimumSize = new Size(1100, 640);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.Sizable;
            Font = Ui.Font(11);
            BackColor = Color.White;
            KeyPreview = true;

            BuildRail();
            BuildContent();
        }

        void BuildRail()
        {
            var p = new RailPanel(RailVariant.Normal) { Location = new Point(0, 0), Size = new Size(RailW, FormH), Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left };
            Controls.Add(p);
            p.Controls.Add(new RailLabel { Text = "澄沐 中控台", Font = Ui.Font(16.5f, true), Location = new Point(24, 26), Size = new Size(RailW - 30, 34) });
            // 版號用 Segoe UI：微軟正黑體的「1」底下有橫腳（照家族側欄）
            p.Controls.Add(new RailLabel
            {
                Text = "官網客服紀錄 v" + Program.Version, Font = Ui.Font(10), ForeColor = DeepTheme.RailSub,
                Location = new Point(26, 62), Size = new Size(RailW - 32, 22),
            });
            p.Controls.Add(new RailLabel
            {
                Text = "日期（台灣時間）", Font = Ui.Font(10, true), ForeColor = DeepTheme.RailSub,
                Location = new Point(26, 100), Size = new Size(RailW - 32, 24),
            });
            dateList = new DateList
            {
                Location = new Point(0, 128), Size = new Size(RailW, FormH - 128),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            };
            p.Controls.Add(dateList);
        }

        void BuildContent()
        {
            var cw = FormW - RailW;
            content = new Panel { Location = new Point(RailW, 0), Size = new Size(cw, FormH), BackColor = Color.White, Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
            Raised.Attach(content);
            Controls.Add(content);

            title = new TitleLabel { Text = "官網客服紀錄", Location = new Point(PageX, 16), Size = new Size(200, 40) };
            content.Controls.Add(title);
            lblStatus = new Label
            {
                Location = new Point(PageX, 60), Size = new Size(cw - PageX * 2, 26), AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft,
                Font = Ui.Font(10.5f), ForeColor = RailTheme.Muted, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Text = "準備中…",
            };
            content.Controls.Add(lblStatus);

            // 右上的工具列：由右往左排
            const int barY = 18, barH = 36;
            var right = cw - PageX;
            btnSettings = new FlatButton("設定", ButtonTone.Secondary) { Icon = IconKind.Gear, Size = new Size(96, barH) };
            btnExport = new FlatButton("匯出", ButtonTone.Secondary) { Icon = IconKind.Download, Size = new Size(96, barH) };
            btnRefresh = new FlatButton("重新整理", ButtonTone.Primary) { Icon = IconKind.Restore, Size = new Size(128, barH) };
            foreach (var b in new Control[] { btnSettings, btnExport, btnRefresh })
            {
                right -= b.Width;
                b.Location = new Point(right, barY);
                b.Anchor = AnchorStyles.Top | AnchorStyles.Right;
                content.Controls.Add(b);
                right -= 8;
            }
            cboRange = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Font = Ui.Font(11), Width = 124, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            cboRange.Items.AddRange(RangeNames);
            right -= cboRange.Width + 4;
            cboRange.Location = new Point(right, barY + (barH - cboRange.PreferredHeight) / 2);
            content.Controls.Add(cboRange);
            right -= 10;

            // 搜尋框：圓角白框裡放放大鏡＋無框 TextBox
            const int searchW = 260;
            right -= searchW;
            var sbox = new CardPanel(Color.White, Color.FromArgb(201, 211, 222))
            {
                Location = new Point(right, barY), Size = new Size(searchW, barH), Radius = 8, HasShadow = false,
                BorderBottom = Color.FromArgb(181, 193, 207), Anchor = AnchorStyles.Top | AnchorStyles.Right, Cursor = Cursors.IBeam,
            };
            content.Controls.Add(sbox);
            sbox.Controls.Add(new IconBox(IconKind.Search, RailTheme.Muted) { Location = new Point(10, (barH - 18) / 2), Size = new Size(18, 18), Stroke = 1.8f });
            txtSearch = new TextBox { BorderStyle = BorderStyle.None, Font = Ui.Font(11), Location = new Point(36, 8), Width = searchW - 46, BackColor = Color.White };
            txtSearch.Top = (barH - txtSearch.PreferredHeight) / 2 + 1;
            sbox.Controls.Add(txtSearch);
            sbox.Click += (s, e) => txtSearch.Focus();
            Cue(txtSearch, "搜尋對話內容、IP…");

            // 兩張卡
            var cardsH = FormH - CardsY - 22;
            var cust = new CardPanel(Color.White, RailTheme.Border)
            {
                Location = new Point(PageX, CardsY), Size = new Size(CustW, cardsH), Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left,
            };
            content.Controls.Add(cust);
            custHeader = new CardHeader(IconKind.User, "客戶") { Location = new Point(18, 6), Size = new Size(CustW - 36, 42), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            cust.Controls.Add(custHeader);
            cust.Rules.Add(50);
            customerList = new CustomerList
            {
                Location = new Point(4, 54), Size = new Size(CustW - 10, cardsH - 62), Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            };
            cust.Controls.Add(customerList);

            var chatX = PageX + CustW + 16;
            var chatW = cw - chatX - PageX;
            var chat = new CardPanel(Color.White, RailTheme.Border)
            {
                Location = new Point(chatX, CardsY), Size = new Size(chatW, cardsH), Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            };
            content.Controls.Add(chat);
            btnCopy = new FlatButton("複製全部", ButtonTone.Outline) { Icon = IconKind.Copy, Location = new Point(chatW - 18 - 118, 11), Size = new Size(118, 32), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            chat.Controls.Add(btnCopy);
            chatHeader = new CardHeader(IconKind.Chat, "對話內容") { Location = new Point(18, 6), Size = new Size(chatW - 36 - 126, 42), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            chat.Controls.Add(chatHeader);
            chat.Rules.Add(50);
            transcript = new TranscriptView
            {
                Location = new Point(6, 54), Size = new Size(chatW - 14, cardsH - 62), Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            };
            chat.Controls.Add(transcript);
        }

        // 搜尋框的淡色提示字（EM_SETCUEBANNER）
        static void Cue(TextBox t, string text)
        {
            t.HandleCreated += (s, e) => NativeMethods.SendMessage(t.Handle, 0x1501, (IntPtr)1, text);
            if (t.IsHandleCreated) NativeMethods.SendMessage(t.Handle, 0x1501, (IntPtr)1, text);
        }
    }

    static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);
    }
}
