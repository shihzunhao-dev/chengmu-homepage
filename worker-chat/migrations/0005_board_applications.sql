-- 2026-11-22：線上會議白板建房密碼申請（src/board.js）
-- 執行：--file 走 /import 曾回 auth 10000，改用 --command 逐句執行（見 HANDOFF「D1 資料表變更」）
-- 密碼本身不存。姓名、單位、Email、用途只在「待確認」期間保存，申請完成（核發或拒絕）就清空，
-- 之後只留 Email 的 SHA-256 供次數限制；整筆紀錄 90 天後由每日排程刪除（KEEP_BOARD_DAYS）。
CREATE TABLE IF NOT EXISTS board_applications (
  id              TEXT PRIMARY KEY,           -- 申請單編號（隨機，前端確認時帶回）
  created_at      TEXT NOT NULL,              -- UTC ISO 8601
  conversation_id TEXT,
  ip              TEXT,
  lang            TEXT,                       -- zh / en
  email_hash      TEXT NOT NULL,              -- 小寫 Email 的 SHA-256
  name            TEXT,
  org             TEXT,
  email           TEXT,
  purpose         TEXT,
  status          TEXT NOT NULL,              -- pending / reserved / issued / rejected
  day             TEXT,                       -- 預留額度那天（UTC YYYY-MM-DD）
  result          TEXT,                       -- mailed / mail-failed-owner-notified / 拒絕原因
  finished_at     TEXT
);
CREATE INDEX IF NOT EXISTS idx_board_day ON board_applications (day, status);
CREATE INDEX IF NOT EXISTS idx_board_email ON board_applications (email_hash, day);
CREATE INDEX IF NOT EXISTS idx_board_ip ON board_applications (ip, created_at);
CREATE INDEX IF NOT EXISTS idx_board_conv ON board_applications (conversation_id);
