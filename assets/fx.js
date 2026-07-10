/* ============================================================
   ChengMu 官網 — 漸進增強特效 (Engineering Dossier FX)
   ------------------------------------------------------------
   設計語言：所有動作都必須是「製圖機 / 量測儀會做的動作」。
   - 全程 no-JS 可讀：CSS 只在 <html class="fx-ready"> 時才套用初始隱藏態。
   - 尊重 prefers-reduced-motion：關閉時直接顯示終態、不掛游標特效。
   - 純 Vanilla、無依賴、只動 transform / opacity / clip-path。
   - 作用於 4 頁共用結構，無需改動個別頁面 HTML。
   ============================================================ */
(function () {
  'use strict';

  var doc = document;
  var root = doc.documentElement;
  var reduceMotion = window.matchMedia &&
    window.matchMedia('(prefers-reduced-motion: reduce)').matches;

  // 標記 JS 就緒（CSS 據此才隱藏初始態 → 保證 no-JS 全內容可見）
  root.classList.add('fx-ready');
  if (reduceMotion) root.classList.add('fx-reduced');

  function ready(fn) {
    if (doc.readyState !== 'loading') fn();
    else doc.addEventListener('DOMContentLoaded', fn);
  }

  ready(function () {
    setupReveals();
    setupHero();
    if (window.matchMedia && window.matchMedia('(pointer: fine)').matches) {
      setupCursor();
    }
  });

  /* ── 進場揭示：datum 校準 / 章節線落筆 / 逐列點亮 ──────────── */
  function setupReveals() {
    var rows = [].slice.call(doc.querySelectorAll('section .row, header.hero'));

    // 為可 stagger 的子元素標記序號（逐列印出感，上限 9 避免拖太長）
    rows.forEach(function (row) {
      var items = row.querySelectorAll(
        '.specs .spec, .proc .proc-step, .faq .faq-item, table.ds tbody tr'
      );
      for (var i = 0; i < items.length; i++) {
        items[i].style.setProperty('--i', Math.min(i, 9));
      }
    });

    if (reduceMotion || !('IntersectionObserver' in window)) {
      rows.forEach(function (r) { r.classList.add('is-visible'); });
      return;
    }

    var io = new IntersectionObserver(function (entries) {
      entries.forEach(function (e) {
        if (e.isIntersecting) {
          e.target.classList.add('is-visible');
          io.unobserve(e.target);   // 一次性：不回放、不每幀計算
        }
      });
    }, { rootMargin: '0px 0px -12% 0px', threshold: 0.12 });

    rows.forEach(function (r) { io.observe(r); });
  }

  /* ── Hero 招牌時刻：出圖 plot-in + 尺寸線自量 + 數字回穩 ─────── */
  function setupHero() {
    var hero = doc.querySelector('header.hero');
    if (!hero) return;

    // 注入四角製圖定位角標 + 基準靶（裝飾、aria-hidden，含游標十字容器）
    var frame = doc.createElement('div');
    frame.className = 'hero-frame';
    frame.setAttribute('aria-hidden', 'true');
    frame.innerHTML =
      '<span class="cm tl"></span><span class="cm tr"></span>' +
      '<span class="cm bl"></span><span class="cm br"></span>' +
      '<span class="hf-datum"></span>';
    hero.insertBefore(frame, hero.firstChild);

    if (reduceMotion) {
      hero.classList.add('is-visible', 'hero-in');
      return; // 數字已是 HTML 終值，不需 count-up
    }

    // 觸發載入序列（雙 rAF 確保初始態先上畫面）
    requestAnimationFrame(function () {
      requestAnimationFrame(function () { hero.classList.add('hero-in'); });
    });

    // 尺寸線畫完後，儀器讀值回穩
    window.setTimeout(function () { countUp(hero); }, 1000);
  }

  /* ── 數字回穩 count-up（僅 hero 的 .est b 與 .dim span 內數字）── */
  function countUp(scope) {
    var targets = [];
    scope.querySelectorAll('.est b').forEach(function (el) {
      targets.push({ el: el, wrap: false });
    });
    var dimSpan = scope.querySelector('.dim span');
    if (dimSpan) targets.push({ el: dimSpan, wrap: true });

    targets.forEach(function (t) { animateNumber(t.el, t.wrap); });
  }

  function animateNumber(el, wrap) {
    var text = el.textContent;
    var m = text.match(/\d[\d,]*/);
    if (!m) return;                                  // 無數字（如首頁 .dim）→ 跳過
    var target = parseInt(m[0].replace(/,/g, ''), 10);
    if (!isFinite(target)) return;
    var hasComma = m[0].indexOf(',') > -1;
    var fmt = function (n) {
      return hasComma ? n.toLocaleString('en-US') : String(n);
    };

    var span;
    if (wrap) {
      var before = text.slice(0, m.index);
      var after = text.slice(m.index + m[0].length);
      el.textContent = '';
      el.appendChild(doc.createTextNode(before));
      span = doc.createElement('span');
      span.className = 'cu';
      el.appendChild(span);
      el.appendChild(doc.createTextNode(after));
    } else {
      span = el;                                     // .est b 整格就是數字
    }

    var dur = 780, start = null;
    function step(ts) {
      if (start === null) start = ts;
      var p = Math.min((ts - start) / dur, 1);
      var eased = 1 - Math.pow(1 - p, 3);            // easeOutCubic：儀表回穩
      span.textContent = fmt(Math.round(target * eased));
      if (p < 1) requestAnimationFrame(step);
      else span.textContent = fmt(target);
    }
    requestAnimationFrame(step);
  }

  /* ── 全站 CAD 十字游標：整頁跟著指標，像在圖紙上量測（僅 pointer:fine）──
     全視窗 fixed、pointer-events:none 不擋操作、壓在 nav 之下；
     只動 transform、rAF throttle；指標離開視窗即淡出。 */
  function setupCursor() {
    var wrap = doc.createElement('div');
    wrap.className = 'cad-cursor';
    wrap.setAttribute('aria-hidden', 'true');
    wrap.innerHTML = '<span class="cad-x"></span><span class="cad-y"></span>';
    doc.body.appendChild(wrap);

    var vx = wrap.querySelector('.cad-x');   // 垂直線（跟指標 X）
    var hy = wrap.querySelector('.cad-y');   // 水平線（跟指標 Y）
    var raf = 0, px = 0, py = 0;

    function apply() {
      raf = 0;
      vx.style.transform = 'translateX(' + px + 'px)';
      hy.style.transform = 'translateY(' + py + 'px)';
    }
    window.addEventListener('mousemove', function (ev) {
      px = ev.clientX; py = ev.clientY;
      if (!wrap.classList.contains('on')) wrap.classList.add('on');
      if (!raf) raf = requestAnimationFrame(apply);
    }, { passive: true });
    // 指標離開視窗 / 分頁失焦 → 淡出
    doc.addEventListener('mouseleave', function () { wrap.classList.remove('on'); });
    window.addEventListener('blur', function () { wrap.classList.remove('on'); });
  }

})();
