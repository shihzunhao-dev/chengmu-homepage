-- 2026-11-04：已建立的 chat_messages 加上 ip 欄位（新建資料庫直接用 schema.sql，不用跑這支）
-- 執行：wrangler d1 execute chengmu-chat-log --remote --file=./migrations/0002_add_ip.sql
ALTER TABLE chat_messages ADD COLUMN ip TEXT;
CREATE INDEX IF NOT EXISTS idx_chat_ip ON chat_messages (ip, id);
