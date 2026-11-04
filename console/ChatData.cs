using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ChengMuConsole
{
    /// <summary>chat_messages 的一筆（表結構見 worker-chat\schema.sql）。</summary>
    public sealed class ChatRow
    {
        public long Id;
        public string ConversationId;
        public DateTime CreatedUtc;
        public string Role;      // user / assistant / error
        public string Content;
        public string Country;
        public string City;
        public string Usage;
        public string Ip;        // null＝還沒加 ip 欄位以前寫入的，或資料庫還沒跑 migration 0002

        public DateTime Tw { get { return TwTime.FromUtc(CreatedUtc); } }
        public bool IsQuestion { get { return Role == "user"; } }

        public string RoleLabel
        {
            get { return Role == "user" ? "客人" : Role == "assistant" ? "助理" : "錯誤"; }
        }
    }

    /// <summary>台灣時間＝UTC+8，沒有日光節約，直接加 8 小時（不靠電腦的時區設定）。</summary>
    public static class TwTime
    {
        static readonly TimeSpan Offset = TimeSpan.FromHours(8);
        static readonly string[] WeekNames = { "日", "一", "二", "三", "四", "五", "六" };

        public static DateTime FromUtc(DateTime utc) { return DateTime.SpecifyKind(utc + Offset, DateTimeKind.Unspecified); }
        public static DateTime ToUtc(DateTime tw) { return DateTime.SpecifyKind(tw - Offset, DateTimeKind.Utc); }
        public static DateTime Now { get { return FromUtc(DateTime.UtcNow); } }

        // Worker 寫的是 toISOString()（2026-11-04T07:12:33.123Z）；沒帶時區的（SQLite 的 datetime()）一律當 UTC
        public static DateTime ParseUtc(string s)
        {
            return DateTime.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
        }

        // 跟 Worker 寫入的格式一樣，created_at 用字串比大小才正確
        public static string ToIso(DateTime utc) { return utc.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture); }

        public static string Week(DateTime d) { return WeekNames[(int)d.DayOfWeek]; }

        // 「11/04（三）」；不是今年的加上年份
        public static string DayLabel(DateTime d)
        {
            var head = d.Year == Now.Year ? d.ToString("MM/dd", CultureInfo.InvariantCulture) : d.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture);
            return head + "（" + Week(d) + "）";
        }
    }

    public sealed class Conversation
    {
        public string Id;
        public readonly List<ChatRow> Messages = new List<ChatRow>();
        public DateTime StartTw { get { return Messages[0].Tw; } }
        public int QuestionCount { get { return Messages.Count(m => m.IsQuestion); } }
    }

    /// <summary>某一天、某一個 IP 的客人：那天他所有的對話。</summary>
    public sealed class Customer
    {
        public const string NoIpLabel = "未記錄 IP";

        public string Ip;   // null＝未記錄
        public readonly List<Conversation> Conversations = new List<Conversation>();

        public string IpLabel { get { return Ip ?? NoIpLabel; } }
        public int QuestionCount { get { return Conversations.Sum(c => c.QuestionCount); } }
        public IEnumerable<ChatRow> Messages { get { return Conversations.SelectMany(c => c.Messages); } }

        // 最後一次提問的時間（台灣時間）；整段都沒有提問（只剩錯誤列）就用最後一筆
        public DateTime LastQuestionTw
        {
            get
            {
                var q = Messages.Where(m => m.IsQuestion).ToList();
                return (q.Count > 0 ? q : Messages.ToList()).Max(m => m.Tw);
            }
        }

        // 地區：同一個 IP 通常只有一個；未記錄 IP 那組會混好幾個地方，最多列兩個
        public string Region
        {
            get
            {
                var all = Messages.Select(m => Regions.Name(m.Country, m.City)).Where(s => s != "").Distinct().ToList();
                if (all.Count == 0) return "地區不明";
                if (all.Count <= 2) return string.Join("、", all);
                return all[0] + "、" + all[1] + " 等 " + all.Count + " 地";
            }
        }
    }

    public sealed class Day
    {
        public DateTime Date;
        public readonly List<Customer> Customers = new List<Customer>();
        public int QuestionCount { get { return Customers.Sum(c => c.QuestionCount); } }
    }

    /// <summary>
    /// 分組規則（畫面與匯出共用同一套）：
    ///   日期＝每一則訊息自己的台灣日期。跨過午夜的對話會拆在兩天，各自只有當天的那幾則。
    ///   客人＝同一天同一個 IP；ip 是 NULL 的全部歸到「未記錄 IP」一組。
    ///   排序：日期新到舊、客人依最後提問新到舊、對話與訊息依時間（id）舊到新。
    /// </summary>
    public static class ChatGrouping
    {
        public static List<Day> Group(IEnumerable<ChatRow> rows)
        {
            var days = new List<Day>();
            foreach (var byDay in rows.GroupBy(r => r.Tw.Date))
            {
                var day = new Day { Date = byDay.Key };
                foreach (var byIp in byDay.GroupBy(r => string.IsNullOrEmpty(r.Ip) ? null : r.Ip))
                {
                    var cust = new Customer { Ip = byIp.Key };
                    foreach (var byConv in byIp.GroupBy(r => r.ConversationId ?? ""))
                    {
                        var conv = new Conversation { Id = byConv.Key };
                        conv.Messages.AddRange(byConv.OrderBy(m => m.Id));
                        cust.Conversations.Add(conv);
                    }
                    cust.Conversations.Sort((a, b) => a.Messages[0].Id.CompareTo(b.Messages[0].Id));
                    day.Customers.Add(cust);
                }
                day.Customers.Sort((a, b) => b.LastQuestionTw.CompareTo(a.LastQuestionTw));
                days.Add(day);
            }
            days.Sort((a, b) => b.Date.CompareTo(a.Date));
            return days;
        }

        /// <summary>
        /// 搜尋：只留「含有關鍵字的對話」（同一天、同一段對話裡任一則內容含關鍵字，整段留下）。
        /// IP、地區、對話編號也算，方便直接貼 IP 找人。不分大小寫。
        /// </summary>
        public static List<ChatRow> Filter(List<ChatRow> rows, string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword)) return rows;
            var k = keyword.Trim();
            var hits = new HashSet<string>();
            foreach (var r in rows)
                if (Contains(r.Content, k) || Contains(r.Ip, k) || Contains(r.ConversationId, k) || Contains(r.City, k)
                    || Contains(Regions.Name(r.Country, r.City), k))
                    hits.Add(ConvKey(r));
            return rows.Where(r => hits.Contains(ConvKey(r))).ToList();
        }

        public static bool Contains(string text, string keyword)
        {
            return !string.IsNullOrEmpty(text) && !string.IsNullOrEmpty(keyword) && text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static string ConvKey(ChatRow r) { return r.Tw.Date.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + "|" + r.ConversationId; }
    }

    /// <summary>Cloudflare 給的國家代碼轉中文；城市名照 Cloudflare 原樣（英文）。</summary>
    public static class Regions
    {
        static readonly Dictionary<string, string> Countries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "TW", "台灣" }, { "US", "美國" }, { "JP", "日本" }, { "CN", "中國" }, { "HK", "香港" }, { "MO", "澳門" },
            { "SG", "新加坡" }, { "MY", "馬來西亞" }, { "KR", "韓國" }, { "VN", "越南" }, { "TH", "泰國" }, { "PH", "菲律賓" },
            { "ID", "印尼" }, { "IN", "印度" }, { "AU", "澳洲" }, { "CA", "加拿大" }, { "GB", "英國" }, { "DE", "德國" },
            { "FR", "法國" }, { "NL", "荷蘭" }, { "XX", "不明" }, { "T1", "Tor" },
        };

        public static string Country(string code)
        {
            if (string.IsNullOrEmpty(code)) return "";
            string name;
            return Countries.TryGetValue(code, out name) ? name : code;
        }

        public static string Name(string country, string city)
        {
            var c = Country(country);
            if (string.IsNullOrEmpty(city)) return c;
            return c == "" ? city : c + " " + city;
        }
    }
}
