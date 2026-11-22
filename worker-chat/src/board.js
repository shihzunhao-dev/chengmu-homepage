// 線上會議白板（board.chengmu.com.tw）建房密碼申請：
//   1. 對話中 AI 收集資料後呼叫 request_board_code → createPending() 只建立「待確認」申請單，不發碼。
//   2. 前端顯示申請卡片，客人確認資料、通過 Turnstile 後 POST /api/board-apply → handleBoardApply()。
//   3. handleBoardApply() 驗 Turnstile、在 D1 原子預留額度、向白板要一組單次碼、用 Resend 寄給申請者、寄通知給澄沐。
// 安全原則：
//   - 密碼不經過模型、不進對話紀錄、不存 D1；只出現在寄給申請者的信（寄信失敗時改寄給澄沐請人工轉寄）。
//   - 寄給申請者的信是固定範本（密碼、期限、白板網址），不放客人填的任何文字，避免被拿來替陌生人寄自訂內容。
//   - 次數限制全部在伺服器端（同一段對話、同一個 email、同一個 IP、全站每天），不靠 AI 判斷。
//   - 必要的 secret 沒設齊時，工具與提示整段不啟用（fail closed）。

const BOARD_URL = "https://board.chengmu.com.tw";
const BOARD_API = `${BOARD_URL}/api/create-codes`;
const BOARD_PRIVACY = `${BOARD_URL}/privacy`;
const SITE_HOST = "chengmu.com.tw";
const TURNSTILE_ACTION = "board_apply";
const OWNER_EMAIL = "chengmu00082998@gmail.com";
const NOTIFY_SENDER = "assistant@chengmu.com.tw";            // Cloudflare send_email（只能寄給 OWNER_EMAIL）
const APPLICANT_SENDER = "澄沐實業 <noreply@chengmu.com.tw>"; // Resend（chengmu.com.tw 已驗證）

// 次數限制（日界線用 UTC，與 Resend 每日額度同步重算）
// 同一個 Email 每天 3 組（2026-11-22 使用者決定：主持人一天可能開好幾場）。
// 不限制同一段對話：conversationId 由前端產生，重新整理就換一組，擋不了人只造成不便。
// 白板端另有「同一個 Email 同時只能有一組未使用的密碼」，所以不會一次囤好幾組。
const LIMIT_PER_EMAIL_PER_DAY = 3;
const LIMIT_PER_IP_PER_DAY = 3;
const LIMIT_GLOBAL_PER_DAY = 50;          // Resend 免費版每天 100 封，保留一半給其他用途
const LIMIT_PENDING_PER_IP_PER_DAY = 10;  // 申請單（還沒確認）本身也限量，避免灌資料庫
const PENDING_TTL_MIN = 30;               // 申請單建立後多久內要確認
const FETCH_TIMEOUT_MS = 10_000;
export const KEEP_BOARD_DAYS = 90;        // 申請紀錄保存期限（與白板一致，privacy.html 同步）

const EMAIL_RE = /^[A-Za-z0-9._%+-]{1,64}@[A-Za-z0-9-]{1,63}(\.[A-Za-z0-9-]{1,63})+$/;

export function boardEnabled(env) {
  // Cloudflare Access 服務憑證是選用的（2026-11-22 使用者決定先不開 Zero Trust）：有設才帶，不列入啟用條件
  return Boolean(env.DB && env.EMAIL && env.BOARD_CODES_SECRET && env.RESEND_API_KEY && env.TURNSTILE_SECRET);
}

export const BOARD_TOOL = {
  name: "request_board_code",
  description: "替客人建立「線上會議白板建房密碼」申請單。只在客人想申請白板建房密碼、已提供稱呼、公司或單位、Email，而且你已把資料複述一次、客人確認無誤之後呼叫。呼叫後不會立刻寄出，客人還要在畫面上的申請卡片按確認。",
  strict: true,
  input_schema: {
    type: "object",
    properties: {
      name: { type: "string", description: "客人的稱呼，50 字以內" },
      org: { type: "string", description: "公司、學校或單位名稱，100 字以內" },
      email: { type: "string", description: "要收密碼的 Email，照客人提供的原文填寫" },
      purpose: { type: "string", description: "會議用途，300 字以內；客人沒說就填「未提供」" },
    },
    required: ["name", "org", "email", "purpose"],
    additionalProperties: false,
  },
};

