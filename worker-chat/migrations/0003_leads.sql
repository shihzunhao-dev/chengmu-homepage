-- 2026-11-05：轉交工程師的需求紀錄（notify_engineer 寄信前先寫入，lead_key 擋同一段對話重複寄信）
-- 執行：wrangler d1 execute chengmu-chat-log --remote --file=./migrations/0003_leads.sql
CREATE TABLE IF NOT EXISTS leads (
  lead_key      TEXT PRIMARY KEY,   -- 對話編號；沒有編號時用 ip:<IP>
  created_at    TEXT NOT NULL,      -- UTC ISO 8601
  ip            TEXT,
  customer_name TEXT,
  contact       TEXT,
  company       TEXT,
  service       TEXT,
  summary       TEXT
);
CREATE INDEX IF NOT EXISTS idx_leads_time ON leads (created_at);
