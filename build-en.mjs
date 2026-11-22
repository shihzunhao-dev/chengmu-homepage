// 由中文首頁 index.html 產生英文版 en/index.html。
//   node build-en.mjs
// 中文首頁是唯一的原稿：改首頁時只改 index.html，再跑這支（deploy.ps1 會自動跑）。
// 下方對照表的每一句中文都必須在原稿裡找得到，找不到就報錯停止，避免產生半中半英的英文版；
// 改了首頁文字而報錯時，回來更新對照表。
import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const SRC = path.join(HERE, 'index.html');
const OUT = path.join(HERE, 'en', 'index.html');

// 整段替換：原稿用 <!--名稱--> … <!--/名稱--> 標起來的區塊
const BLOCKS = {
  'lang-auto': '',   // 自動判斷語言只放在中文首頁
  ld: `<script type="application/ld+json">
{
  "@context": "https://schema.org",
  "@graph": [
    {
      "@type": "Organization",
      "@id": "https://chengmu.com.tw/#organization",
      "name": "CHENGMU INDUSTRIAL CO., LTD.",
      "alternateName": "澄沐實業有限公司",
      "url": "https://chengmu.com.tw/en/",
      "logo": { "@type": "ImageObject", "url": "https://chengmu.com.tw/assets/logo-square.png", "width": 512, "height": 512 },
      "image": "https://chengmu.com.tw/assets/og-image.png",
      "description": "An engineer-led software studio in Taiwan offering custom software development, system maintenance and IT consulting services.",
      "email": "chengmu00082998@gmail.com",
      "telephone": "+886-952-159-801",
      "areaServed": { "@type": "Country", "name": "Taiwan" },
      "sameAs": ["https://line.me/R/ti/p/@378vuckv"],
      "knowsAbout": ["Operations management systems", "Warehouse management systems", "Electronic approval workflows", "Report automation", "System integration", "Legacy system maintenance", "ASP.NET", "SQL Server", "Android field apps"],
      "makesOffer": [
        { "@type": "Offer", "itemOffered": { "@type": "Service", "name": "Custom Software Development", "description": "Systems built around your actual workflow, from requirements interviews, design and development to deployment, covering operations and warehouse management, e-approvals, field apps, reporting and system integration." } },
        { "@type": "Offer", "itemOffered": { "@type": "Service", "name": "System Maintenance", "description": "Bug fixing, enhancements, performance tuning and upgrades for existing systems, including legacy systems with little documentation whose original developers have left, plus security hardening and server migration." } },
        { "@type": "Offer", "itemOffered": { "@type": "Service", "name": "IT Consulting Services", "description": "System architecture and technology selection advice, data cleanup, report automation, and server and deployment planning." } }
      ],
      "contactPoint": { "@type": "ContactPoint", "contactType": "customer service", "telephone": "+886-952-159-801", "email": "chengmu00082998@gmail.com", "areaServed": "TW", "availableLanguage": ["zh-Hant", "en"] }
    },
    {
      "@type": "WebSite",
      "@id": "https://chengmu.com.tw/en/#website",
      "url": "https://chengmu.com.tw/en/",
      "name": "CHENGMU INDUSTRIAL CO., LTD.",
      "inLanguage": "en",
      "publisher": { "@id": "https://chengmu.com.tw/#organization" }
    }
  ]
}
</script>`,
  noscript: `<noscript>
  <section style="max-width: 1080px; margin: 0 auto; padding: 0 40px 48px; font-size: 16px;">
    <h2 style="font-size: 18px;">About ChengMu Industrial</h2>
    <p>CHENGMU INDUSTRIAL CO., LTD. is an engineer-led IT services company in Taiwan. The same team of engineers handles requirements interviews, development, deployment and ongoing maintenance, with no outsourcing or hand-offs, and the source code is delivered with the system.</p>
    <ul style="padding-left: 1.4em;">
      <li><strong>Custom Software Development</strong>: operations and warehouse management, e-approvals, Android field apps, Excel / PDF / Word reporting and system integration.</li>
      <li><strong>System Maintenance</strong>: bug fixes, enhancements and upgrades for existing systems, including poorly documented legacy systems, plus security hardening and server migration.</li>
      <li><strong>IT Consulting Services</strong>: architecture and technology selection, data cleanup, report automation, and server and deployment planning.</li>
    </ul>
    <p>The online assistant requires JavaScript. You are also welcome to contact us by email, LINE or phone above.</p>
  </section>
</noscript>`,
};