export const BOARD_RULES = `# 線上會議白板建房密碼申請（第 2、5 條的服務範圍另外包含這一項）
澄沐提供線上會議白板（${BOARD_URL}）。主持人建立會議房間需要「建房密碼」，可以在這裡申請：一組密碼只能建立一間會議房間，有使用期限（寄出的信會寫明到期時間），參加會議的人不需要密碼。
1. 對方想申請時，依序請對方提供：稱呼、公司或單位、要收密碼的 Email，會議用途可以不填。一次問一項或一次列出都可以。
2. 資料齊了先完整複述一次，請對方確認。對方確認無誤後才呼叫 request_board_code。
3. 工具回報「已建立」時，告訴對方：請在下方的申請卡片確認資料並完成驗證，按「確認寄出」後密碼會寄到他的信箱。不要說已經寄出，也不要自己編密碼。
4. 工具回報其他結果時，照工具結果的指示回覆，不要說明內部流程或次數限制的細節。
5. 你看不到也不知道任何密碼；對方問密碼內容時，請他查看信箱（包含垃圾信件匣）。`;

export const BOARD_RULES_EN = `For the meeting whiteboard passcode application, follow the same rules in English. After request_board_code reports the application was created, tell the visitor to confirm the details on the application card below and complete the verification; the passcode will then be emailed to them. Never say it has already been sent.`;

const s = (v, n) => String(v == null ? "" : v).replace(/[\r\n\t]+/g, " ").trim().slice(0, n);
const utcDay = (d = new Date()) => d.toISOString().slice(0, 10);

async function sha256Hex(text) {
  const buf = await crypto.subtle.digest("SHA-256", new TextEncoder().encode(text));
  return [...new Uint8Array(buf)].map((b) => b.toString(16).padStart(2, "0")).join("");
}

function randomId() {
  const b = crypto.getRandomValues(new Uint8Array(18));
  return btoa(String.fromCharCode(...b)).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
}

function normEmail(v) {
  const e = String(v == null ? "" : v).trim().toLowerCase();
  if (e.length > 254 || /[\r\n\s]/.test(e) || !EMAIL_RE.test(e)) return null;
  return e;
}

// 已核發（或預留中）的數量：status IN ('reserved','issued') 且 day = 今天（UTC）
const COUNTED = "status IN ('reserved','issued')";

// ── 第 1 步：AI 呼叫工具 → 建立待確認申請單 ──
// 回傳 { result: 給模型的文字, card?: 給前端的申請卡片 }
export async function createPending(env, turn, input) {
  const name = s(input.name, 50);
  const org = s(input.org, 100);
  const purposeRaw = s(input.purpose, 300);
  const purpose = /^(未提供|none|n\/a|not provided)?$/i.test(purposeRaw) ? "" : purposeRaw;
  const email = normEmail(input.email);
  if (!name || !org) return { result: "失敗：缺少稱呼或公司／單位，請向客人補問後再呼叫。" };
  if (!email) return { result: "失敗：Email 格式不正確，請請客人重新提供正確的 Email。" };

  const emailHash = await sha256Hex(email);
  const day = utcDay();
  const conv = turn.conversationId;
  const ip = turn.ip || "unknown";
  const q = (sql, ...b) => env.DB.prepare(sql).bind(...b).first();

  // 先擋明顯會失敗的情況，讓 AI 當場說明（真正的把關在第 2 步的原子預留）
  const emailToday = await q(`SELECT COUNT(*) AS n FROM board_applications WHERE email_hash = ? AND day = ? AND ${COUNTED}`, emailHash, day);
  if (emailToday && emailToday.n >= LIMIT_PER_EMAIL_PER_DAY) {
    return { result: "已達上限：這個 Email 今天的申請次數已達上限，請客人明天再申請，或直接聯絡澄沐。" };
  }
  const ipToday = await q(`SELECT COUNT(*) AS n FROM board_applications WHERE ip = ? AND day = ? AND ${COUNTED}`, ip, day);
  const ipPending = await q("SELECT COUNT(*) AS n FROM board_applications WHERE ip = ? AND created_at >= ?", ip, `${day}T00:00:00.000Z`);
  if ((ipToday && ipToday.n >= LIMIT_PER_IP_PER_DAY) || (ipPending && ipPending.n >= LIMIT_PENDING_PER_IP_PER_DAY)) {
    return { result: "已達上限：今天的申請次數已達上限，請客人明天再試，或直接聯絡澄沐。" };
  }

  const id = randomId();
  await env.DB.prepare(
    "INSERT INTO board_applications (id, created_at, conversation_id, ip, lang, email_hash, name, org, email, purpose, status) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 'pending')"
  ).bind(id, new Date().toISOString(), conv, ip, turn.lang, emailHash, name, org, email, purpose).run();
  return {
    result: "已建立：申請單已建立，等待客人在畫面上的申請卡片確認並完成驗證，按下確認後才會寄出。",
    card: { id, name, org, email, purpose },
  };
}

