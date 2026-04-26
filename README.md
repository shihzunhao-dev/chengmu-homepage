# 澄沐實業有限公司 — 公司形象網站

[![Pages](https://img.shields.io/badge/deploy-Cloudflare%20Pages-orange)](https://pages.cloudflare.com)

> 為客戶提供深度客製軟體與資訊情報工具。

## 結構

```
chengmu_homepage/
├── index.html                          首頁（含聯絡表單）
├── assets/
│   └── logo.png                        公司 logo（</> 漸層）
├── products/
│   ├── pcc-tender-watch.html           政府採購標案監控
│   ├── my-health-bank.html             家庭存摺
│   └── budget-system.html              客製化預算管理系統
├── worker/
│   ├── contact.js                      Cloudflare Worker — 聯絡表單後端
│   └── DEPLOY.md                       Worker 部署指南（Resend API + 環境變數）
└── README.md
```

純靜態站，無 build step，無外部依賴（除 Google Fonts CDN）。

## 本機預覽

```bash
cd D:\chengmu_homepage
python -m http.server 3500
# → http://localhost:3500
```

## 設計系統

- **色系**：暗色 Linear（`#08090a` + `#7170ff`）/ 淺色 Claude Code Desktop（`#faf9f5` + `#c96442`）
- **字型**：Inter + JetBrains Mono + Noto Sans TC
- **動效**：page transition、glow sphere（mouse-tracking）、scroll reveal
- **主題切換**：localStorage key `cm-theme`

## 部署

### GitHub Pages（已部署 ✅）

🟢 **https://shihzunhao-dev.github.io/chengmu-homepage/**

push 到 main 後 30-60 秒自動更新。

### Cloudflare Pages（升級用，可同時並存）

更快、無頻寬上限、更接近 Worker 生態：

1. 登入 <https://dash.cloudflare.com> → **Workers & Pages** → **Create** → **Pages**
2. **Connect to Git** → 授權 GitHub → 選 `chengmu-homepage` repo
3. Build 設定：
   - Framework preset: **None**
   - Build command: **(留空)**
   - Build output directory: **`/`**
4. **Save and Deploy** → 30 秒後拿到 `https://chengmu-homepage.pages.dev`
5. （可選）**Custom domains** → 綁自訂網域，自動發 SSL

### 聯絡表單 Worker（必要：表單才能寄信）

詳見 [`worker/DEPLOY.md`](worker/DEPLOY.md)。流程摘要：

1. 申請 [Resend](https://resend.com) 拿 API key（免費 3000 封/月）
2. dash.cloudflare.com → 建 Worker `chengmu-contact` → 貼 `worker/contact.js`
3. Worker Settings → Variables 加：
   - `RESEND_API_KEY` (Secret)
   - `RECIPIENT_EMAIL` (Plain) = chengmu00082998@gmail.com
   - `ALLOWED_ORIGINS` (Plain) = `https://shihzunhao-dev.github.io,https://chengmu-homepage.pages.dev`
4. 完成後 `index.html` 內的 `CONTACT_WORKER_URL` 對齊 Worker 實際 URL

→ Worker 沒部署前，表單會顯示「網路錯誤」；用戶可改用 Email / LINE / 電話三張卡片直接聯絡（仍可正常用）。

## 聯絡

- Email: chengmu00082998@gmail.com
- LINE: [@378vuckv](https://line.me/R/ti/p/@378vuckv)
- 電話: 0952-159-801

---

© 2026 澄沐實業有限公司 / CHENGMU INDUSTRIAL CO., LTD.
