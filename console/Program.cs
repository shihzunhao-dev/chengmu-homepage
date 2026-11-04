using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ChengMuConsole
{
    // =============================================================================
    //  澄沐 中控台（官網客服紀錄）2026-11-04
    //
    //  看官網服務助理（worker-chat）存在 Cloudflare D1 的對話：左邊日期、中間依 IP 分的客人、右邊對話泡泡。
    //  只讀資料，不改 D1；token 用 DPAPI 加密存在 %APPDATA%\ChengMu-Console\settings.json。
    //  備援：worker-chat\export-chats.ps1（走 wrangler，不需要 token）。
    //
    //  隱藏的命令列模式（開發驗證用，不連網）：
    //    ChengMu-Console.exe --selftest <fixture.json> [匯出資料夾]   解析 D1 回應的 fixture，印出 日期→IP→對話 分組
    //    ChengMu-Console.exe --screenshot <fixture.json> <out.png> [日] [客]  用 fixture 畫主視窗（1280×800）存成 PNG
    // =============================================================================
    static class Program
    {
        public const string Version = "1.0.0";

        [STAThread]
        static int Main(string[] args)
        {
            args = args ?? new string[0];
            // 命令列模式不裝訊息框的錯誤處理：自動化跑的時候跳視窗會卡住
            if (args.Length > 0 && args[0] == "--selftest") return SelfTest.Run(args);
            Application.EnableVisualStyles();
            if (args.Length > 0 && args[0] == "--screenshot") return Screenshot(args);

            D1Client.UseTls12();
            // 不呼叫 SetCompatibleTextRenderingDefault(false)：自畫元件用 GDI+ 畫字，跟家族一樣（見 Ui.cs 開頭）
            Ui.InstallCrashHandlers("澄沐 中控台");
            string warning;
            var settings = AppSettings.Load(out warning);
            Application.Run(new MainForm(settings, null, warning));
            return 0;
        }

        static int Screenshot(string[] args)
        {
            var err = SelfTest.Stderr();
            if (args.Length < 3) { err.WriteLine("用法：--screenshot <fixture.json> <out.png>"); return 2; }
            try
            {
                var fixture = D1Client.FromFixture(File.ReadAllText(args[1], Encoding.UTF8));
                using (var f = new MainForm(new AppSettings(), fixture, null))
                {
                    f.StartPosition = FormStartPosition.Manual;
                    f.Location = new System.Drawing.Point(-10000, -10000);
                    f.ShowInTaskbar = false;
                    f.Show();
                    f.ClientSize = new System.Drawing.Size(1280, 800);
                    // 選填的第 4、5 個參數：第幾天、第幾位客人（從 0 起；預設都是第一個）
                    int day = 0, cust = 0;
                    if (args.Length > 3) int.TryParse(args[3], out day);
                    if (args.Length > 4) int.TryParse(args[4], out cust);
                    f.SelectRow(day, cust);
                    for (int i = 0; i < 5; i++) Application.DoEvents();
                    using (var bmp = f.RenderClient())
                    {
                        var dir = Path.GetDirectoryName(Path.GetFullPath(args[2]));
                        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                        bmp.Save(args[2], ImageFormat.Png);
                    }
                    f.Close();
                }
                SelfTest.Stdout().WriteLine("saved " + Path.GetFullPath(args[2]));
                return 0;
            }
            catch (Exception ex)
            {
                err.WriteLine("screenshot failed: " + ex);
                return 1;
            }
        }
    }

    /// <summary>--selftest：走跟正式查詢同一條解析路徑（D1Client.ParseResponse），印出分組結果，再測幾種錯誤回應。</summary>
    static class SelfTest
    {
        public static TextWriter Stdout() { return new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush = true }; }
        public static TextWriter Stderr() { return new StreamWriter(Console.OpenStandardError(), new UTF8Encoding(false)) { AutoFlush = true }; }

        public static int Run(string[] args)
        {
            var o = Stdout();
            if (args.Length < 2) { o.WriteLine("用法：--selftest <fixture.json> [匯出資料夾]"); return 2; }
            var failures = 0;
            try
            {
                var fixture = D1Client.FromFixture(File.ReadAllText(args[1], Encoding.UTF8));
                o.WriteLine("fixture：{0} 筆訊息，ip 欄位：{1}", fixture.Rows.Count, fixture.HasIpColumn ? "有" : "沒有");
                var days = ChatGrouping.Group(fixture.Rows);
                Print(o, days);

                o.WriteLine();
                o.WriteLine("== 搜尋「報價」==");
                Print(o, ChatGrouping.Group(ChatGrouping.Filter(fixture.Rows, "報價")));

                o.WriteLine();
                o.WriteLine("== 錯誤回應的判讀 ==");
                failures += Expect(o, "沒有 ip 欄位（HTTP 400、7500）", 400,
                    "{\"result\":[],\"success\":false,\"errors\":[{\"code\":7500,\"message\":\"no such column: ip at offset 77: SQLITE_ERROR\"}],\"messages\":[]}",
                    D1ErrorKind.MissingIpColumn);
                failures += Expect(o, "token 錯（HTTP 401、10000）", 401,
                    "{\"success\":false,\"errors\":[{\"code\":10000,\"message\":\"Authentication error\"}],\"messages\":[],\"result\":null}",
                    D1ErrorKind.Auth);
                failures += Expect(o, "Authorization 格式錯（HTTP 400、6111）", 400,
                    "{\"success\":false,\"errors\":[{\"code\":6003,\"message\":\"Invalid request headers\",\"error_chain\":[{\"code\":6111,\"message\":\"Invalid format for Authorization header\"}]}],\"messages\":[],\"result\":null}",
                    D1ErrorKind.Auth);
                failures += Expect(o, "ID 錯（HTTP 404、7003）", 404,
                    "{\"success\":false,\"errors\":[{\"code\":7003,\"message\":\"Could not route to /client/v4/accounts/x/d1/database/y/query, perhaps your object identifier is invalid?\"}],\"messages\":[],\"result\":null}",
                    D1ErrorKind.NotFound);
                failures += Expect(o, "不是 JSON（HTTP 502，例如被攔截的 HTML 頁）", 502, "<html>Bad gateway</html>", D1ErrorKind.BadResponse);

                var noIp = D1Client.FromFixture("{\"success\":true,\"errors\":[],\"messages\":[],\"result\":[{\"results\":[{\"id\":1,\"conversation_id\":\"c-old\",\"created_at\":\"2026-10-01T02:00:00.000Z\",\"role\":\"user\",\"content\":\"舊資料\",\"country\":\"TW\",\"city\":\"Taipei\",\"usage\":null}],\"success\":true,\"meta\":{}}]}");
                var ok = !noIp.HasIpColumn && ChatGrouping.Group(noIp.Rows)[0].Customers[0].IpLabel == Customer.NoIpLabel;
                o.WriteLine("{0} 沒有 ip 欄的查詢結果 → 歸到「{1}」", ok ? "🟢" : "🔴", Customer.NoIpLabel);
                if (!ok) failures++;

                if (args.Length >= 3)
                {
                    o.WriteLine();
                    o.WriteLine("== 匯出 ==");
                    var res = Exporter.Export(fixture.Rows, args[2], "selftest fixture", TwTime.Now);
                    o.WriteLine("CSV：{0}（{1:N0} bytes，開頭 BOM：{2}）", res.CsvPath, new FileInfo(res.CsvPath).Length, HasBom(res.CsvPath) ? "有" : "沒有");
                    o.WriteLine("MD ：{0}（{1:N0} bytes）", res.MdPath, new FileInfo(res.MdPath).Length);
                }
            }
            catch (Exception ex)
            {
                o.WriteLine("🔴 selftest 例外：" + ex);
                return 1;
            }
            o.WriteLine();
            o.WriteLine(failures == 0 ? "🟢 selftest 全部通過" : "🔴 selftest 有 " + failures + " 項不符");
            return failures == 0 ? 0 : 1;
        }

        static void Print(TextWriter o, List<Day> days)
        {
            foreach (var d in days)
            {
                o.WriteLine("{0:yyyy-MM-dd}（{1}）  客戶 {2}・提問 {3}", d.Date, TwTime.Week(d.Date), d.Customers.Count, d.QuestionCount);
                foreach (var c in d.Customers)
                {
                    o.WriteLine("  {0,-26} {1,-18} 對話 {2}・提問 {3}・最後 {4:HH:mm:ss}", c.IpLabel, c.Region, c.Conversations.Count, c.QuestionCount, c.LastQuestionTw);
                    foreach (var conv in c.Conversations)
                    {
                        o.WriteLine("    對話 {0}（{1:HH:mm} 開始，{2} 則）", conv.Id, conv.StartTw, conv.Messages.Count);
                        foreach (var m in conv.Messages)
                        {
                            var text = (m.Content ?? "").Replace("\r", "").Replace("\n", " ⏎ ");
                            if (text.Length > 40) text = text.Substring(0, 40) + "…";
                            o.WriteLine("      #{0,-3} {1:HH:mm:ss} {2}：{3}", m.Id, m.Tw, m.RoleLabel, text);
                        }
                    }
                }
            }
        }

        static int Expect(TextWriter o, string name, int status, string json, D1ErrorKind kind)
        {
            try
            {
                D1Client.ParseResponse(json, status);
                o.WriteLine("🔴 {0}：沒有丟出錯誤", name);
                return 1;
            }
            catch (D1Exception ex)
            {
                var ok = ex.Kind == kind;
                o.WriteLine("{0} {1} → {2}：{3}", ok ? "🟢" : "🔴", name, ex.Kind, ex.Message.Replace("\r\n", " "));
                return ok ? 0 : 1;
            }
        }

        static bool HasBom(string path)
        {
            var b = new byte[3];
            using (var f = File.OpenRead(path)) f.Read(b, 0, 3);
            return b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF;
        }
    }
}
