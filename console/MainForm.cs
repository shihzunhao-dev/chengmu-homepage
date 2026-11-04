using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ChengMuConsole
{
    /// <summary>主視窗：讀資料、分組、搜尋、匯出。版面在 MainForm.Layout.cs。</summary>
    sealed partial class MainForm : Form
    {
        AppSettings settings;
        readonly bool offline;          // --screenshot：用 fixture 資料，不連網、不跳設定視窗
        readonly string startWarning;
        LoadResult data;
        List<Day> days = new List<Day>();
        string keyword = "";
        string keepIp;                  // 重新分組後要選回的客人
        DateTime? lastRefreshTw;
        bool loading;
        CancellationTokenSource cts;
        readonly System.Windows.Forms.Timer searchTimer = new System.Windows.Forms.Timer { Interval = 350 };
        string errorText;               // 最近一次讀取失敗的說明（狀態列紅字）

        public MainForm(AppSettings settings, LoadResult fixture, string startWarning)
        {
            this.settings = settings;
            this.startWarning = startWarning;
            offline = fixture != null;
            BuildLayout();

            cboRange.SelectedIndex = Math.Max(0, Array.IndexOf(AppSettings.DayChoices, settings.Days));
            dateList.SelectedIndexChanged += (s, e) => OnDayChanged();
            customerList.SelectedIndexChanged += (s, e) => OnCustomerChanged();
            btnRefresh.Click += async (s, e) => await RefreshAsync();
            btnExport.Click += (s, e) => OnExport();
            btnSettings.Click += async (s, e) => { if (OpenSettings(null)) await RefreshAsync(); };
            btnCopy.Click += (s, e) => OnCopy();
            cboRange.SelectedIndexChanged += async (s, e) => await OnRangeChanged();
            txtSearch.TextChanged += (s, e) => { searchTimer.Stop(); searchTimer.Start(); };
            txtSearch.KeyDown += OnSearchKey;
            searchTimer.Tick += (s, e) => { searchTimer.Stop(); ApplySearch(); };
            KeyDown += async (s, e) => { if (e.KeyCode == Keys.F5) { e.Handled = true; await RefreshAsync(); } };
            FormClosed += (s, e) => { if (cts != null) cts.Cancel(); searchTimer.Dispose(); };

            if (offline)
            {
                ApplyData(fixture);
            }
            else
            {
                ShowNoData(settings.HasToken ? "讀取中…" : "還沒有設定 API Token，請按右上角「設定」。");
                Shown += async (s, e) => await OnFirstShown();
            }
        }

        async Task OnFirstShown()
        {
            if (!settings.HasToken)
            {
                if (!OpenSettings(startWarning ?? "第一次使用：請貼上 Cloudflare API Token（D1 讀取權限）。Account ID 與 Database ID 已經填好，通常不用改。"))
                {
                    SetStatus("還沒有設定 API Token，請按右上角「設定」。", true);
                    return;
                }
            }
            else if (startWarning != null) Ui.Info(startWarning, Text);
            await RefreshAsync();
        }

        bool OpenSettings(string reason)
        {
            using (var f = new SettingsForm(settings, reason))
            {
                if (f.ShowDialog(this) != DialogResult.OK || f.Result == null) return false;
                settings = f.Result;
                return true;
            }
        }

        // -------------------------------------------------------------------------
        //  讀資料（背景執行，不鎖畫面）
        // -------------------------------------------------------------------------
        DateTime? SinceUtc()
        {
            if (settings.Days <= 0) return null;
            // 今天算一天：最近 30 天＝今天加上前 29 天，從那天台灣時間 00:00 起
            return TwTime.ToUtc(TwTime.Now.Date.AddDays(-(settings.Days - 1)));
        }

        string RangeText()
        {
            if (settings.Days <= 0) return "全部紀錄";
            var from = TwTime.Now.Date.AddDays(-(settings.Days - 1));
            return string.Format("最近 {0} 天（{1:yyyy-MM-dd} ～ {2:yyyy-MM-dd}，台灣時間）", settings.Days, from, TwTime.Now.Date);
        }

        async Task RefreshAsync()
        {
            if (offline || loading) return;
            if (!settings.HasToken) { SetStatus("還沒有設定 API Token，請按右上角「設定」。", true); return; }
            loading = true;
            btnRefresh.Enabled = btnExport.Enabled = cboRange.Enabled = false;
            btnRefresh.Text = "讀取中…";
            SetStatus("正在從 Cloudflare 讀取" + RangeText() + "的對話紀錄…", false);
            cts = new CancellationTokenSource();
            try
            {
                var result = await D1Client.LoadAsync(settings, SinceUtc(), cts.Token);
                if (IsDisposed) return;
                errorText = null;
                ApplyData(result);
            }
            catch (OperationCanceledException) { return; }
            catch (D1Exception ex)
            {
                if (IsDisposed) return;
                errorText = "讀取失敗：" + ex.Message.Replace("\r\n", " ");
                UpdateStatus();
                if (ex.Kind == D1ErrorKind.Auth || ex.Kind == D1ErrorKind.NotFound)
                {
                    if (MessageBox.Show(ex.FullText + "\r\n\r\n要現在開啟「連線設定」嗎？", "讀取失敗", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes
                        && OpenSettings(null))
                    {
                        loading = false;
                        RestoreButtons();
                        await RefreshAsync();
                        return;
                    }
                }
                else Ui.Info(ex.FullText, "讀取失敗");
            }
            finally
            {
                loading = false;
                if (!IsDisposed) RestoreButtons();
            }
        }

        void RestoreButtons()
        {
            btnRefresh.Enabled = btnExport.Enabled = cboRange.Enabled = true;
            btnRefresh.Text = "重新整理";
        }

        async Task OnRangeChanged()
        {
            var choice = AppSettings.DayChoices[Math.Max(0, cboRange.SelectedIndex)];
            if (choice == settings.Days) return;
            settings.Days = choice;
            TrySave();
            await RefreshAsync();
        }

        void TrySave()
        {
            if (offline) return;
            try { settings.Save(); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is System.Security.Cryptography.CryptographicException)
            {
                SetStatus("設定存不進去：" + ex.Message, true);
            }
        }

        void ApplyData(LoadResult result)
        {
            data = result;
            lastRefreshTw = TwTime.Now;
            Regroup();
        }

        // -------------------------------------------------------------------------
        //  分組、搜尋、選取
        // -------------------------------------------------------------------------
        void Regroup()
        {
            var keepDay = dateList.SelectedDay != null ? dateList.SelectedDay.Date : (DateTime?)null;
            keepIp = customerList.SelectedCustomer != null ? customerList.SelectedCustomer.IpLabel : null;
            var rows = data == null ? new List<ChatRow>() : ChatGrouping.Filter(data.Rows, keyword);
            days = ChatGrouping.Group(rows);
            dateList.EmptyText = keyword != "" ? "沒有符合的對話" : "這段期間沒有對話紀錄";
            dateList.SetDays(days, keepDay);
            if (days.Count == 0) OnDayChanged();
            keepIp = null;
            UpdateStatus();
        }

        void OnDayChanged()
        {
            var d = dateList.SelectedDay;
            if (d == null)
            {
                custHeader.Note = "";
                customerList.EmptyText = data == null ? "" : keyword != "" ? "沒有符合「" + keyword + "」的對話" : "這段期間沒有對話紀錄";
                customerList.SetCustomers(new List<Customer>(), null);
                return;
            }
            custHeader.Note = string.Format("{0}・{1} 位", TwTime.DayLabel(d.Date), d.Customers.Count);
            customerList.EmptyText = "這一天沒有客人";
            customerList.SetCustomers(d.Customers, keepIp);
        }

        void OnCustomerChanged()
        {
            var d = dateList.SelectedDay;
            var c = customerList.SelectedCustomer;
            btnCopy.Enabled = c != null;
            if (d == null || c == null)
            {
                chatHeader.Note = "";
                transcript.ShowEmpty(data == null ? "" : days.Count == 0 ? "沒有可以顯示的對話。" : "請在中間選一位客人。");
                return;
            }
            chatHeader.Note = c.IpLabel + "・" + c.Region;
            transcript.ShowCustomer(c, keyword);
        }

        void ShowNoData(string message)
        {
            dateList.EmptyText = "";
            dateList.SetDays(new List<Day>(), null);
            customerList.EmptyText = "";
            customerList.SetCustomers(new List<Customer>(), null);
            transcript.ShowEmpty(message);
            btnCopy.Enabled = false;
            SetStatus(message, !settings.HasToken);
        }

        void OnSearchKey(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter) { searchTimer.Stop(); ApplySearch(); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Escape) { txtSearch.Text = ""; searchTimer.Stop(); ApplySearch(); e.SuppressKeyPress = true; }
        }

        void ApplySearch()
        {
            var k = txtSearch.Text.Trim();
            if (k == keyword) return;
            keyword = k;
            Regroup();
        }

        // -------------------------------------------------------------------------
        //  狀態列（標題下面那一行）
        // -------------------------------------------------------------------------
        void UpdateStatus()
        {
            if (errorText != null) { SetStatus(errorText + (lastRefreshTw.HasValue ? "（畫面上是 " + lastRefreshTw.Value.ToString("HH:mm") + " 讀到的資料）" : ""), true); return; }
            if (data == null) return;
            var all = ChatGrouping.Group(data.Rows);
            var parts = new List<string>();
            parts.Add("上次更新 " + lastRefreshTw.Value.ToString("yyyy-MM-dd HH:mm:ss"));
            parts.Add(string.Format("{0}：{1} 天有對話、{2} 位客戶、{3} 則提問", settings.Days > 0 ? "最近 " + settings.Days + " 天" : "全部紀錄",
                all.Count, all.Sum(d => d.Customers.Count), all.Sum(d => d.QuestionCount)));
            if (keyword != "")
                parts.Add(string.Format("搜尋「{0}」：{1} 段對話", keyword, days.Sum(d => d.Customers.Sum(c => c.Conversations.Count))));
            if (!data.HasIpColumn) parts.Add("資料庫還沒有 ip 欄位（migration 0002 未執行），IP 顯示「未記錄」");
            SetStatus(string.Join("　・　", parts), false);
        }

        void SetStatus(string text, bool isError)
        {
            lblStatus.Text = text;
            lblStatus.ForeColor = isError ? RailTheme.Red : RailTheme.Muted;
        }

        // -------------------------------------------------------------------------
        //  複製、匯出
        // -------------------------------------------------------------------------
        void OnCopy()
        {
            var d = dateList.SelectedDay;
            var c = customerList.SelectedCustomer;
            if (d == null || c == null) return;
            try
            {
                Clipboard.SetText(TranscriptView.PlainText(d, c));
                SetStatus("已複製 " + c.IpLabel + " 在 " + TwTime.DayLabel(d.Date) + " 的全部對話，可以直接貼到 LINE 或 Word。", false);
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                SetStatus("剪貼簿正被別的程式占用，請再按一次「複製全部」。", true);
            }
        }

        void OnExport()
        {
            if (data == null || data.Rows.Count == 0) { Ui.Info("目前沒有讀到任何對話紀錄，沒有東西可以匯出。", "匯出", MessageBoxIcon.Information); return; }
            var dir = string.IsNullOrEmpty(settings.ExportDir) ? AppSettings.DefaultExportDir : settings.ExportDir;
            try { Directory.CreateDirectory(dir); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is NotSupportedException || ex is ArgumentException)
            {
                // 這台電腦沒有 D 槽之類：改從「文件」開始選
                dir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }
            using (var dlg = new FolderBrowserDialog
            {
                Description = "選擇匯出資料夾（會產生 CSV 與 Markdown 兩個檔）。匯出的是" + RangeText() + "讀進來的全部紀錄，不受搜尋影響。",
                SelectedPath = dir, ShowNewFolderButton = true,
            })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                dir = dlg.SelectedPath;
            }
            Exporter.Result res;
            try { res = Exporter.Export(data.Rows, dir, RangeText(), TwTime.Now); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Ui.Info("匯出失敗：" + ex.Message, "匯出", MessageBoxIcon.Error);
                return;
            }
            if (!string.Equals(settings.ExportDir, dir, StringComparison.OrdinalIgnoreCase)) { settings.ExportDir = dir; TrySave(); }
            var msg = string.Format("已匯出 {0:N0} 則訊息：\r\n\r\n{1}\r\n{2}\r\n\r\n要開啟資料夾嗎？", res.Rows, Path.GetFileName(res.CsvPath), Path.GetFileName(res.MdPath));
            if (MessageBox.Show(msg, "匯出完成", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                Process.Start("explorer.exe", "/select,\"" + res.CsvPath + "\"");
        }

        // -------------------------------------------------------------------------
        //  --screenshot 用
        // -------------------------------------------------------------------------
        public void SelectRow(int dayIndex, int customerIndex)
        {
            if (dateList.Count > 0) dateList.SelectedIndex = Math.Min(dayIndex, dateList.Count - 1);
            if (customerList.Count > 0) customerList.SelectedIndex = Math.Min(customerIndex, customerList.Count - 1);
        }

        public Bitmap RenderClient()
        {
            var bmp = new Bitmap(ClientSize.Width, ClientSize.Height);
            using (var g = Graphics.FromImage(bmp)) g.Clear(BackColor);
            // Controls[0] 在最上層：從最底層畫起
            for (int i = Controls.Count - 1; i >= 0; i--)
            {
                var c = Controls[i];
                if (c.Visible) c.DrawToBitmap(bmp, c.Bounds);
            }
            return bmp;
        }
    }
}
