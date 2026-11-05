-- 官網服務助理的對話紀錄（D1 資料庫 chengmu-chat-log）
-- 建立：wrangler d1 execute chengmu-chat-log --remote --file=./schema.sql
CREATE TABLE IF NOT EXISTS chat_messages (
  id              INTEGER PRIMARY KEY AUTOINCREMENT,
  conversation_id TEXT    NOT NULL,             -- 前端每個分頁產生一組，同一段對話共用
  created_at      TEXT    NOT NULL,             -- UTC ISO 8601
  role            TEXT    NOT NULL,             -- user / assistant / error
  content         TEXT    NOT NULL,
  country         TEXT,                         -- Cloudflare 判斷的國家代碼
  city            TEXT,
  usage           TEXT,                         -- assistant 那筆的 token 用量
  ip              TEXT                          -- 客戶連線 IP（CF-Connecting-IP），中控台依此分組
);
CREATE INDEX IF NOT EXISTS idx_chat_conv ON chat_messages (conversation_id, id);
CREATE INDEX IF NOT EXISTS idx_chat_time ON chat_messages (created_at);
CREATE INDEX IF NOT EXISTS idx_chat_ip ON chat_messages (ip, id);

-- 轉交工程師的需求紀錄（同 migrations/0003_leads.sql）
CREATE TABLE IF NOT EXISTS leads (
  lead_key      TEXT PRIMARY KEY,
  created_at    TEXT NOT NULL,
  ip            TEXT,
  customer_name TEXT,
  contact       TEXT,
  company       TEXT,
  service       TEXT,
  summary       TEXT
);
CREATE INDEX IF NOT EXISTS idx_leads_time ON leads (created_at);
