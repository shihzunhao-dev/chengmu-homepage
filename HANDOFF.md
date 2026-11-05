# 澄沐官網 交接文件

最後更新：2026-11-05（清理舊檔後）

正式網址：https://chengmu.com.tw/ ・ 專案 wiki：`chengmu-homepage`（ProjectWiki）

---

## 1. 現況一覽

| 項目 | 狀態 |
|---|---|
| 首頁 `index.html` | 極簡單頁：公司 logo + 「澄沐實業」+ 三項服務；輸入框固定在頁尾分隔線正上方；頁尾 Email / LINE（含 QR code）/ 電話；右上角時鐘（時:分:秒，手機不顯示） |
| 隨時間變色 | 依訪客電腦時間，24 小時各一組底色、整點換色（1.5 秒淡入）；白天 06–17 淺色、晚上 18–05 深色；沒有手動切換按鈕。24 色預覽：本機 `demo-daylight.html`（不部署、不進 git） |
| 特效 | 工程方格紙底、CAD 十字游標、滑鼠經過格子亮起 logo 64 色階、點擊格子重力掉落（`prefers-reduced-motion` 時關閉） |
| 字體 | 英數：自架更紗英數裁字檔 `assets/fonts/SarasaTermTC-Latin-*.woff2`；中文：Google Fonts Noto Sans TC |
| AI 服務助理 | Worker `chengmu-chat`，路由 `chengmu.com.tw/api/*`，模型 `claude-haiku-5-5`（thinking disabled、effort high） |
| 對話紀錄 | D1 `chengmu-chat-log`：`chat_messages`（每則問答＋IP）、`leads`（轉交工程師紀錄） |
| 轉交工程師 | 客人有合作意願並留下聯絡方式時，AI 呼叫 `notify_engineer` 寄信到 chengmu00082998@gmail.com；同一段對話最多 3 封 |
| 中控台 | `D:\chengmu_deploy\ChengMu-Console.exe`（原始碼 `console/`），依日期／客戶 IP 看對話、搜尋、匯出 |
| 網域與郵件 DNS | chengmu.com.tw 綁 CF Pages；www 301 轉 apex；Email Routing 已啟用；SPF、DKIM、DMARC（`p=none`）都已設定 |

---

## 2. 檔案地圖

```
index.html                 首頁（CSS/JS 都在檔內）
404.html                   找不到頁面
robots.txt / sitemap.xml   sitemap 只有首頁
assets/                    logo.svg、logo.png、line-qr.svg、fonts/
deploy.ps1                 網站部署（git push + 白名單複製到 dist/ 後 wrangler pages deploy）
worker-chat/               AI 服務助理 Worker
  src/index.js             RULES（回答規則）、notify_engineer、D1 寫入、限流、驗證
  knowledge.md             AI 唯一知識來源（只寫服務類型，不寫客戶資訊、不寫 AI）
  wrangler.toml            路由、D1 綁定 DB、寄信綁定 EMAIL、限流 RATE_LIMITER
  schema.sql、migrations/  資料表結構（0002 加 ip、0003 leads、0004 notify_count）
  export-chats.ps1         備用：用 wrangler 匯出對話成 CSV / Markdown
console/                   ChengMu-Console（WinForms net472，SE-Deploy 家族樣式）
```

2026-11-05 已清掉的舊檔（要找回用 git 歷史）：`assets/site.css`、`assets/fx.js`（舊版樣式與特效）、
`worker/contact.js`、`worker/DEPLOY.md`（舊聯絡表單 Worker）、`products/*.html`（舊產品頁）。

---

## 3. 部署（兩個部署要分開做）

### 網站（index.html、assets 等）
```powershell
cd D:\chengmu_homepage
.\deploy.ps1 "commit message"   # git push + CF Pages
.\deploy.ps1 -SkipGit           # 只推 CF Pages
```
- 只會上傳 `deploy.ps1` 裡 `$SiteFiles` 白名單的檔案；**新增網站檔案要加進白名單**。
- Claude Code 可以代跑（wrangler login 有效時）。`git push` 第一次常失敗、重試就好。

