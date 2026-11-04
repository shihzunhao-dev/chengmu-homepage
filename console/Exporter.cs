using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace ChengMuConsole
{
    /// <summary>
    /// 匯出目前讀進來的範圍：CSV（UTF-8 BOM，Excel 直接開不亂碼）＋ Markdown（日期 → IP → 對話）。
    /// 檔名格式跟 worker-chat\export-chats.ps1 一樣（chat-log-yyyyMMdd-HHmm），兩邊匯出的檔案放同一個資料夾也排得整齊。
    /// </summary>
    public static class Exporter
    {
        public sealed class Result
        {
            public string CsvPath, MdPath;
            public int Rows;
        }

        public static Result Export(List<ChatRow> rows, string dir, string rangeText, DateTime nowTw)
        {
            Directory.CreateDirectory(dir);
            var stamp = nowTw.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture);
            var res = new Result
            {
                CsvPath = Path.Combine(dir, "chat-log-" + stamp + ".csv"),
                MdPath = Path.Combine(dir, "chat-log-" + stamp + ".md"),
                Rows = rows.Count,
            };
            var days = ChatGrouping.Group(rows);
            File.WriteAllText(res.CsvPath, Csv(days), new UTF8Encoding(true));
            File.WriteAllText(res.MdPath, Markdown(days, rangeText, nowTw), new UTF8Encoding(true));
            return res;
        }

        public static string Csv(List<Day> days)
        {
            var sb = new StringBuilder();
            Line(sb, "日期", "時間", "IP", "地區", "對話編號", "角色", "內容", "用量", "編號");
            // CSV 照時間順序（舊到新）一列一則，方便在 Excel 裡篩選排序
            foreach (var day in days.OrderBy(d => d.Date))
                foreach (var m in day.Customers.SelectMany(c => c.Messages.Select(x => new { c, x })).OrderBy(p => p.x.Id))
                {
                    var r = m.x;
                    Line(sb, r.Tw.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), r.Tw.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
                        m.c.IpLabel, Regions.Name(r.Country, r.City), r.ConversationId, r.RoleLabel, r.Content, r.Usage ?? "",
                        r.Id.ToString(CultureInfo.InvariantCulture));
                }
            return sb.ToString();
        }

        static void Line(StringBuilder sb, params string[] cells)
        {
            sb.Append(string.Join(",", cells.Select(Cell))).Append("\r\n");
        }

        // RFC 4180：有逗號、引號、換行就整格加引號、引號重複。
        // 客人打的字會直接進 Excel：開頭是 = + - @ 的前面加 '，免得被當成公式（CSV injection）
        static string Cell(string v)
        {
            v = v ?? "";
            if (v.Length > 0 && "=+-@\t\r".IndexOf(v[0]) >= 0) v = "'" + v;
            if (v.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0) v = "\"" + v.Replace("\"", "\"\"") + "\"";
            return v;
        }

        public static string Markdown(List<Day> days, string rangeText, DateTime nowTw)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# 官網服務助理對話紀錄");
            sb.AppendLine();
            sb.AppendFormat("- 範圍：{0}\r\n", rangeText);
            sb.AppendFormat("- 匯出時間：{0:yyyy-MM-dd HH:mm}（台灣時間）\r\n", nowTw);
            sb.AppendFormat("- 共 {0} 天、{1} 位客戶（依 IP）、{2} 則提問\r\n", days.Count, days.Sum(d => d.Customers.Count), days.Sum(d => d.QuestionCount));
            sb.AppendLine();
            foreach (var day in days)
            {
                sb.AppendFormat("## {0:yyyy-MM-dd}（{1}）— {2} 位客戶、{3} 則提問\r\n\r\n", day.Date, TwTime.Week(day.Date), day.Customers.Count, day.QuestionCount);
                foreach (var c in day.Customers)
                {
                    sb.AppendFormat("### {0}（{1}）— {2} 段對話、{3} 則提問\r\n\r\n", c.IpLabel, c.Region, c.Conversations.Count, c.QuestionCount);
                    var n = 0;
                    foreach (var conv in c.Conversations)
                    {
                        n++;
                        sb.AppendFormat("#### 對話 {0}・{1:HH:mm} 開始（編號 {2}）\r\n\r\n", n, conv.StartTw, conv.Id);
                        foreach (var m in conv.Messages)
                        {
                            sb.AppendFormat("**{0}**　{1:HH:mm:ss}\r\n\r\n", m.RoleLabel, m.Tw);
                            foreach (var line in (m.Content ?? "").Replace("\r\n", "\n").Split('\n'))
                                sb.Append("> ").Append(line).Append("\r\n");
                            sb.AppendLine();
                        }
                    }
                }
            }
            return sb.ToString();
        }
    }
}