// 逐句替換：[中文原稿, 英文]；同一句出現多次會全部換掉
const PAIRS = [
  ['<html lang="zh-Hant">', '<html lang="en">'],
  ['<title>澄沐實業有限公司 — 客製化程式系統開發・程式系統維護・資訊專業服務</title>',
    '<title>ChengMu Industrial Co., Ltd. — Custom Software Development, System Maintenance &amp; IT Services</title>'],
  ['content="澄沐實業有限公司：工程師主導的軟體事務所，提供客製化程式系統開發、程式系統維護與資訊專業服務。需求訪談、開發、部署到維護，由同一組工程師負責到底。"',
    'content="ChengMu Industrial is an engineer-led software studio in Taiwan offering custom software development, system maintenance and IT consulting. The same engineers handle requirements, development, deployment and ongoing maintenance."'],
  ['content="澄沐實業有限公司 / CHENGMU INDUSTRIAL"', 'content="CHENGMU INDUSTRIAL CO., LTD."'],
  ['content="客製化程式系統開發・程式系統維護・資訊專業服務。"', 'content="Custom software development · System maintenance · IT consulting services."'],
  ['content="澄沐實業：客製化程式系統開發／程式系統維護／資訊專業服務"', 'content="ChengMu Industrial: custom software development / system maintenance / IT consulting services"'],
  ['<meta property="og:url" content="https://chengmu.com.tw/">', '<meta property="og:url" content="https://chengmu.com.tw/en/">'],
  ['<meta property="og:locale" content="zh_TW">', '<meta property="og:locale" content="en_US">\n<meta property="og:locale:alternate" content="zh_TW">'],
  ['<meta property="og:site_name" content="澄沐實業有限公司">', '<meta property="og:site_name" content="CHENGMU INDUSTRIAL CO., LTD.">'],
  ['<link rel="canonical" href="https://chengmu.com.tw/">', '<link rel="canonical" href="https://chengmu.com.tw/en/">'],
  // 英文版放在 /en/ 子目錄：相對路徑往上一層
  ['href="favicon.ico"', 'href="../favicon.ico"'],
  ['"assets/', '"../assets/'],
  // 版面文字
  ['<a class="lang" href="en/" hreflang="en" lang="en" data-lang="en">EN</a>', '<a class="lang" href="../" hreflang="zh-Hant" lang="zh-Hant" data-lang="zh">中文</a>'],
  ['alt="澄沐實業 logo"', 'alt="ChengMu Industrial logo"'],
  ['<h1>澄沐實業</h1>', '<h1>ChengMu Industrial</h1>'],
  ['<span class="ti">客製化程式系統開發<i>／</i></span><span class="ti">程式系統維護<i>／</i></span><span class="ti">資訊專業服務</span>',
    // 項目之間要留空格：英文沒有空格就不能斷行，三項會擠成一行撐破版面
    '<span class="ti">Custom Software Development<i>/</i></span> <span class="ti">System Maintenance<i>/</i></span> <span class="ti">IT Consulting Services</span>'],
  ['aria-label="服務諮詢"', 'aria-label="Service inquiry"'],
  ['placeholder="請輸入您的問題，例如：舊系統可以接手維護嗎？"', 'placeholder="Ask a question, e.g. Can you take over our legacy system?"'],
  ['aria-label="輸入問題"', 'aria-label="Type your question"'],
  ['aria-label="送出"', 'aria-label="Send"'],
  ['aria-label="LINE 官方帳號加入好友"', 'aria-label="Add our LINE official account"'],
  ['alt="LINE 官方帳號 @378vuckv QR code"', 'alt="LINE official account @378vuckv QR code"'],
  ['<span class="k">電話</span><a href="tel:0952159801">0952-159-801</a>', '<span class="k">PHONE</span><a href="tel:+886952159801">+886-952-159-801</a>'],
  ['© 2026 澄沐實業有限公司<span class="sep">・</span><a href="privacy">隱私說明</a>', '© 2026 CHENGMU INDUSTRIAL CO., LTD.<span class="sep">・</span><a href="privacy">Privacy</a>'],
  ['aria-label="觀看形象影片"', 'aria-label="Watch our company film"'],
  ['aria-label="澄沐實業形象影片"', 'aria-label="ChengMu Industrial company film"'],
  ['aria-label="播放影片"', 'aria-label="Play video"'],
  // 程式裡的字
  ["'Email：chengmu00082998@gmail.com／LINE：@378vuckv／電話：0952-159-801'", "'Email: chengmu00082998@gmail.com / LINE: @378vuckv / Phone: +886-952-159-801'"],
  ["var SUMMARY_HEAD = '需求摘要', SUMMARY_KEY = '【主要功能】', SUMMARY_TAIL = '【待確認事項】';",
    "var SUMMARY_HEAD = 'Requirements Summary', SUMMARY_KEY = '[Key Features]', SUMMARY_TAIL = '[Open Questions]';"],
  ["input.placeholder = '請輸入您的問題…';", "input.placeholder = 'Ask a question…';"],
  ["'複製摘要'", "'Copy summary'"],
  ["'已複製'", "'Copied'"],
  ["'請手動選取複製'", "'Please select and copy manually'"],
  ["'思考中…'", "'Thinking…'"],
  ["'服務暫時無法使用，請改用以下方式聯絡：'", "'The service is temporarily unavailable. Please contact us directly: '"],
  ["'目前無法連線到服務助理，請改用以下方式聯絡：'", "'Cannot reach the assistant right now. Please contact us directly: '"],
  // 會議白板建房密碼申請卡片
  ["'會議白板建房密碼申請'", "'Meeting Whiteboard Passcode Request'"],
  ["[['稱呼', card.name], ['公司／單位', card.org], ['Email', card.email], ['用途', card.purpose || '未提供']]",
    "[['Name', card.name], ['Organization', card.org], ['Email', card.email], ['Purpose', card.purpose || 'Not provided']]"],
  ["'確認寄出'", "'Confirm & send'"],
  ["'取消'", "'Cancel'"],
  ["'寄出中…'", "'Sending…'"],
  ["'驗證元件載入失敗，請重新整理頁面後再試。'", "'The verification widget failed to load. Please reload the page and try again.'"],
  ["'已取消申請。'", "'Request cancelled.'"],
];

