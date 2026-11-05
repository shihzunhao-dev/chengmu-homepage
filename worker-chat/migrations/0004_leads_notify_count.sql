-- 2026-11-05：同一段對話允許補充通知（最多 3 封），記錄已寄次數
-- 執行：wrangler d1 execute chengmu-chat-log --remote --command "ALTER TABLE leads ADD COLUMN notify_count INTEGER NOT NULL DEFAULT 1"
ALTER TABLE leads ADD COLUMN notify_count INTEGER NOT NULL DEFAULT 1;
