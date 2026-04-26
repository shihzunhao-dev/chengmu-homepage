# 澄沐實業有限公司 — 公司形象網站

[![Pages](https://img.shields.io/badge/deploy-Cloudflare%20Pages-orange)](https://pages.cloudflare.com)

> 為客戶提供深度客製軟體與資訊情報工具。

## 結構

```
chengmu_homepage/
├── index.html                          首頁（暗色預設 + 淺色切換）
├── assets/
│   └── logo.png                        公司 logo（</> 漸層）
├── products/
│   ├── pcc-tender-watch.html           政府採購標案監控
│   ├── my-health-bank.html             家庭存摺
│   └── budget-system.html              客製化預算管理系統
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

### Cloudflare Pages（推薦）

1. 推到 GitHub
2. dash.cloudflare.com → Workers & Pages → Create → Pages → Connect to Git
3. Build command 留空、Output directory `/`
4. 取得 `https://chengmu-homepage.pages.dev`
5. （可選）綁自訂網域

### GitHub Pages

1. 推到 GitHub
2. Repo Settings → Pages → Source: main branch / root
3. 取得 `https://<user>.github.io/<repo>/`

## 聯絡

- Email: chengmu00082998@gmail.com
- LINE: [@378vuckv](https://line.me/R/ti/p/@378vuckv)
- 電話: 0952-159-801

---

© 2026 澄沐實業有限公司 / CHENGMU INDUSTRIAL CO., LTD.
