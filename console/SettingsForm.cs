using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace ChengMuConsole
{
    /// <summary>
    /// 連線設定：Account ID、Database ID（預先填好）、API Token（遮罩）。
    /// 已儲存的 Token 不會填回欄位：留空＝沿用原本那組，要換才輸入。
    /// </summary>
    sealed class SettingsForm : Form
    {
        const int W = 640, PadX = 28, FooterH = 60;
        readonly AppSettings original;
        TextBox txtAccount, txtDatabase, txtToken;
        Label lblResult;
        FlatButton btnTest, btnSave, btnCancel;
        CancellationTokenSource testCts;

        public AppSettings Result { get; private set; }

        public SettingsForm(AppSettings current, string reason)
        {
            original = current;
            Text = "澄沐 中控台 — 連線設定";
            Font = Ui.Font(11);
            BackColor = Color.White;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;

            Controls.Add(new TitleLabel { Text = "連線設定", Location = new Point(PadX, 18), Size = new Size(W - PadX * 2, 36) });
            var desc = Ui.Text(reason ?? "對話紀錄存在 Cloudflare D1（chengmu-chat-log）。這裡填讀取用的 API Token；Account ID 與 Database ID 已經填好，通常不用改。",
                58, W - PadX * 2, false, 10.5f, RailTheme.Muted);
            desc.Left = PadX;
            Controls.Add(desc);
            var y = desc.Bottom + 12;

            var card = new CardPanel(Color.White, RailTheme.Border) { Location = new Point(PadX, y), Size = new Size(W - PadX * 2, 10) };
            Controls.Add(card);
            const int fx = 150, rowH = 44;
            var fw = card.Width - fx - 18;
            var yy = 12;
            Field(card, "Account ID", yy, rowH);
            txtAccount = new TextBox { Location = new Point(fx, yy + 8), Width = fw, Text = current.AccountId, Font = Ui.Latin(11) };
            card.Controls.Add(txtAccount);
            yy += rowH; card.Rules.Add(yy); yy += 4;

            Field(card, "Database ID", yy, rowH);
            txtDatabase = new TextBox { Location = new Point(fx, yy + 8), Width = fw, Text = current.DatabaseId, Font = Ui.Latin(11) };
            card.Controls.Add(txtDatabase);
            yy += rowH; card.Rules.Add(yy); yy += 4;

            Field(card, "API Token", yy, rowH);
            txtToken = new TextBox { Location = new Point(fx, yy + 8), Width = fw, UseSystemPasswordChar = true, Font = Ui.Latin(11) };
            card.Controls.Add(txtToken);
            yy += rowH;
            card.Controls.Add(new Label
            {
                Location = new Point(fx, yy - 4), Size = new Size(fw, 46), Font = Ui.Font(10), ForeColor = RailTheme.Muted,
                Text = current.HasToken ? "已經儲存一組 Token（加密存在這台電腦）。留空＝沿用，要換才輸入新的。"
                                        : "在 Cloudflare 儀表板「我的設定檔 → API Token」建立，權限選「帳戶 → D1 → 讀取」。",
            });
            yy += 46;
            card.Height = yy + 8;

            var hint = Ui.Text("Token 以 Windows 帳號加密（DPAPI）存在 " + AppSettings.FilePath + "，只有這台電腦的這個帳號解得開。",
                card.Bottom + 10, W - PadX * 2, false, 10f, DeepTheme.Icon);
            hint.Left = PadX;
            Controls.Add(hint);

            var footerY = hint.Bottom + 14;
            ClientSize = new Size(W, footerY + FooterH);
            var f = new Panel { Location = new Point(0, footerY), Size = new Size(W, FooterH), BackColor = RailTheme.FooterBg };
            f.Paint += (s, e) => { using (var pen = new Pen(RailTheme.FooterLine)) e.Graphics.DrawLine(pen, 0, 0, f.Width, 0); };
            Controls.Add(f);
            btnTest = new FlatButton("測試連線", ButtonTone.Secondary) { Icon = IconKind.Database, Location = new Point(PadX, 13), Size = new Size(128, 34) };
            f.Controls.Add(btnTest);
            lblResult = new Label
            {
                Location = new Point(PadX + 136, 1), Size = new Size(W - PadX - 136 - 236, FooterH - 2), TextAlign = ContentAlignment.MiddleLeft,
                Font = Ui.Font(10), ForeColor = DeepTheme.FooterInk, AutoEllipsis = true,
            };
            f.Controls.Add(lblResult);
            btnCancel = new FlatButton("取消", ButtonTone.Secondary) { Location = new Point(W - PadX - 220, 13), Size = new Size(100, 34), DialogResult = DialogResult.Cancel };
            f.Controls.Add(btnCancel);
            btnSave = new FlatButton("儲存", ButtonTone.Primary) { Location = new Point(W - PadX - 110, 13), Size = new Size(110, 34) };
            f.Controls.Add(btnSave);
            CancelButton = btnCancel;
            AcceptButton = btnSave;
            // 通常只需要填 token：游標直接放那裡（不然 Account ID 會整段反白，一打字就蓋掉）
            ActiveControl = txtToken;

            btnTest.Click += async (s, e) => await OnTest();
            btnSave.Click += (s, e) => OnSave();
            FormClosed += (s, e) => { if (testCts != null) testCts.Cancel(); };
        }

        static void Field(Control c, string text, int y, int h)
        {
            c.Controls.Add(new Label
            {
                Text = text, Location = new Point(18, y), Size = new Size(126, h), TextAlign = ContentAlignment.MiddleLeft,
                Font = Ui.Font(10.5f, true), ForeColor = DeepTheme.Title,
            });
        }

        // 欄位組成的設定；token 留空就用原本的
        AppSettings FromFields(out string error)
        {
            error = null;
            var s = original.Clone();
            s.AccountId = txtAccount.Text.Trim();
            s.DatabaseId = txtDatabase.Text.Trim();
            var t = txtToken.Text.Trim();
            if (t != "") s.Token = t;
            if (!AppSettings.IsValidAccountId(s.AccountId)) error = "Account ID 應該是 32 個英數字（0-9、a-f）。";
            else if (!AppSettings.IsValidDatabaseId(s.DatabaseId)) error = "Database ID 格式不對（應該像 33bab8c1-cd58-48a4-94c6-4754e13fe36f）。";
            else if (!s.HasToken) error = "請輸入 API Token。";
            return s;
        }

        async System.Threading.Tasks.Task OnTest()
        {
            string error;
            var s = FromFields(out error);
            if (error != null) { SetResult(error, RailTheme.Red); return; }
            btnTest.Enabled = btnSave.Enabled = false;
            SetResult("連線中…", DeepTheme.FooterInk);
            testCts = new CancellationTokenSource();
            try
            {
                var n = await D1Client.CountAsync(s, testCts.Token);
                SetResult(string.Format("連線成功，資料庫共有 {0:N0} 筆訊息。", n), Ui.Green);
            }
            catch (D1Exception ex)
            {
                SetResult("連線失敗", RailTheme.Red);
                if (!IsDisposed) Ui.Info(ex.FullText, "測試連線", MessageBoxIcon.Warning);
            }
            catch (OperationCanceledException) { return; }
            finally
            {
                if (!IsDisposed) btnTest.Enabled = btnSave.Enabled = true;
            }
        }

        void SetResult(string text, Color color)
        {
            if (IsDisposed) return;
            lblResult.Text = text;
            lblResult.ForeColor = color;
        }

        void OnSave()
        {
            string error;
            var s = FromFields(out error);
            if (error != null) { SetResult(error, RailTheme.Red); Ui.Info(error, "連線設定"); return; }
            try { s.Save(); }
            catch (Exception ex) when (ex is System.IO.IOException || ex is UnauthorizedAccessException || ex is System.Security.Cryptography.CryptographicException)
            {
                Ui.Info("設定存不進去：" + ex.Message, "連線設定", MessageBoxIcon.Error);
                return;
            }
            Result = s;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
