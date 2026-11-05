# 澄沐實業有限公司 — 官網

正式網址：https://chengmu.com.tw/

極簡單頁官網：公司 logo、三項服務（客製化程式系統開發／程式系統維護／資訊專業服務），
首頁輸入框直接連到 AI 服務助理（Claude Haiku 5.5），客人有合作意願時會整理需求寄信通知。

**現況、檔案地圖、部署步驟、設計決定與待辦，都寫在 [`HANDOFF.md`](HANDOFF.md)。**

## 結構

```
index.html            首頁（CSS/JS 都在檔內）
404.html              找不到頁面
assets/               logo、LINE QR code、更紗英數字型
deploy.ps1            網站部署（只上傳 $SiteFiles 白名單）
worker-chat/          AI 服務助理（Cloudflare Worker + D1 + 寄信）
console/              ChengMu-Console 中控台（看對話紀錄）
```

## 本機預覽

```powershell
cd D:\chengmu_homepage
python -m http.server 3500
# → http://localhost:3500（/api/chat 不可用，對話會顯示無法連線）
```

## 部署（兩個部署要分開做）

```powershell
# 網站
.\deploy.ps1 "commit message"     # git push + Cloudflare Pages
.\deploy.ps1 -SkipGit             # 只推 Cloudflare Pages

# AI 服務助理（改了 worker-chat/ 底下任何檔案，含 knowledge.md）
cd worker-chat
wrangler deploy
```

新增網站檔案時要加進 `deploy.ps1` 的 `$SiteFiles` 白名單，否則不會上線。

## 聯絡

- Email: chengmu00082998@gmail.com
- LINE: [@378vuckv](https://line.me/R/ti/p/@378vuckv)
- 電話: 0952-159-801

---

© 2026 澄沐實業有限公司 / CHENGMU INDUSTRIAL CO., LTD.
