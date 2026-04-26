/**
 * Cloudflare Worker — 澄沐官網聯絡表單後端
 *
 * 流程：網站 form POST → 本 Worker → Resend API 寄信 → 收件人信箱
 *
 * 必要環境變數（在 Cloudflare dashboard → Settings → Variables 設定）：
 *   RESEND_API_KEY   (Secret) — Resend API key（從 https://resend.com/api-keys 申請）
 *   RECIPIENT_EMAIL  (Plain)  — 收件人 email（預設 chengmu00082998@gmail.com）
 *   ALLOWED_ORIGINS  (Plain)  — CORS 允許來源，逗號分隔（建議填網站正式網域）
 *                                範例: https://shihzunhao-dev.github.io,https://chengmu-homepage.pages.dev
 *
 * 部署：dash.cloudflare.com → Workers & Pages → Create Worker → 貼上本檔內容
 *      Worker name 建議: chengmu-contact
 *      預設 URL: https://chengmu-contact.<你的 handle>.workers.dev
 */

const DEFAULT_RECIPIENT = 'chengmu00082998@gmail.com';

export default {
  async fetch(request, env) {
    const origin = request.headers.get('Origin') || '';
    const corsHeaders = buildCorsHeaders(origin, env);

    // CORS preflight
    if (request.method === 'OPTIONS') {
      return new Response(null, { status: 204, headers: corsHeaders });
    }

    if (request.method !== 'POST') {
      return jsonResp({ ok: false, error: 'Method not allowed' }, 405, corsHeaders);
    }

    // Parse JSON body
    let body;
    try {
      body = await request.json();
    } catch {
      return jsonResp({ ok: false, error: 'Invalid JSON' }, 400, corsHeaders);
    }

    const { name, company, email, phone, message, website } = body;

    // Honeypot — 隱藏欄位 bot 會填、人不會填，假裝成功不寄信
    if (website && String(website).length > 0) {
      return jsonResp({ ok: true }, 200, corsHeaders);
    }

    // 基本驗證
    if (!name || !email || !message) {
      return jsonResp({ ok: false, error: '請填寫姓名、Email、訊息' }, 400, corsHeaders);
    }
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(String(email))) {
      return jsonResp({ ok: false, error: 'Email 格式不正確' }, 400, corsHeaders);
    }
    if (String(name).length > 100 || String(message).length > 5000) {
      return jsonResp({ ok: false, error: '欄位過長' }, 400, corsHeaders);
    }

    // 寄信
    const recipient = env.RECIPIENT_EMAIL || DEFAULT_RECIPIENT;
    const subject = `[澄沐官網] ${name}${company ? ' (' + company + ')' : ''} 來信`;
    const html = buildEmailHtml({
      name, company, email, phone, message,
      ip: request.headers.get('CF-Connecting-IP') || 'unknown',
      ua: request.headers.get('User-Agent') || 'unknown',
    });

    if (!env.RESEND_API_KEY) {
      console.error('Missing RESEND_API_KEY');
      return jsonResp({ ok: false, error: '伺服器設定錯誤' }, 500, corsHeaders);
    }

    try {
      const resp = await fetch('https://api.resend.com/emails', {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${env.RESEND_API_KEY}`,
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          from: '澄沐官網表單 <onboarding@resend.dev>',
          to: [recipient],
          reply_to: email,
          subject,
          html,
        }),
      });

      if (!resp.ok) {
        const errText = await resp.text();
        console.error('Resend API error:', resp.status, errText);
        return jsonResp(
          { ok: false, error: '寄信失敗，請改用 email 直接聯絡' },
          500,
          corsHeaders
        );
      }

      return jsonResp({ ok: true }, 200, corsHeaders);
    } catch (err) {
      console.error('Worker exception:', err);
      return jsonResp(
        { ok: false, error: '伺服器錯誤，請改用 email 直接聯絡' },
        500,
        corsHeaders
      );
    }
  },
};

function buildCorsHeaders(origin, env) {
  const allowed = (env.ALLOWED_ORIGINS || '*')
    .split(',')
    .map(s => s.trim())
    .filter(Boolean);

  const isWildcard = allowed.length === 1 && allowed[0] === '*';
  const matched = isWildcard ? '*' : allowed.includes(origin) ? origin : allowed[0];

  return {
    'Access-Control-Allow-Origin': matched,
    'Access-Control-Allow-Methods': 'POST, OPTIONS',
    'Access-Control-Allow-Headers': 'Content-Type',
    'Access-Control-Max-Age': '86400',
    'Vary': 'Origin',
  };
}

function jsonResp(data, status, extraHeaders = {}) {
  return new Response(JSON.stringify(data), {
    status,
    headers: { 'Content-Type': 'application/json; charset=utf-8', ...extraHeaders },
  });
}

function escapeHtml(str) {
  return String(str)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;');
}

function buildEmailHtml({ name, company, email, phone, message, ip, ua }) {
  const row = (label, value) => value ? `
    <tr>
      <td style="padding:10px 14px;border-bottom:1px solid #eee;font-weight:600;width:90px;color:#444;background:#fafafa;">${label}</td>
      <td style="padding:10px 14px;border-bottom:1px solid #eee;color:#1a1a1a;">${value}</td>
    </tr>` : '';

  return `
<!DOCTYPE html>
<html>
<body style="font-family:-apple-system,'Segoe UI',sans-serif;background:#f5f5f5;padding:24px;margin:0;">
  <div style="max-width:640px;margin:0 auto;background:#fff;border-radius:12px;overflow:hidden;border:1px solid #e5e5e5;">
    <div style="background:#08090a;color:#fff;padding:20px 28px;">
      <div style="font-size:18px;font-weight:600;letter-spacing:-0.4px;">澄沐官網表單新訊息</div>
      <div style="font-size:12px;color:#8a8f98;margin-top:4px;">CHENGMU INDUSTRIAL CO., LTD. — Contact Form</div>
    </div>
    <table style="border-collapse:collapse;width:100%;font-size:14px;line-height:1.6;">
      ${row('姓名', escapeHtml(name))}
      ${row('公司', escapeHtml(company || ''))}
      ${row('Email', `<a href="mailto:${escapeHtml(email)}" style="color:#5e6ad2;text-decoration:none;">${escapeHtml(email)}</a>`)}
      ${row('電話', escapeHtml(phone || ''))}
      <tr>
        <td style="padding:10px 14px;font-weight:600;width:90px;color:#444;background:#fafafa;vertical-align:top;">訊息</td>
        <td style="padding:14px;color:#1a1a1a;white-space:pre-wrap;">${escapeHtml(message)}</td>
      </tr>
    </table>
    <div style="padding:16px 28px;background:#fafafa;font-size:11px;color:#888;border-top:1px solid #eee;">
      <div>來源：chengmu-homepage 聯絡表單</div>
      <div>IP：${escapeHtml(ip)}</div>
      <div>時間：${new Date().toISOString()}</div>
    </div>
  </div>
  <div style="text-align:center;color:#999;font-size:11px;margin-top:16px;">
    可直接回覆此 email — 會送到對方信箱（reply-to: ${escapeHtml(email)}）
  </div>
</body>
</html>`;
}