### AI 服務助理（worker-chat/ 底下任何檔案，含 knowledge.md）
```powershell
cd D:\chengmu_homepage\worker-chat
wrangler deploy
```
- **在 Claude Code 內跑會被自動權限審查擋下**（動正式路由），請使用者在自己的 PowerShell 跑。
- 本機先驗證打包：`wrangler deploy --dry-run --outdir <暫存資料夾>`。
- 第一次或換電腦：`npm install`；換金鑰：`wrangler secret put ANTHROPIC_API_KEY`。

### D1 資料表變更
- 用 `wrangler d1 execute chengmu-chat-log --remote --yes --command "..."`；`--file` 走 `/import` 曾回 auth 10000。
- 結構改了要同步：`schema.sql`、`migrations/`、`src/index.js`（saveTurn / notifyEngineer）、`console/D1Client.cs`、`export-chats.ps1`。

### 中控台
```powershell
cd D:\chengmu_homepage\console
dotnet build -c Release
# 再把 bin\Release\net472\ChengMu-Console.exe 複製到 D:\chengmu_deploy\
```
- 讀資料用使用者自己的 Cloudflare API Token（名稱 ChengMu-Console，權限只有 D1:Read），DPAPI 加密存在 `%APPDATA%\ChengMu-Console\settings.json`。

---

## 4. 帳號與花費（不含任何密碼或金鑰）

| 服務 | 用途 |
|---|---|
| Cloudflare（chengmu00082998@gmail.com） | Pages、Workers、D1、DNS、Email Routing、Web Analytics |
| Anthropic Console | 官網 AI 專用工作區，**每月花費上限 50 USD**（硬上限）；Worker 另有每 IP 每分鐘 10 次的軟性限流（近似值） |
| GitHub `shihzunhao-dev/chengmu-homepage` | 原始碼、GitHub Pages 備援站（沒有 /api） |
| Google Search Console | 網域資源 chengmu.com.tw，DNS TXT 驗證，**不要刪 google-site-verification** |

---

## 5. 使用者確定過的設計決定

- 版面維持「logo 置中、輸入框貼在分隔線上方」；**不要說明文字、不要建議問題**，越簡單越好。
- 右下角浮動對話按鈕已移除，對話只有首頁輸入框一個入口。
- 深淺色切換按鈕已拿掉（2026-11-05），改由時間自動決定；手機不顯示時鐘（手機本身有時鐘）。
- 字級要老花友善；手機版 logo 與公司名並排、頁尾標籤與內容同一行。
- AI 只說明三項服務；服務對象寫「各行各業」；**知識文件與回答都不提 AI**（定位是工程師負責的專業資訊服務），只有客人直接問「你是不是 AI」才承認。
- 不提供客戶名稱、人名、報價金額、天數。
- 稱呼客人一律用「您」。
- 3 個產品頁與試玩頁已刪除（回 404，不做轉址）。

---

## 6. 待處理

| 項目 | 說明 |
|---|---|
| 舊檔快取殘留 | chengmu.com.tw 曾仍回已刪除的舊產品頁、README.md 等（pages.dev 與預覽網址正確 404，zone 清快取無效）。待過期後再查；還在就加 Worker 路由強制 404 |
| 隱私告知 | 輸入框下的「對話與 IP 會保存」已依使用者要求拿掉；個資法上建議之後在版權列放「隱私說明」連結 |
| Resend API key | 舊聯絡表單用的，Worker 已刪（2026-11-05），可到 Resend 後台撤銷 |
| 自有產品 | 是否讓 AI 介紹 PCC Tender Watch、家庭存摺、預算系統，使用者尚未決定（目前不介紹） |

D1 測試紀錄已於 2026-11-05 清除；保留的兩段（`3f0ecab6-…`、`25b7fb8e-…`）無法確定是測試還是真實訪客，未刪。
