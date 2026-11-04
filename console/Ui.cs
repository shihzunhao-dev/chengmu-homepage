using System;
using System.Drawing;
using System.Windows.Forms;

namespace ChengMuConsole
{
    // ★ 這五個 Ui*.cs 從 D:\SE_Dev\DeployTool 複製（那邊又是從 CCIMS 安裝包複製），改了 namespace、拿掉用不到的元件，
    //   讓澄沐中控台跟 SE-Deploy.exe／CCIMS 中控台長得一樣。家族那邊換了外觀，這裡要跟著換。
    //   字級比家族大一號：業主年紀較長，內文至少 11pt、標籤至少 10pt。
    // ⚠ 不要呼叫 Application.SetCompatibleTextRenderingDefault(false)：家族的標籤是用 GDI+ 畫字的，兩邊字寬才一樣。
    // ⚠ manifest 也不宣告 DPI 感知：高 DPI 時由 Windows 整個放大，跟家族一樣。
    public static class Ui
    {
        public const string FontName = "微軟正黑體";

        public static Font Font(float size, bool bold = false)
        {
            return new Font(FontName, size, bold ? FontStyle.Bold : FontStyle.Regular);
        }

        // 數字、IP、時間用 Segoe UI（微軟正黑體的「1」底下有橫腳，IP 的點會跟兩邊黏在一起）
        public static Font Latin(float size, bool bold = false)
        {
            return new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular);
        }

        public static readonly Color Green = Color.FromArgb(15, 138, 82);
        public static readonly Color Amber = Color.FromArgb(180, 110, 0);
        public static readonly Color Red = Color.FromArgb(198, 40, 40);

        // 做出一段說明文字：先量高度再定位，中文換行才不會被切掉
        public static Label Text(string text, int y, int width = 800, bool bold = false, float size = 11, Color? color = null)
        {
            var l = new Label
            {
                Text = text,
                AutoSize = false,
                Width = width,
                Height = 10,
                Location = new Point(0, y),
                Font = Font(size, bold),
            };
            if (color.HasValue) l.ForeColor = color.Value;
            var sz = TextRenderer.MeasureText(text, l.Font, new Size(width, 0), TextFormatFlags.WordBreak);
            l.Height = sz.Height + 6;
            return l;
        }

        public static void Info(string text, string title, MessageBoxIcon icon = MessageBoxIcon.Warning)
        {
            MessageBox.Show(text, title, MessageBoxButtons.OK, icon);
        }

        // 程式最外層：任何沒接到的錯誤都跳中文訊息框，不要讓使用者看到 .NET 的英文例外對話框
        public static void InstallCrashHandlers(string title)
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) => ShowCrash(title, e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => ShowCrash(title, e.ExceptionObject as Exception);
        }

        public static void ShowCrash(string title, Exception ex)
        {
            var msg = ex == null ? "（沒有錯誤訊息）" : ex.Message;
            MessageBox.Show(
                string.Format("{0}遇到預期外的狀況，這一步沒有完成。\r\n\r\n訊息：{1}\r\n\r\n請把這個畫面截圖給維護人員。", title, msg),
                title, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
