/* Rebuild You Training Centre – website assistant widget
   Drop-in: <script src="chatbot.js" defer></script>
   Set CHAT_ENDPOINT to your deployed worker URL to enable the AI.
   Left empty, the widget still works using built-in answers. */
(function () {
  'use strict';

  var CHAT_ENDPOINT = ''; // e.g. 'https://rebuildyou-chat.YOURNAME.workers.dev'
  var WHATSAPP = 'https://wa.me/27656001164';

  var GREETING = 'Hi, I’m the Rebuild You assistant. Ask me about our training, registering, careers or how to reach us.';
  var SUGGESTIONS = ['What training do you offer?', 'How do I register?', 'Do you help with CVs?', 'Where are you based?'];

  /* ---------- built-in answers (used when no endpoint is set or the AI is unreachable) ---------- */
  var FAQ = [
    { k: ['register', 'enrol', 'enroll', 'sign up', 'apply for', 'join'],
      a: 'To register, use the “Register now” button on this page or send us a message in the contact form. You can also WhatsApp us on +27 65 600 1164 and we’ll guide you through the next steps.' },
    { k: ['corporate', 'customer service', 'call centre', 'call center', 'soft skill', 'conduct'],
      a: 'Our corporate training covers customer service, call centre skills, workplace communication, and professional conduct and soft skills. Programmes can be tailored to your organisation.' },
    { k: ['computer', 'admin', 'reception', 'hospitality', 'skills development'],
      a: 'Our skills development programmes cover computer skills, administration and reception, and hospitality training.' },
    { k: ['cv', 'interview', 'job application', 'employment readiness', 'resume'],
      a: 'Our employment readiness programme covers CV development, interview preparation, job application support and workplace communication.' },
    { k: ['consult', 'strategy', 'gap analysis', 'advisory', 'programme design', 'program design'],
      a: 'Our consulting services include workforce development strategy, programme design, skills gap analysis and training advisory.' },
    { k: ['training', 'course', 'programme', 'program', 'offer', 'skills'],
      a: 'We offer four areas: corporate training, skills development, employment readiness, and consulting services. Tell me which one interests you and I’ll share what’s covered.' },
    { k: ['job', 'vacanc', 'career', 'upload'],
      a: 'Job seekers can browse vacancies and upload a CV on our Careers page: jobs.html. Applications are sent directly by email.' },
    { k: ['where', 'address', 'location', 'based', 'visit', 'roodepoort'],
      a: 'We’re at 2 Anderson Street, Davidsonville, Roodepoort, Johannesburg, 1724, South Africa.' },
    { k: ['phone', 'call', 'number', 'contact', 'email', 'reach', 'whatsapp'],
      a: 'You can call or WhatsApp +27 65 600 1164, email lereases@gmail.com, or use the contact form on this page. We’re on Instagram at @rebuildyou_trainingcentre.' },
    { k: ['who', 'ceo', 'founder', 'lerease'],
      a: 'Rebuild You was founded by Lerease Sam, our CEO. She leads strategy, partnerships and programme development.' },
    { k: ['about', 'vision', 'mission', 'value'],
      a: 'Rebuild You Training Centre is a South African skills development organisation focused on practical, industry-aligned training that leads to employability. See the About section for our vision, mission and values.' },
    { k: ['price', 'cost', 'fee', 'how much', 'quote', 'duration', 'how long', 'schedule', 'dates'],
      a: 'Fees, durations and start dates depend on the programme. Please contact us on +27 65 600 1164 or lereases@gmail.com and we’ll send you the details.' },
    { k: ['hello', 'hi', 'hey', 'good morning', 'good afternoon'],
      a: 'Hello! What would you like to know about Rebuild You Training Centre?' }
  ];
  var FALLBACK = 'I’m not sure about that one. Our team can help directly: call or WhatsApp +27 65 600 1164 or email lereases@gmail.com.';

  function localAnswer(text) {
    var t = ' ' + text.toLowerCase() + ' ';
    for (var i = 0; i < FAQ.length; i++) {
      for (var j = 0; j < FAQ[i].k.length; j++) {
        if (t.indexOf(FAQ[i].k[j]) !== -1) return FAQ[i].a;
      }
    }
    return FALLBACK;
  }

  /* ---------- styles ---------- */
  var css = [
    '#ry-chat,#ry-chat *{box-sizing:border-box}',
    '#ry-chat{--ry-navy:var(--navy,#12305a);--ry-accent:var(--teal,#0e8f86);--ry-bg:#fff;--ry-ink:#1c2430;--ry-soft:#eef2f7;',
    'position:fixed;left:18px;bottom:18px;z-index:9999;font:400 15px/1.45 "Instrument Sans",system-ui,sans-serif;color:var(--ry-ink)}',
    '#ry-launch{display:flex;align-items:center;gap:10px;border:0;cursor:pointer;background:var(--ry-navy);color:#fff;',
    'padding:12px 18px 12px 14px;border-radius:999px;font:600 15px "Instrument Sans",system-ui,sans-serif;box-shadow:0 6px 20px rgba(10,25,50,.28)}',
    '#ry-launch svg{width:22px;height:22px;fill:#fff}',
    '#ry-launch:focus-visible,#ry-close:focus-visible,#ry-send:focus-visible,.ry-chip:focus-visible,#ry-in:focus-visible{outline:3px solid #f2b705;outline-offset:2px}',
    '#ry-panel{position:absolute;left:0;bottom:62px;width:360px;max-width:calc(100vw - 36px);height:520px;max-height:calc(100vh - 110px);',
    'background:var(--ry-bg);border-radius:16px;box-shadow:0 14px 44px rgba(10,25,50,.32);display:none;flex-direction:column;overflow:hidden}',
    '#ry-chat.open #ry-panel{display:flex;animation:ry-pop .18s ease-out}',
    '#ry-chat.open #ry-launch{display:none}',
    '#ry-panel{bottom:0}',
    '@keyframes ry-pop{from{opacity:0;transform:translateY(8px)}to{opacity:1;transform:none}}',
    '.ry-head{display:flex;align-items:center;gap:10px;background:var(--ry-navy);color:#fff;padding:14px 14px 14px 16px}',
    '.ry-head b{font:700 16px "Bricolage Grotesque",system-ui,sans-serif;display:block}',
    '.ry-head small{opacity:.8;font-size:12px}',
    '.ry-head div{flex:1}',
    '#ry-close{background:transparent;border:0;color:#fff;cursor:pointer;font-size:26px;line-height:1;padding:2px 8px;border-radius:8px}',
    '#ry-log{flex:1;overflow-y:auto;padding:14px;display:flex;flex-direction:column;gap:10px;background:var(--ry-soft)}',
    '.ry-m{max-width:86%;padding:10px 13px;border-radius:14px;white-space:pre-wrap;word-wrap:break-word}',
    '.ry-m a{color:inherit;text-decoration:underline}',
    '.ry-bot{background:#fff;align-self:flex-start;border-bottom-left-radius:4px}',
    '.ry-me{background:var(--ry-navy);color:#fff;align-self:flex-end;border-bottom-right-radius:4px}',
    '.ry-chips{display:flex;flex-wrap:wrap;gap:6px;margin-top:2px}',
    '.ry-chip{border:1px solid var(--ry-navy);background:#fff;color:var(--ry-navy);border-radius:999px;padding:6px 12px;cursor:pointer;font:500 13px "Instrument Sans",system-ui,sans-serif}',
    '.ry-chip:hover{background:var(--ry-navy);color:#fff}',
    '.ry-typing{display:flex;gap:4px;padding:13px}',
    '.ry-typing i{width:7px;height:7px;border-radius:50%;background:#8a97a8;animation:ry-b 1s infinite}',
    '.ry-typing i:nth-child(2){animation-delay:.15s}.ry-typing i:nth-child(3){animation-delay:.3s}',
    '@keyframes ry-b{0%,60%,100%{opacity:.3}30%{opacity:1}}',
    '.ry-foot{display:flex;gap:8px;padding:10px;border-top:1px solid #dde3ec;background:#fff}',
    '#ry-in{flex:1;border:1px solid #c5cedb;border-radius:10px;padding:10px 12px;font:inherit;min-width:0}',
    '#ry-send{border:0;background:var(--ry-accent);color:#fff;border-radius:10px;padding:0 16px;font:600 14px "Instrument Sans",system-ui,sans-serif;cursor:pointer}',
    '#ry-send:disabled{opacity:.5;cursor:default}',
    '.ry-note{font-size:11.5px;color:#5d6b7e;padding:0 12px 9px;background:#fff}',
    '.ry-note a{color:inherit}',
    '@media (max-width:480px){#ry-chat{left:12px;bottom:12px}#ry-panel{width:calc(100vw - 24px);height:calc(100vh - 90px)}}',
    '@media (prefers-reduced-motion:reduce){#ry-chat.open #ry-panel,.ry-typing i{animation:none}}'
  ].join('');

  var style = document.createElement('style');
  style.textContent = css;
  document.head.appendChild(style);

  /* ---------- markup ---------- */
  var root = document.createElement('div');
  root.id = 'ry-chat';
  root.innerHTML =
    '<button id="ry-launch" type="button" aria-expanded="false" aria-controls="ry-panel">' +
      '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M4 3h16a2 2 0 0 1 2 2v11a2 2 0 0 1-2 2H9l-5 4v-4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2z"/></svg>Ask us</button>' +
    '<section id="ry-panel" role="dialog" aria-label="Rebuild You assistant">' +
      '<div class="ry-head"><div><b>Rebuild You assistant</b><small>Answers about training, careers and contact details</small></div>' +
      '<button id="ry-close" type="button" aria-label="Close chat">&times;</button></div>' +
      '<div id="ry-log" aria-live="polite"></div>' +
      '<div class="ry-foot"><input id="ry-in" type="text" maxlength="500" placeholder="Type your question" aria-label="Your question" autocomplete="off">' +
      '<button id="ry-send" type="button">Send</button></div>' +
      '<div class="ry-note">Automated assistant. For anything urgent, <a href="' + WHATSAPP + '" target="_blank" rel="noopener">WhatsApp us</a>.</div>' +
    '</section>';
  document.body.appendChild(root);

  var launch = root.querySelector('#ry-launch');
  var closeBtn = root.querySelector('#ry-close');
  var log = root.querySelector('#ry-log');
  var input = root.querySelector('#ry-in');
  var send = root.querySelector('#ry-send');

  var history = []; // {role, content}
  var busy = false;
  var started = false;

  function escapeHtml(s) {
    return s.replace(/[&<>"']/g, function (c) {
      return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
    });
  }
  // Safe formatting: escape first, then link URLs, emails and phone numbers.
  function format(s) {
    var h = escapeHtml(s);
    h = h.replace(/(https?:\/\/[^\s<]+[^\s<.,;:!?)])/g, '<a href="$1" target="_blank" rel="noopener">$1</a>');
    h = h.replace(/([A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,})/g, '<a href="mailto:$1">$1</a>');
    h = h.replace(/\b((?:jobs|skills|about)\.html(?:#[\w-]+)?)/g, '<a href="$1">$1</a>');
    return h;
  }

  function addMsg(text, who) {
    var d = document.createElement('div');
    d.className = 'ry-m ' + (who === 'me' ? 'ry-me' : 'ry-bot');
    if (who === 'me') d.textContent = text; else d.innerHTML = format(text);
    log.appendChild(d);
    log.scrollTop = log.scrollHeight;
    return d;
  }

  function showChips() {
    var w = document.createElement('div');
    w.className = 'ry-chips';
    SUGGESTIONS.forEach(function (q) {
      var b = document.createElement('button');
      b.type = 'button';
      b.className = 'ry-chip';
      b.textContent = q;
      b.addEventListener('click', function () { w.remove(); ask(q); });
      w.appendChild(b);
    });
    log.appendChild(w);
  }

  function open() {
    root.classList.add('open');
    launch.setAttribute('aria-expanded', 'true');
    if (!started) {
      started = true;
      addMsg(GREETING, 'bot');
      showChips();
    }
    setTimeout(function () { input.focus(); }, 50);
  }
  function close() {
    root.classList.remove('open');
    launch.setAttribute('aria-expanded', 'false');
    launch.focus();
  }

  function typing() {
    var d = document.createElement('div');
    d.className = 'ry-m ry-bot ry-typing';
    d.setAttribute('aria-label', 'Assistant is typing');
    d.innerHTML = '<i></i><i></i><i></i>';
    log.appendChild(d);
    log.scrollTop = log.scrollHeight;
    return d;
  }

  async function fetchAI() {
    var ctl = new AbortController();
    var timer = setTimeout(function () { ctl.abort(); }, 25000);
    try {
      var r = await fetch(CHAT_ENDPOINT, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ messages: history.slice(-12) }),
        signal: ctl.signal
      });
      if (!r.ok) throw new Error('HTTP ' + r.status);
      var data = await r.json();
      if (!data.reply) throw new Error('empty');
      return data.reply;
    } finally {
      clearTimeout(timer);
    }
  }

  async function ask(text) {
    text = (text || '').trim();
    if (!text || busy) return;
    busy = true;
    send.disabled = true;
    input.value = '';
    addMsg(text, 'me');
    history.push({ role: 'user', content: text });
    var t = typing();
    var reply;
    try {
      reply = CHAT_ENDPOINT ? await fetchAI() : (await new Promise(function (res) { setTimeout(res, 500); }), localAnswer(text));
    } catch (e) {
      reply = localAnswer(text);
    }
    t.remove();
    addMsg(reply, 'bot');
    history.push({ role: 'assistant', content: reply });
    busy = false;
    send.disabled = false;
    input.focus();
  }

  launch.addEventListener('click', open);
  closeBtn.addEventListener('click', close);
  send.addEventListener('click', function () { ask(input.value); });
  input.addEventListener('keydown', function (e) {
    if (e.key === 'Enter') { e.preventDefault(); ask(input.value); }
  });
  root.addEventListener('keydown', function (e) {
    if (e.key === 'Escape' && root.classList.contains('open')) close();
  });
})();

