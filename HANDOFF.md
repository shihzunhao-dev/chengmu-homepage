# 澄沐官網 交接文件

最後更新：2026-11-08（SEO 強化第一批已上線）

正式網址：https://chengmu.com.tw/ ・ 專案 wiki：`chengmu-homepage`（ProjectWiki）

---

## 0. 進行中：SEO 強化（2026-11-08，從 D:\Temp session 轉來）

來源：seo-specialist 對正式站做的唯讀審查，72/100。主要扣分：可索引文字少、OG 圖是 158×88、沒有安全標頭、JSON-LD 偏薄、版權列沒隱私連結。
已實測確認：舊檔外洩（README.md、deploy.ps1 等）已解決，正式網域都回 404。

### 第一批：已上線（2026-11-08，commit `9d3423a`）
- 分享圖 `assets/og-image.png`（1200×630），`twitter:card` 改 `summary_large_image`，補 og:image 寬高與 alt
- 圖示：`favicon.ico`（根目錄）、`assets/logo-square.png`、`assets/apple-touch-icon.png`；拿掉 SVG 分頁圖示（Google 要方形 favicon），`404.html` 改用 `/favicon.ico`
- JSON-LD：維持 `Organization`（改 ProfessionalService 會因缺 address 報錯），logo 改方形，補 areaServed、sameAs（LINE）、knowsAbout、makesOffer；用字照 `worker-chat/knowledge.md`，不提 AI
- `<noscript>` 公司與服務說明，放在 `.card` 外；`robots.txt` 每組加 `Disallow: /api/`；新增 `_headers`（X-Frame-Options、Permissions-Policy）
- `deploy.ps1` 白名單加 5 個新檔；`sitemap.xml` lastmod 2026-11-08
- 驗證：JSON-LD 可解析；1920/1440/360 三種尺寸與修改前截圖比對，版面不變（只有時鐘與 QR 卡圓角 6 px 反鋸齒差異）

- 上線後已驗證：新檔都回 200、`X-Frame-Options`／`Permissions-Policy` 有送出、robots 19 組都有 `Disallow: /api/`；ProjectWiki 已更新（wiki `854a66f`）
- 還沒做：Facebook Sharing Debugger 按 Scrape Again，讓 FB／LINE 換成新分享圖（使用者自己操作）

### 等使用者決定（不要自行假設）
- **隱私說明頁**（`/privacy`＋版權列小連結，要加進白名單與 sitemap）：Worker 會存對話內容、IP、國家城市、leads（姓名／聯絡方式／公司／需求摘要），程式裡沒有自動刪除。要問：保存期限寫多久？要不要寫「由 AI 服務商處理」（網站定位刻意不提 AI）？先寫草稿給使用者看，改 `index.html` 版權列前要確認
- **title 縮短**：現在約 40 字會被截，提案「澄沐實業｜客製化程式系統開發與維護」
- **JSON-LD**：是否補統一編號、公司登記地址；成立年份 2025 是否正確
- **Cloudflare 後台**（使用者自己操作）：HSTS、關閉 Email Obfuscation（正式站 email 被改寫成 `[email protected]`）、pages.dev 301 到正式網域
- **CSP**：這輪沒加；頁面有行內 script/style，要先盤點來源，用 Report-Only 觀察

---

## 1. 現況一覽