// ── 第 2 步：前端 POST /api/board-apply ──
const MSG = {
  done: { zh: (e, x) => `建房密碼已寄到 ${e}，${x} 前有效，只能建立一間會議房間。沒收到的話請查看垃圾信件匣。`,
          en: (e, x) => `Your passcode has been sent to ${e}. It is valid until ${x} (Taiwan time) and can create one meeting room. If you don't see it, please check your spam folder.` },
  mailFailed: { zh: () => "密碼已核發，但寄信時發生問題，澄沐會盡快以 Email 把密碼寄給您。",
                en: () => "The passcode was issued, but sending the email failed. We will email it to you as soon as possible." },
  verify: { zh: () => "驗證沒有通過，請重新整理頁面後再試一次。", en: () => "Verification failed. Please reload the page and try again." },
  expired: { zh: () => "這張申請單已失效，請在對話中重新申請。", en: () => "This application has expired. Please apply again in the chat." },
  dupEmail: { zh: () => "這個 Email 今天的申請次數已達上限，請明天再申請，或直接聯絡澄沐。", en: () => "This email address has reached today's request limit. Please try again tomorrow or contact us directly." },
  limit: { zh: () => "今天的申請名額已滿，請明天再試，或直接聯絡澄沐。", en: () => "Today's application limit has been reached. Please try again tomorrow or contact us directly." },
  already: { zh: (r) => r || "這個 Email 已經有一組尚未使用的密碼，請先使用它。", en: () => "This email address already has an unused passcode. Please use that one first." },
  invalid: { zh: (r) => `申請資料有誤：${r || "請檢查後在對話中重新申請"}。`, en: () => "Some of the details are invalid. Please check them and apply again in the chat." },
  unavailable: { zh: () => "目前暫時無法申請，請稍後再試，或直接聯絡澄沐。", en: () => "Applications are temporarily unavailable. Please try again later or contact us directly." },
};
const say = (lang, key, ...a) => (MSG[key][lang] || MSG[key].zh)(...a);

function reply(status, body) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "content-type": "application/json; charset=utf-8", "cache-control": "no-store" },
  });
}

async function verifyTurnstile(env, token, ip, id) {
  if (typeof token !== "string" || !token || token.length > 2048) return false;
  const form = new FormData();
  form.append("secret", env.TURNSTILE_SECRET);
  form.append("response", token);
  if (ip) form.append("remoteip", ip);
  form.append("idempotency_key", crypto.randomUUID());
  try {
    const r = await fetch("https://challenges.cloudflare.com/turnstile/v0/siteverify", { method: "POST", body: form, signal: AbortSignal.timeout(FETCH_TIMEOUT_MS) });
    const d = await r.json();
    if (!d.success) console.warn("turnstile fail", id, JSON.stringify(d["error-codes"] || []));
    return d.success === true && d.hostname === SITE_HOST && d.action === TURNSTILE_ACTION;
  } catch (err) {
    console.error("turnstile error", err && err.message);
    return false;
  }
}

async function hmacHex(secret, text) {
  const key = await crypto.subtle.importKey("raw", new TextEncoder().encode(secret), { name: "HMAC", hash: "SHA-256" }, false, ["sign"]);
  const sig = await crypto.subtle.sign("HMAC", key, new TextEncoder().encode(text));
  return [...new Uint8Array(sig)].map((b) => b.toString(16).padStart(2, "0")).join("");
}

