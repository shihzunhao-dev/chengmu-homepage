using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace ChengMuConsole
{
    /// <summary>
    /// 設定存在 %APPDATA%\ChengMu-Console\settings.json。
    /// API Token 用 DPAPI（CurrentUser）加密後才寫檔：換一台電腦或換一個 Windows 帳號都解不開，要重新輸入。
    /// ⚠ Token 明文只留在記憶體，不寫記錄、不上畫面（設定視窗的欄位是遮罩的，而且不會把已存的 token 填回去）。
    /// </summary>
    public sealed class AppSettings
    {
        public const string DefaultAccountId = "a14d46443ec54dffd4646de3839e2a95";
        public const string DefaultDatabaseId = "33bab8c1-cd58-48a4-94c6-4754e13fe36f";
        public const string DefaultExportDir = @"D:\澄沐公司\官網客服紀錄";
        public static readonly int[] DayChoices = { 7, 30, 90, 365, 0 };   // 0＝全部

        static readonly byte[] Entropy = Encoding.UTF8.GetBytes("ChengMu-Console/D1-token/v1");

        public string AccountId = DefaultAccountId;
        public string DatabaseId = DefaultDatabaseId;
        public string Token;        // 明文，只在記憶體
        public string ExportDir = DefaultExportDir;
        public int Days = 30;

        public bool HasToken { get { return !string.IsNullOrEmpty(Token); } }

        public static string Folder
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ChengMu-Console"); }
        }

        public static string FilePath { get { return Path.Combine(Folder, "settings.json"); } }

        public static bool IsValidAccountId(string s) { return s != null && Regex.IsMatch(s, "^[0-9a-fA-F]{32}$"); }
        public static bool IsValidDatabaseId(string s) { return s != null && Regex.IsMatch(s, "^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$"); }

        public AppSettings Clone()
        {
            return (AppSettings)MemberwiseClone();
        }

        /// <summary>讀設定；沒有檔案或檔案壞掉就用預設值（token 空白，啟動時會跳設定視窗）。</summary>
        public static AppSettings Load(out string warning)
        {
            warning = null;
            var s = new AppSettings();
            if (!File.Exists(FilePath)) return s;
            try
            {
                var d = new JavaScriptSerializer().DeserializeObject(File.ReadAllText(FilePath, Encoding.UTF8)) as Dictionary<string, object>;
                if (d == null) return s;
                s.AccountId = Get(d, "accountId") ?? s.AccountId;
                s.DatabaseId = Get(d, "databaseId") ?? s.DatabaseId;
                s.ExportDir = Get(d, "exportDir") ?? s.ExportDir;
                int days;
                if (int.TryParse(Get(d, "days"), out days) && Array.IndexOf(DayChoices, days) >= 0) s.Days = days;
                var enc = Get(d, "tokenDpapi");
                if (!string.IsNullOrEmpty(enc))
                {
                    try
                    {
                        s.Token = Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(enc), Entropy, DataProtectionScope.CurrentUser));
                    }
                    catch (CryptographicException) { warning = "已儲存的 API Token 在這台電腦（或這個 Windows 帳號）解不開，請重新輸入。"; }
                    catch (FormatException) { warning = "已儲存的 API Token 讀不出來，請重新輸入。"; }
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is InvalidOperationException)
            {
                warning = "設定檔讀不出來，已改用預設值：" + ex.Message;
            }
            return s;
        }

        public void Save()
        {
            Directory.CreateDirectory(Folder);
            var d = new Dictionary<string, object>
            {
                { "accountId", AccountId },
                { "databaseId", DatabaseId },
                { "exportDir", ExportDir },
                { "days", Days },
                { "tokenDpapi", HasToken ? Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(Token), Entropy, DataProtectionScope.CurrentUser)) : "" },
            };
            var tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, new JavaScriptSerializer().Serialize(d), new UTF8Encoding(false));
            if (File.Exists(FilePath)) File.Replace(tmp, FilePath, null);
            else File.Move(tmp, FilePath);
        }

        static string Get(Dictionary<string, object> d, string key)
        {
            object v;
            return d.TryGetValue(key, out v) && v != null ? Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture) : null;
        }
    }
}
