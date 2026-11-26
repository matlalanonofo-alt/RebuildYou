/* RebuildYOU - animated connecting-dots background.
   Decorative only: sits behind all page content, ignores the mouse, and keeps clear of pictures. */
(function () {
  'use strict';
  if (!document.body || !window.requestAnimationFrame) return;

  var COLORS = ['#2563eb', '#6d28d9', '#0e7490', '#0b1650', '#22d3ee']; // blue, purple, teal, navy, cyan
  var KEEP_CLEAR = 'img, picture, video, iframe, [data-no-dots]';       // dots never show over these
  var PAD = 8;
  var reduceMotion = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;

  var canvas = document.createElement('canvas');
  canvas.className = 'ry-dots-canvas';
  canvas.setAttribute('aria-hidden', 'true');
  canvas.setAttribute('role', 'presentation');
  var st = canvas.style;
  st.position = 'fixed'; st.top = '0'; st.left = '0'; st.width = '100%'; st.height = '100%';
  st.zIndex = '-1'; st.pointerEvents = 'none'; st.display = 'block';
  document.body.appendChild(canvas);

  var ctx = canvas.getContext('2d');
  if (!ctx) return;

  var w = 0, h = 0, maxDist = 130, dots = [], last = 0, pending = false;

  function rand(a, b) { return a + Math.random() * (b - a); }

  function makeDot() {
    var angle = Math.random() * Math.PI * 2, speed = rand(8, 22); // pixels per second
    return {
      x: rand(0, w), y: rand(0, h),
      vx: Math.cos(angle) * speed, vy: Math.sin(angle) * speed,
      r: rand(1.4, 2.6), c: COLORS[(Math.random() * COLORS.length) | 0]
    };
  }

  function resize() {
    var nw = window.innerWidth, nh = window.innerHeight;
    var dpr = Math.min(window.devicePixelRatio || 1, 2);
    var small = nw < 576, i;
    canvas.width = Math.round(nw * dpr);
    canvas.height = Math.round(nh * dpr);
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    if (w && h) {
      for (i = 0; i < dots.length; i++) { dots[i].x *= nw / w; dots[i].y *= nh / h; }
    }
    w = nw; h = nh;
    maxDist = small ? 95 : 130;
    var target = Math.max(24, Math.min(small ? 45 : 110, Math.round((w * h) / (small ? 9000 : 13000))));
    while (dots.length < target) dots.push(makeDot());
    if (dots.length > target) dots.length = target;
  }

  function draw() {
    var i, j, a, b, dx, dy, d;
    ctx.clearRect(0, 0, w, h);

    ctx.lineWidth = 1;
    for (i = 0; i < dots.length; i++) {
      a = dots[i];
      for (j = i + 1; j < dots.length; j++) {
        b = dots[j]; dx = a.x - b.x; dy = a.y - b.y;
        if (dx > maxDist || dx < -maxDist || dy > maxDist || dy < -maxDist) continue;
        d = Math.sqrt(dx * dx + dy * dy);
        if (d < maxDist) {
          ctx.globalAlpha = (1 - d / maxDist) * 0.22;
          ctx.strokeStyle = a.c;
          ctx.beginPath(); ctx.moveTo(a.x, a.y); ctx.lineTo(b.x, b.y); ctx.stroke();
        }
      }
    }

    ctx.globalAlpha = 0.5;
    for (i = 0; i < dots.length; i++) {
      a = dots[i];
      ctx.fillStyle = a.c;
      ctx.beginPath(); ctx.arc(a.x, a.y, a.r, 0, Math.PI * 2); ctx.fill();
    }
    ctx.globalAlpha = 1;

    // Wipe the dots from any picture on screen (plus a small margin around it).
    var els = document.querySelectorAll(KEEP_CLEAR), n = Math.min(els.length, 80), r;
    for (i = 0; i < n; i++) {
      r = els[i].getBoundingClientRect();
      if (r.width < 4 || r.height < 4 || r.bottom < 0 || r.top > h || r.right < 0 || r.left > w) continue;
      ctx.clearRect(r.left - PAD, r.top - PAD, r.width + PAD * 2, r.height + PAD * 2);
    }
  }

  function step(t) {
    requestAnimationFrame(step);
    var dt = Math.min((t - last) / 1000, 0.05), i, a;
    last = t;
    for (i = 0; i < dots.length; i++) {
      a = dots[i];
      a.x += a.vx * dt; a.y += a.vy * dt;
      if (a.x < -10) a.x = w + 10; else if (a.x > w + 10) a.x = -10;
      if (a.y < -10) a.y = h + 10; else if (a.y > h + 10) a.y = -10;
    }
    draw();
  }

  function redrawSoon() {
    if (pending) return;
    pending = true;
    requestAnimationFrame(function () { pending = false; draw(); });
  }

  var resizeTimer;
  window.addEventListener('resize', function () {
    clearTimeout(resizeTimer);
    resizeTimer = setTimeout(function () { resize(); if (reduceMotion) draw(); }, 120);
  });

  resize();
  if (reduceMotion) {
    // Visitors who ask for less motion get still dots (redrawn so they stay clear of pictures).
    draw();
    window.addEventListener('scroll', redrawSoon, { passive: true });
    window.addEventListener('load', redrawSoon);
  } else {
    requestAnimationFrame(step);
  }
})();