// 向白板要一組單次碼。回傳 { ok, code, expiresAt } 或 { ok:false, kind, reason }
async function requestCode(env, row) {
  const body = JSON.stringify({ name: row.name, org: row.org, email: row.email, ...(row.purpose ? { purpose: row.purpose } : {}), source: "homepage-chat" });
  const ts = String(Math.floor(Date.now() / 1000));
  const nonce = crypto.randomUUID().replace(/-/g, "");
  const sig = await hmacHex(env.BOARD_CODES_SECRET, `${ts}\n${nonce}\n${body}`);
  let res;
  try {
    res = await fetch(BOARD_API, {
      method: "POST",
      // 不帶 Origin（白板對帶了非白名單 Origin 的請求回 403）；簽的就是送出去的這個字串
      headers: {
        "content-type": "application/json",
        "x-timestamp": ts, "x-nonce": nonce, "x-signature": sig,
        ...(env.BOARD_ACCESS_CLIENT_ID && env.BOARD_ACCESS_CLIENT_SECRET
          ? { "cf-access-client-id": env.BOARD_ACCESS_CLIENT_ID, "cf-access-client-secret": env.BOARD_ACCESS_CLIENT_SECRET } : {}),
      },
      body,
      redirect: "manual",   // Access 擋下時會 302 到登入頁，不跟著轉
      signal: AbortSignal.timeout(FETCH_TIMEOUT_MS),
    });
  } catch (err) {
    console.error("board fetch failed", err && err.message);
    return { ok: false, kind: "unavailable" };
  }
  let d = {};
  try { d = await res.json(); } catch { /* 非 JSON（例如 Access 登入頁） */ }
  if (res.status === 201 && typeof d.code === "string" && d.code && d.expiresAt) return { ok: true, code: d.code, expiresAt: d.expiresAt };
  console.warn("board refused", res.status, d.error || "");
  if (res.status === 409) return { ok: false, kind: "already", reason: s(d.reason, 200) };
  if (res.status === 400) return { ok: false, kind: "invalid", reason: s(d.reason, 200) };
  if (res.status === 429) return { ok: false, kind: "limit" };
  return { ok: false, kind: "unavailable" };
}

function twTime(iso) {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return String(iso);
  return new Date(d.getTime() + 8 * 3600 * 1000).toISOString().replace("T", " ").slice(0, 16);
}

function applicantMail(lang, code, expires) {
  if (lang === "en") {
    return {
      subject: "Your meeting whiteboard passcode",
      text: [
        "Hello,", "",
        "Here is the room-creation passcode for the online meeting whiteboard you requested:", "",
        `    ${code}`, "",
        `- Valid until: ${expires} (Taiwan time, UTC+8)`,
        "- Single use: it expires once you create one meeting room",
        `- How to use: go to ${BOARD_URL}, create a meeting room and enter this passcode`, "",
        "If you did not request this, please ignore this email. The passcode will expire automatically.", "",
        "CHENGMU INDUSTRIAL CO., LTD.",
        `Privacy: ${BOARD_PRIVACY}`,
      ].join("\n"),
    };
  }
  return {
    subject: "您的線上會議白板建房密碼",
    text: [
      "您好：", "",
      "您申請的線上會議白板建房密碼如下：", "",
      `    ${code}`, "",
      `・有效期限：${expires}（台灣時間）前`,
      "・只能使用一次：建立一間會議房間後即失效",
      `・使用方式：前往 ${BOARD_URL} 建立會議房間時輸入這組密碼`, "",
      "如果您沒有提出申請，請忽略這封信，密碼到期後會自動失效。", "",
      "澄沐實業有限公司",
      `隱私權說明：${BOARD_PRIVACY}`,
    ].join("\n"),
  };
}

async function sendResend(env, id, to, mail) {
  try {
    const r = await fetch("https://api.resend.com/emails", {
      method: "POST",
      headers: { authorization: `Bearer ${env.RESEND_API_KEY}`, "content-type": "application/json", "idempotency-key": `board-${id}` },
      body: JSON.stringify({ from: APPLICANT_SENDER, to: [to], reply_to: OWNER_EMAIL, subject: mail.subject, text: mail.text }),
      signal: AbortSignal.timeout(FETCH_TIMEOUT_MS),
    });
    if (r.ok) return true;
    console.error("resend failed", r.status, (await r.text()).slice(0, 200));
    return false;
  } catch (err) {
    console.error("resend error", err && err.message);
    return false;
  }
}

async function notifyOwner(env, subject, lines) {
  try {
    await env.EMAIL.send({ to: OWNER_EMAIL, from: NOTIFY_SENDER, subject, text: lines.join("\n") });
    return true;
  } catch (err) {
    console.error("owner notify failed", err && (err.code || err.message));
    return false;
  }
}