| 項目 | 狀態 |
|---|---|
| 首頁 `index.html` | 極簡單頁：公司 logo + 「澄沐實業」+ 三項服務；輸入框固定在頁尾分隔線正上方；頁尾 Email / LINE（含 QR code）/ 電話；右上角時鐘（時:分:秒，手機版縮小同列） |
| 隨時間變色 | 依訪客電腦時間，24 小時各一組底色、整點換色（1.5 秒淡入）；白天 06–17 淺色、晚上 18–05 深色；沒有手動切換按鈕。24 色預覽：本機 `demo-daylight.html`（不部署、不進 git） |
| 特效 | 工程方格紙底、CAD 十字游標、滑鼠經過格子亮起 logo 64 色階、點擊格子重力掉落（`prefers-reduced-motion` 時關閉） |
| 字體 | 英數：自架更紗英數裁字檔 `assets/fonts/SarasaTermTC-Latin-*.woff2`；中文：Google Fonts Noto Sans TC |
| AI 服務助理 | Worker `chengmu-chat`，路由 `chengmu.com.tw/api/*`，模型 `claude-haiku-5-5`（thinking disabled、effort high） |
| 對話紀錄 | D1 `chengmu-chat-log`：`chat_messages`（每則問答＋IP）、`leads`（轉交工程師紀錄） |
| 需求整理 | 客人需求模糊時（例如「想做系統但沒頭緒」），AI 一題一題問（附選項），最後產生「需求摘要」（系統目的／使用者／主要功能／資料內容／使用環境／現況與困擾／期望時程／待確認事項），摘要下有「複製摘要」按鈕；確認後留聯絡方式，摘要隨通知信寄給工程師。規則在 `src/index.js` RULES 第 11 條；對話上限 40 則（Worker `MAX_MESSAGES` 與前端 `MAX_HISTORY` 要一致） |
| 轉交工程師 | 客人有合作意願並留下聯絡方式時，AI 呼叫 `notify_engineer` 寄信到 chengmu00082998@gmail.com；同一段對話最多 3 封 |
| 中控台 | `D:\chengmu_deploy\ChengMu-Console.exe`（原始碼 `console/`），依日期／客戶 IP 看對話、搜尋、匯出 |
| 網域與郵件 DNS | chengmu.com.tw 綁 CF Pages；www 301 轉 apex；Email Routing 已啟用；SPF、DKIM、DMARC（`p=none`）都已設定 |

---

## 2. 檔案地圖

```
index.html                 首頁（CSS/JS 都在檔內）
404.html                   找不到頁面
robots.txt / sitemap.xml   sitemap 只有首頁；robots 每個 User-agent 組都要各自寫 Disallow: /api/
_headers                   Cloudflare Pages 回應標頭（X-Frame-Options、Permissions-Policy；CSP 尚未加）
favicon.ico                16/32/48 透明底，必須放在網站根目錄
assets/                    logo.svg、logo.png、line-qr.svg、fonts/
                           og-image.png（1200×630 分享圖）、logo-square.png（512 透明底，JSON-LD logo 用）、
                           apple-touch-icon.png（180 米色底，iOS 不支援透明）
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
- `index.html` 的圖示 `<link>` 一律用相對路徑（`favicon.ico`、`assets/...`）：GitHub Pages 備援站在 `/chengmu-homepage/` 子路徑下，寫 `/assets/...` 會壞。og:image、JSON-LD 則用完整的 `https://chengmu.com.tw/...`。
- 分享圖、圖示的產生方式（2026-11-08）：Chrome headless 截 HTML 範本（方格底＋logo＋公司名）成 1200×630，方形 logo 用透明底截 512×512 再以 Pillow 縮出 favicon 與 apple-touch-icon。改 logo 時要重做這幾張。
- 改了分享圖後，Facebook 要到 Sharing Debugger 按「Scrape Again」，LINE 要等快取過期才會換新預覽。
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
- 深淺色切換按鈕已拿掉（2026-11-05），改由時間自動決定；手機版時鐘縮成 13px 與公司英文名同列（320px 寬仍不換行）。
- 字級要老花友善；手機版 logo 與公司名並排、頁尾標籤與內容同一行。
- AI 只說明三項服務；服務對象寫「各行各業」；**知識文件與回答都不提 AI**（定位是工程師負責的專業資訊服務），只有客人直接問「你是不是 AI」才承認。
- 不提供客戶名稱、人名、報價金額、天數。
- 稱呼客人一律用「您」。
- 3 個產品頁與試玩頁已刪除（回 404，不做轉址）。

---

## 6. 待處理

| 項目 | 說明 |
|---|---|
| 隱私告知 | 輸入框下的「對話與 IP 會保存」已依使用者要求拿掉；個資法上建議之後在版權列放「隱私說明」連結 |
| Resend API key | 舊聯絡表單用的，Worker 已刪（2026-11-05），可到 Resend 後台撤銷 |
| 自有產品 | 是否讓 AI 介紹 PCC Tender Watch、家庭存摺、預算系統，使用者尚未決定（目前不介紹） |

D1 測試紀錄已於 2026-11-05 清除；保留的兩段（`3f0ecab6-…`、`25b7fb8e-…`）無法確定是測試還是真實訪客，未刪。
