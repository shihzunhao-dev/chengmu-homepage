# 聯絡表單 Worker 部署指南

把 `contact.js` 部署到 Cloudflare Worker，作為官網表單的後端。**全程約 10-15 分鐘、完全免費。**

---

## 架構

```
網站表單 (POST JSON)
    ↓
Cloudflare Worker (chengmu-contact.<handle>.workers.dev)
    ↓ (Resend API)
Resend
    ↓ (SMTP)
chengmu00082998@gmail.com
```

---

## Step 1：申請 Resend 帳號（1 分鐘）

1. 開啟 <https://resend.com/signup>
2. 用 email 或 GitHub 註冊（免費 3,000 封/月、100 封/天 — 對小型官網夠用幾年）
3. 登入後跳到 dashboard

---

## Step 2：取得 Resend API Key（30 秒）

1. dashboard → 左側 **API Keys** → **Create API Key**
2. Name 填：`chengmu-contact`
3. Permission：`Sending access`
4. Domain：`All domains`
5. 點 **Add** → **複製整串 `re_...` 開頭的 key**（**只會出現一次**，先存到記事本）

---

## Step 3：建立 Cloudflare Worker（5 分鐘）

> 你已經為 pcc-license 部署過 Worker，這次流程相同。

1. 登入 <https://dash.cloudflare.com> → 左側 **Workers & Pages** → **Overview**
2. 點 **Create application** → **Create Worker**
3. Name 填：`chengmu-contact`
4. 點 **Deploy**（先發個 hello world）
5. 部署完點 **Edit code**
6. 把整個 `worker/contact.js` 內容**全選複製貼上**（覆蓋預設模板）
7. 點右上 **Save and deploy**

---

## Step 4：設定環境變數 / Secrets（2 分鐘）

在 Worker 頁面 → **Settings** → **Variables and Secrets**：

### 4a. Secret: `RESEND_API_KEY`

1. 點 **Add variable**
2. Type 選 **Secret**（重要 — 才會加密儲存）
3. Variable name: `RESEND_API_KEY`
4. Value: 貼上 Step 2 的 `re_...` key
5. 點 **Save**

### 4b. Plain: `RECIPIENT_EMAIL`（可選，預設用 chengmu00082998@gmail.com）

1. 點 **Add variable**
2. Type 選 **Plain text**
3. Variable name: `RECIPIENT_EMAIL`
4. Value: `chengmu00082998@gmail.com`
5. 點 **Save**

### 4c. Plain: `ALLOWED_ORIGINS`（CORS 白名單，建議設）

1. 點 **Add variable**
2. Type 選 **Plain text**
3. Variable name: `ALLOWED_ORIGINS`
4. Value（依實際網域，逗號分隔）：
   ```
   https://shihzunhao-dev.github.io,https://chengmu-homepage.pages.dev,http://localhost:3500
   ```
5. 點 **Save and deploy**

> 不設這個變數的話會預設 `*`（任何網站都能呼叫你的 Worker，浪費你的免費額度且容易被濫用）

---

## Step 5：測試 Worker（1 分鐘）

開啟 PowerShell 或 cmd 跑：

```powershell
curl.exe -X POST https://chengmu-contact.chengmu00082998.workers.dev/ ^
  -H "Content-Type: application/json" ^
  -H "Origin: https://shihzunhao-dev.github.io" ^
  -d "{\"name\":\"測試\",\"email\":\"chengmu00082998@gmail.com\",\"message\":\"Hello from curl\"}"
```

預期回應：`{"ok":true}`

打開信箱應收到一封來自 `onboarding@resend.dev` 的訊息，主旨「[澄沐官網] 測試 來信」。

---

## Step 6：在網站 index.html 改 Worker URL（1 分鐘）

打開 `D:\chengmu_homepage\index.html`，搜尋：

```js
const CONTACT_WORKER_URL = 'https://chengmu-contact.chengmu00082998.workers.dev/';
```

確認你的 Worker URL（看 Cloudflare dashboard 上方顯示的網址）一致。如不一樣（例如 handle 改過）就改成你的。

存檔後 commit + push：
```bash
cd D:\chengmu_homepage
git add index.html
git commit -m "fix: 修正聯絡表單 Worker URL"
git push
```

---

## ❓ 常見狀況

### 表單送出後出現 CORS 錯誤
→ Step 4c 的 `ALLOWED_ORIGINS` 沒包含你前端的網域。把該網域加進去重新 deploy。

### 寄出後沒收到信
1. 看 Cloudflare Worker → **Logs** 即時觀察錯誤
2. 看 Resend dashboard → **Logs** 確認訊息有沒有送出
3. 檢查 Gmail 垃圾郵件（onboarding@resend.dev 寄信偶爾會被歸類）

### 想換成自己網域的寄件人 (chengmu.com.tw)
1. Resend → **Domains** → **Add Domain** → 填 chengmu.com.tw
2. 依指示在你的 DNS 加 SPF / DKIM record（Cloudflare DNS 裡新增 3-4 筆 TXT/CNAME）
3. Resend 驗證通過後，把 Worker 的 `from` 改成 `contact@chengmu.com.tw`
4. 從此 email 不會被歸類成 onboarding 測試地址

### 怕被濫用爆量？
- Resend 免費 100 封/天，超過會 429
- 若擔心 bot 發垃圾，可在 Worker 加 Cloudflare Turnstile（免費 reCAPTCHA 替代）
- 目前的 honeypot field（`website` 隱藏欄位）能擋掉 90% 笨 bot

---

## 維護成本

- Resend：**免費**（3,000/月、100/天）
- Cloudflare Worker：**免費**（10 萬請求/天）
- DNS：用既有 Cloudflare 帳號免費

**全部 0 元，可用幾年沒問題。**