export async function handleBoardApply(request, env, ip, ctx) {
  let body;
  try { body = await request.json(); } catch { return reply(400, { error: "bad request" }); }
  const id = typeof body.id === "string" && /^[A-Za-z0-9_-]{20,40}$/.test(body.id) ? body.id : null;
  const conv = typeof body.conversationId === "string" ? body.conversationId : "";
  const row = id && (await env.DB.prepare("SELECT * FROM board_applications WHERE id = ?").bind(id).first());
  const lang = row && row.lang === "en" ? "en" : ((request.headers.get("X-Site-Lang") || "").toLowerCase() === "en" ? "en" : "zh");
  const fail = (key, status = 409, ...a) => reply(status, { ok: false, message: say(lang, key, ...a) });

  const fresh = row && row.status === "pending" && row.conversation_id === conv && row.ip === (ip || "unknown")
    && Date.now() - Date.parse(row.created_at) < PENDING_TTL_MIN * 60_000;
  if (!fresh) return fail("expired", 410);
  if (!(await verifyTurnstile(env, body.token, ip, id))) return fail("verify", 403);

  // 原子預留：只有在各項額度都還有剩時，才把這張單從 pending 改成 reserved
  const day = utcDay();
  const r = await env.DB.prepare(
    `UPDATE board_applications SET status = 'reserved', day = ?1 WHERE id = ?2 AND status = 'pending'
       AND (SELECT COUNT(*) FROM board_applications WHERE day = ?1 AND ${COUNTED}) < ?3
       AND (SELECT COUNT(*) FROM board_applications WHERE day = ?1 AND ip = ?4 AND ${COUNTED}) < ?5
       AND (SELECT COUNT(*) FROM board_applications WHERE day = ?1 AND email_hash = ?6 AND ${COUNTED}) < ?7`
  ).bind(day, id, LIMIT_GLOBAL_PER_DAY, row.ip, LIMIT_PER_IP_PER_DAY, row.email_hash, LIMIT_PER_EMAIL_PER_DAY).run();

  const finish = (status, result, keepPersonal = false) => env.DB.prepare(
    `UPDATE board_applications SET status = ?, result = ?, finished_at = ?${keepPersonal ? "" : ", name = NULL, org = NULL, email = NULL, purpose = NULL"} WHERE id = ?`
  ).bind(status, result, new Date().toISOString(), id).run();

  if (!r.meta || r.meta.changes !== 1) {
    // 找出是哪一項額度用完，回給客人對應的說明
    const q = (sql, ...b) => env.DB.prepare(sql).bind(...b).first();
    let key = "limit";
    const e = await q(`SELECT COUNT(*) AS n FROM board_applications WHERE day = ? AND email_hash = ? AND ${COUNTED}`, day, row.email_hash);
    if (e && e.n >= LIMIT_PER_EMAIL_PER_DAY) key = "dupEmail";
    await finish("rejected", key);
    return fail(key, 429);
  }

  // 預留之後的步驟（要碼、寄信、收尾）包成一個 promise 交給 waitUntil：
  // 客人中途關掉分頁時 Worker 仍會跑完，不會出現「白板已核發、信沒寄出、紀錄卡在 reserved」。
  const work = issueAndMail(env, row, id, lang, finish, fail).catch(async (err) => {
    console.error("board issue failed", err && err.message);
    try { await finish("rejected", "error"); } catch { /* 每日排程會清掉卡住的紀錄 */ }
    return fail("unavailable", 500);
  });
  if (ctx && ctx.waitUntil) ctx.waitUntil(work);
  return await work;
}

async function issueAndMail(env, row, id, lang, finish, fail) {
  const got = await requestCode(env, row);
  if (!got.ok) {
    await finish("rejected", got.kind);   // 預留的額度跟著釋放（rejected 不計入）
    return fail(got.kind, got.kind === "unavailable" ? 503 : 409, got.reason);
  }

  const expires = twTime(got.expiresAt);
  const mailed = await sendResend(env, id, row.email, applicantMail(lang, got.code, expires));
  const info = [
    `時間：${twTime(new Date().toISOString())}（台灣）`,
    `稱呼：${row.name}`,
    `公司／單位：${row.org}`,
    `Email：${row.email}`,
    `用途：${row.purpose || "未提供"}`,
    `密碼期限：${expires}（台灣）`,
    `來源：IP ${row.ip}　對話編號：${row.conversation_id}${lang === "en" ? "　英文版官網" : ""}`,
  ];
  if (mailed) {
    await notifyOwner(env, `[白板申請] ${row.name}｜${row.org}`, ["已寄出會議白板建房密碼給申請者（密碼不列在本信）。", "", ...info]);
    await finish("issued", "mailed");
    return reply(200, { ok: true, message: say(lang, "done", row.email, expires) });
  }
  // 寄給申請者失敗：白板已核發（同一個 Email 在密碼用掉或到期前無法再申請），改把密碼寄給澄沐人工轉寄
  const backup = await notifyOwner(env, `[白板申請・需人工轉寄] ${row.name}｜${row.org}`, [
    "寄信給申請者失敗，請手動把下面的密碼寄給申請者：", "", `    ${got.code}`, "", ...info,
  ]);
  await finish("issued", backup ? "mail-failed-owner-notified" : "mail-failed");
  return reply(200, { ok: true, message: say(lang, backup ? "mailFailed" : "unavailable") });
}