let html = fs.readFileSync(SRC, 'utf8');
const errors = [];
for (const [name, repl] of Object.entries(BLOCKS)) {
  const re = new RegExp(`<!--${name}-->[\\s\\S]*?<!--/${name}-->`);
  if (!re.test(html)) errors.push(`找不到區塊 <!--${name}-->`);
  html = html.replace(re, repl);
}
for (const [zh, en] of PAIRS) {
  if (!html.includes(zh)) { errors.push(`原稿找不到：${zh}`); continue; }
  html = html.split(zh).join(en);
}
if (errors.length) {
  console.error('英文版產生失敗，index.html 跟對照表對不上：\n  ' + errors.join('\n  '));
  process.exit(1);
}
// 檢查：拿掉註解後還有中文的地方列出來（「中文」切換鈕與 JSON-LD 的公司中文名是刻意保留）
const visible = html.replace(/<!--[\s\S]*?-->/g, '').replace(/\/\*[\s\S]*?\*\//g, '')
  .split('\n').map((l) => l.replace(/(^|\s)\/\/.*$/, ''));
const left = visible.map((l, i) => [i + 1, l.trim()])
  .filter(([, l]) => /[一-鿿]/.test(l) && !l.includes('data-lang="zh">中文') && !l.includes('"alternateName": "澄沐實業有限公司"'));
fs.mkdirSync(path.dirname(OUT), {recursive: true});
fs.writeFileSync(OUT, html);
console.log(`→ en/index.html（${PAIRS.length} 句、${Object.keys(BLOCKS).length} 個區塊）`);
if (left.length) {
  console.log('還有中文的行（請確認是否要加進對照表）：');
  left.forEach(([n, l]) => console.log(`  ${n}: ${l.slice(0, 120)}`));
}
