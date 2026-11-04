using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace ChengMuConsole
{
    public enum D1ErrorKind { Auth, NotFound, MissingIpColumn, Network, Timeout, Api, BadResponse }

    /// <summary>D1 讀取失敗。Message 是給業主看的中文說明；Detail 是 Cloudflare 回的原文（不含 token）。</summary>
    public sealed class D1Exception : Exception
    {
        public D1ErrorKind Kind { get; private set; }
        public string Detail { get; private set; }

        public D1Exception(D1ErrorKind kind, string message, string detail = null, Exception inner = null) : base(message, inner)
        {
            Kind = kind;
            Detail = detail;
        }

        public string FullText { get { return string.IsNullOrEmpty(Detail) ? Message : Message + "\r\n\r\n（Cloudflare 回應：" + Detail + "）"; } }
    }

    public sealed class LoadResult
    {
        public List<ChatRow> Rows = new List<ChatRow>();
        /// <summary>false＝資料庫還沒有 ip 欄位（migration 0002 還沒跑），IP 一律顯示「未記錄」。</summary>
        public bool HasIpColumn = true;
    }

    // =============================================================================
    //  Cloudflare D1 REST API（2026-11-04 對照官方文件）
    //  https://developers.cloudflare.com/api/resources/d1/subresources/database/methods/query/
    //    POST https://api.cloudflare.com/client/v4/accounts/{account_id}/d1/database/{database_id}/query
    //    Authorization: Bearer <API Token>（權限 D1 Read 或 D1 Write）
    //    body：{"sql": "...", "params": ["...", ...]}（params 是字串陣列）
    //    回應：{"success":bool, "errors":[{"code":n,"message":"..."}], "messages":[...],
    //           "result":[{"results":[{欄位:值,...}], "success":bool, "meta":{...}}]}
    //  ⚠ token 只放在 Authorization 標頭，不寫進任何訊息、記錄或例外。
    // =============================================================================
    public static class D1Client
    {
        const int PageSize = 2000;
        static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        const string ColsWithIp = "id, conversation_id, created_at, role, content, country, city, usage, ip";
        const string ColsNoIp = "id, conversation_id, created_at, role, content, country, city, usage";

        static string Url(AppSettings s)
        {
            return "https://api.cloudflare.com/client/v4/accounts/" + s.AccountId + "/d1/database/" + s.DatabaseId + "/query";
        }

        /// <summary>讀 sinceUtc 之後的全部紀錄（null＝全部）。分頁讀（每頁 2000 筆），回應才不會太大。</summary>
        public static async Task<LoadResult> LoadAsync(AppSettings s, DateTime? sinceUtc, CancellationToken ct)
        {
            var since = sinceUtc.HasValue ? TwTime.ToIso(sinceUtc.Value) : "";
            var result = new LoadResult();
            try
            {
                await LoadPages(s, since, ColsWithIp, result.Rows, ct);
            }
            catch (D1Exception ex) when (ex.Kind == D1ErrorKind.MissingIpColumn)
            {
                // 還沒跑 migration 0002（ALTER TABLE ... ADD COLUMN ip）：改用沒有 ip 的查詢，IP 一律「未記錄」
                result.HasIpColumn = false;
                result.Rows.Clear();
                await LoadPages(s, since, ColsNoIp, result.Rows, ct);
            }
            return result;
        }

        static async Task LoadPages(AppSettings s, string since, string cols, List<ChatRow> into, CancellationToken ct)
        {
            long lastId = 0;
            while (true)
            {
                var sql = "SELECT " + cols + " FROM chat_messages WHERE created_at >= ? AND id > CAST(? AS INTEGER) ORDER BY id LIMIT " + PageSize;
                var rows = await QueryAsync(s, sql, new[] { since, lastId.ToString() }, ct);
                foreach (var r in rows) into.Add(ToRow(r));
                if (rows.Count < PageSize) return;
                lastId = into[into.Count - 1].Id;
            }
        }

        /// <summary>設定視窗的「測試連線」：數一下總筆數（同時確認 token、ID、表都對）。</summary>
        public static async Task<long> CountAsync(AppSettings s, CancellationToken ct)
        {
            var rows = await QueryAsync(s, "SELECT COUNT(*) AS n FROM chat_messages", new string[0], ct);
            if (rows.Count == 0 || !rows[0].ContainsKey("n")) throw new D1Exception(D1ErrorKind.BadResponse, "Cloudflare 的回應格式跟預期不同，讀不到筆數。");
            return Convert.ToInt64(rows[0]["n"]);
        }

        public static async Task<List<Dictionary<string, object>>> QueryAsync(AppSettings s, string sql, string[] args, CancellationToken ct)
        {
            if (!AppSettings.IsValidAccountId(s.AccountId) || !AppSettings.IsValidDatabaseId(s.DatabaseId))
                throw new D1Exception(D1ErrorKind.NotFound, "Account ID 或 Database ID 格式不對，請按「設定」檢查。");
            if (string.IsNullOrEmpty(s.Token))
                throw new D1Exception(D1ErrorKind.Auth, "還沒有設定 API Token，請按「設定」輸入。");

            var body = NewSerializer().Serialize(new Dictionary<string, object> { { "sql", sql }, { "params", args } });
            int status;
            string text;
            try
            {
                using (var req = new HttpRequestMessage(HttpMethod.Post, Url(s)))
                {
                    req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", s.Token);
                    req.Content = new StringContent(body, Encoding.UTF8, "application/json");
                    using (var resp = await Http.SendAsync(req, ct))
                    {
                        status = (int)resp.StatusCode;
                        // 先讀內容再看狀態碼：錯誤原因在 errors[] 裡
                        text = await resp.Content.ReadAsStringAsync();
                    }
                }
            }
            catch (TaskCanceledException ex)
            {
                if (ct.IsCancellationRequested) throw;
                throw new D1Exception(D1ErrorKind.Timeout, "連線逾時（30 秒沒有回應）。請檢查網路連線後按「重新整理」再試一次。", null, ex);
            }
            catch (HttpRequestException ex)
            {
                throw new D1Exception(D1ErrorKind.Network, "連不上 Cloudflare。請檢查網路連線（或公司防火牆）後按「重新整理」再試一次。", Inner(ex), ex);
            }
            return ParseResponse(text, status);
        }

        static string Inner(Exception ex)
        {
            var e = ex;
            while (e.InnerException != null) e = e.InnerException;
            return e.Message;
        }

        static JavaScriptSerializer NewSerializer()
        {
            // 預設上限 2 MB，對話多的時候一頁就超過
            return new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 64 };
        }

        // 這幾個錯誤碼是 token 的問題（Cloudflare 對格式不對的 Authorization 標頭回 400，不一定是 401/403）
        static readonly int[] AuthCodes = { 10000, 10001, 9106, 9109, 6003, 6100, 6101, 6102, 6103, 6111 };

        /// <summary>解析 /query 的回應：成功就回傳所有 result[].results[] 的列；失敗丟 D1Exception（中文說明）。</summary>
        public static List<Dictionary<string, object>> ParseResponse(string json, int httpStatus)
        {
            Dictionary<string, object> root = null;
            try { root = NewSerializer().DeserializeObject(json ?? "") as Dictionary<string, object>; }
            catch (ArgumentException) { }
            catch (InvalidOperationException) { }

            if (root == null)
            {
                if (httpStatus == 401 || httpStatus == 403) throw AuthError(null);
                throw new D1Exception(D1ErrorKind.BadResponse,
                    string.Format("Cloudflare 的回應看不懂（HTTP {0}）。可能是網路被攔截，或 Cloudflare 暫時有狀況，請稍後再試。", httpStatus));
            }

            var errors = List(root, "errors").OfType<Dictionary<string, object>>()
                .Select(e => new KeyValuePair<int, string>(ToInt(e, "code"), Str(e, "message") ?? "")).ToList();
            var success = root.ContainsKey("success") && root["success"] is bool && (bool)root["success"];
            var results = List(root, "result").OfType<Dictionary<string, object>>().ToList();
            // 單一查詢失敗時有時 errors 是空的、錯誤寫在 result[].error
            foreach (var r in results)
            {
                var e = Str(r, "error");
                if (!string.IsNullOrEmpty(e)) errors.Add(new KeyValuePair<int, string>(0, e));
                if (r.ContainsKey("success") && r["success"] is bool && !(bool)r["success"]) success = false;
            }

            if (success && httpStatus < 400 && errors.Count == 0)
            {
                var rows = new List<Dictionary<string, object>>();
                foreach (var r in results)
                    rows.AddRange(List(r, "results").OfType<Dictionary<string, object>>());
                return rows;
            }

            var detail = errors.Count == 0 ? "HTTP " + httpStatus
                : string.Join("；", errors.Select(e => (e.Key > 0 ? "[" + e.Key + "] " : "") + e.Value));

            if (errors.Any(e => e.Value.IndexOf("no such column", StringComparison.OrdinalIgnoreCase) >= 0 && Regex.IsMatch(e.Value, @"\bip\b", RegexOptions.IgnoreCase)))
                throw new D1Exception(D1ErrorKind.MissingIpColumn, "資料庫還沒有 ip 欄位（migration 0002 還沒執行）。", detail);
            if (httpStatus == 401 || httpStatus == 403 || errors.Any(e => AuthCodes.Contains(e.Key)))
                throw AuthError(detail);
            if (httpStatus == 404 || errors.Any(e => e.Key == 7003 || e.Key == 7404 || e.Value.IndexOf("not found", StringComparison.OrdinalIgnoreCase) >= 0))
                throw new D1Exception(D1ErrorKind.NotFound, "找不到這個資料庫。請按「設定」確認 Account ID 與 Database ID。", detail);
            if (httpStatus == 429)
                throw new D1Exception(D1ErrorKind.Api, "Cloudflare 回應「查詢太頻繁」，請過一分鐘再按「重新整理」。", detail);
            throw new D1Exception(D1ErrorKind.Api, string.Format("Cloudflare 回應錯誤（HTTP {0}），這次沒有讀到資料。", httpStatus), detail);
        }

        static D1Exception AuthError(string detail)
        {
            return new D1Exception(D1ErrorKind.Auth,
                "API Token 不正確、已過期，或沒有 D1 讀取權限。\r\n請按「設定」重新輸入 Token（建立時權限要選「帳戶 → D1 → 讀取」）。", detail);
        }

        public static ChatRow ToRow(Dictionary<string, object> r)
        {
            var created = Str(r, "created_at");
            DateTime utc;
            try { utc = TwTime.ParseUtc(created); }
            catch (FormatException) { utc = DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc); }
            catch (ArgumentNullException) { utc = DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc); }
            return new ChatRow
            {
                Id = r.ContainsKey("id") && r["id"] != null ? Convert.ToInt64(r["id"]) : 0,
                ConversationId = Str(r, "conversation_id") ?? "",
                CreatedUtc = utc,
                Role = Str(r, "role") ?? "error",
                Content = Str(r, "content") ?? "",
                Country = Str(r, "country"),
                City = Str(r, "city"),
                Usage = Str(r, "usage"),
                Ip = Str(r, "ip"),
            };
        }

        static string Str(Dictionary<string, object> d, string key)
        {
            object v;
            if (!d.TryGetValue(key, out v) || v == null) return null;
            return Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture);
        }

        static int ToInt(Dictionary<string, object> d, string key)
        {
            object v;
            if (!d.TryGetValue(key, out v) || v == null) return 0;
            try { return Convert.ToInt32(v, System.Globalization.CultureInfo.InvariantCulture); }
            catch (FormatException) { return 0; }
            catch (OverflowException) { return 0; }
        }

        static IEnumerable<object> List(Dictionary<string, object> d, string key)
        {
            object v;
            if (!d.TryGetValue(key, out v) || v == null) return new object[0];
            var arr = v as IEnumerable;
            return arr == null || v is string ? new object[0] : arr.Cast<object>();
        }

        /// <summary>從檔案讀 fixture（--selftest／--screenshot 用），走跟正式查詢同一條解析路徑。</summary>
        public static LoadResult FromFixture(string json)
        {
            var rows = ParseResponse(json, 200);
            var result = new LoadResult { HasIpColumn = rows.Any(r => r.ContainsKey("ip")) };
            result.Rows.AddRange(rows.Select(ToRow));
            return result;
        }

        public static void UseTls12()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        }
    }
}
