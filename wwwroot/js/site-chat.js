/* RebuildYOU FAQ assistant.
   Answers come from the FAQ data embedded in the page (see Views/Home/_FaqChat.cshtml and
   Services/SiteKnowledge.cs). Nothing the visitor types is sent anywhere or stored. */
(function () {
  'use strict';

  var root = document.getElementById('ryChat');
  var dataEl = document.getElementById('ry-chat-data');
  if (!root || !dataEl) return;

  var data;
  try { data = JSON.parse(dataEl.textContent); } catch (e) { return; }
  var faqs = data.faqs || [];

  var log = document.getElementById('ryChatLog');
  var chips = document.getElementById('ryChatChips');
  var form = document.getElementById('ryChatForm');
  var input = document.getElementById('ryChatInput');
  var restart = document.getElementById('ryChatRestart');
  if (!log || !chips || !form || !input) return;

  var DELAY = (window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches) ? 0 : 350;
  var START_IDS = ['programmes', 'fees', 'apply', 'cv-help', 'contact'];

  /* ---------- text helpers ---------- */
  var STOP = {};
  ('a an the and or but if of to in on at by for from with about as is are was were be been am do does did have has had ' +
   'can could will would should may might i me my mine we us our you your it its this that these those there here ' +
   'what which who whom when where why how please tell want need like just so very really also any some get got during into')
    .split(' ').forEach(function (w) { STOP[w] = 1; });

  function stem(w) {
    if (w.length > 4 && /ies$/.test(w)) return w.slice(0, -3) + 'y';
    if (w.length > 4 && /ied$/.test(w)) return w.slice(0, -3) + 'y';
    if (w.length > 5 && /ing$/.test(w)) return w.slice(0, -3);
    if (w.length > 4 && /ed$/.test(w)) return w.slice(0, -2);
    if (w.length > 3 && /s$/.test(w) && !/ss$/.test(w)) return w.slice(0, -1);
    return w;
  }
  function rawWords(text) { return String(text).toLowerCase().match(/[\p{L}\p{N}]+/gu) || []; }
  function tokens(text) { return rawWords(text).filter(function (w) { return !STOP[w]; }).map(stem); }
  function norm(text) { return ' ' + rawWords(text).map(stem).join(' ') + ' '; }
  function clip(text, n) { text = String(text); return text.length > n ? text.slice(0, n - 1) + '…' : text; }

  /* ---------- index the FAQ ---------- */
  var byId = {};
  faqs.forEach(function (f) {
    byId[f.id] = f;
    f._qt = tokens(f.q);
    f._single = [];
    f._phrases = [];
    var seen = {};   // "job" and "jobs" share a root, so each root is counted once per answer
    (f.k || []).forEach(function (k) {
      var rw = rawWords(k), key = norm(k);
      if (seen[key]) return;
      seen[key] = 1;
      if (rw.length > 1) f._phrases.push(key);
      else if (rw.length === 1 && !STOP[rw[0]]) f._single.push(stem(rw[0]));
    });
  });

  function scoreFaq(f, set, text) {
    var s = 0, i;
    for (i = 0; i < f._single.length; i++) if (set[f._single[i]]) s += 3;   // one keyword
    for (i = 0; i < f._phrases.length; i++) if (text.indexOf(f._phrases[i]) !== -1) s += 5; // a whole phrase
    for (i = 0; i < f._qt.length; i++) if (set[f._qt[i]]) s += 1;           // words from the FAQ question
    return s;
  }

  function rank(text) {
    var set = {};
    tokens(text).forEach(function (t) { set[t] = 1; });
    var n = norm(text);
    return faqs
      .map(function (f) { return { f: f, s: scoreFaq(f, set, n) }; })
      .filter(function (x) { return x.s >= 3; })
      .sort(function (a, b) { return b.s - a.s; });
  }

  /* ---------- small talk ---------- */
  function onlyThese(text, re) { return tokens(String(text).toLowerCase().replace(re, ' ')).length === 0; }
  var RE_GREET = /\b(hi|hello|hey|howzit|hallo|sawubona|molo|dumela|greetings|good morning|good afternoon|good evening|morning|afternoon|evening)\b/g;
  var RE_THANKS = /\b(thank you|thanks|thank|cheers|appreciate it|appreciate|very much|so much|a lot|much)\b/g;
  var RE_BYE = /\b(bye|goodbye|see you|see ya|take care|later)\b/g;
  var RE_WHO = /\b(who|what) are you\b|\bare you (a |an )?(bot|robot|human|real|person|ai)\b|\byour name\b/;
  var RE_HUMAN = /\b(speak|talk|chat|connect) (to|with) (a |an |the |some)?(someone|somebody|person|human|agent|staff|team|representative|consultant|advisor|adviser|manager|real person)\b|\b(human|agent|representative|consultant|advisor|real person|live chat|call me|call back|callback|phone me|ring me)\b/;

  /* ---------- rendering ---------- */
  function addMsg(kind, text, links, tag) {
    var wrap = document.createElement('div');
    wrap.className = 'ry-msg ry-msg-' + kind;
    if (tag) wrap.setAttribute('data-faq', tag);
    var bubble = document.createElement('div');
    bubble.className = 'ry-bubble';
    bubble.textContent = text;
    wrap.appendChild(bubble);
    if (links && links.length) {
      var row = document.createElement('div');
      row.className = 'ry-msg-links';
      links.forEach(function (l) {
        if (!l || !/^(\/|#|https?:|tel:|mailto:)/i.test(l.u)) return;
        var a = document.createElement('a');
        a.className = 'btn btn-ry-outline btn-sm';
        a.href = l.u;
        a.textContent = l.t;
        if (/^https?:/i.test(l.u)) { a.target = '_blank'; a.rel = 'noopener noreferrer'; }
        row.appendChild(a);
      });
      if (row.childNodes.length) wrap.appendChild(row);
    }
    log.appendChild(wrap);
    log.scrollTop = log.scrollHeight;
  }

  function showChips(list) {
    chips.textContent = '';
    list.slice(0, 4).forEach(function (f) {
      var b = document.createElement('button');
      b.type = 'button';
      b.className = 'ry-qchip';
      b.textContent = f.q;
      b.addEventListener('click', function () { send(f.q, f); });
      chips.appendChild(b);
    });
  }

  function startChips(excludeId) {
    return START_IDS.filter(function (id) { return id !== excludeId && byId[id]; }).map(function (id) { return byId[id]; });
  }

  function whatsappLink(text) {
    var base = data.whatsapp || '';
    if (!/^https:\/\//i.test(base)) return '';
    return base + (base.indexOf('?') === -1 ? '?' : '&') + 'text=' + encodeURIComponent('Hi RebuildYOU, I have a question: ' + clip(text, 200));
  }

  function contactLinks() { return byId.contact ? byId.contact.links : []; }

  /* ---------- answering ---------- */
  function answerFaq(f) {
    addMsg('bot', f.a, f.links, f.id);
    showChips(startChips(f.id));
  }

  function fallback(text) {
    var links = [];
    if (data.searchUrl) links.push({ t: 'Search the website', u: data.searchUrl + '?q=' + encodeURIComponent(clip(text, 80)) });
    var wa = whatsappLink(text);
    if (wa) links.push({ t: 'Ask the team on WhatsApp', u: wa });
    if (data.contactUrl) links.push({ t: 'Send an enquiry', u: data.contactUrl });
    addMsg('bot', 'I could not find a clear answer to that on the website. I can help with our programmes, applying for opportunities, your account and how to reach us. For anything else, you can search the site or ask the team directly.', links, 'fallback');
    showChips(startChips());
  }

  function respond(text) {
    if (RE_WHO.test(text.toLowerCase())) {
      addMsg('bot', 'I am the RebuildYOU website assistant, an automated helper that answers common questions using the information on this website. For anything personal or urgent, please contact the team.', contactLinks(), 'smalltalk-who');
      showChips(startChips()); return;
    }
    var lower = text.toLowerCase();
    if (RE_GREET.test(lower) && onlyThese(lower, RE_GREET)) {
      RE_GREET.lastIndex = 0;
      addMsg('bot', 'Hello! I can answer questions about our training programmes, applying for opportunities, your account and how to reach us. What would you like to know?', [], 'smalltalk-greet');
      showChips(startChips()); return;
    }
    RE_GREET.lastIndex = 0;
    if (RE_THANKS.test(lower) && onlyThese(lower, RE_THANKS)) {
      RE_THANKS.lastIndex = 0;
      addMsg('bot', 'You are welcome! Is there anything else I can help with?', [], 'smalltalk-thanks');
      showChips(startChips()); return;
    }
    RE_THANKS.lastIndex = 0;
    if (RE_BYE.test(lower) && onlyThese(lower, RE_BYE)) {
      RE_BYE.lastIndex = 0;
      addMsg('bot', 'Goodbye, and good luck with your next step. You can come back to ask a question at any time.', [], 'smalltalk-bye');
      showChips(startChips()); return;
    }
    RE_BYE.lastIndex = 0;
    if (RE_HUMAN.test(lower)) {
      addMsg('bot', 'Of course. The team can help with anything I cannot. You can reach them here:', contactLinks(), 'smalltalk-human');
      showChips(startChips('contact')); return;
    }
    var ranked = rank(text);
    if (!ranked.length) { fallback(text); return; }
    var top = ranked[0].f;
    addMsg('bot', top.a, top.links, top.id);
    var related = ranked.slice(1).map(function (x) { return x.f; });
    startChips(top.id).forEach(function (f) { if (related.indexOf(f) === -1) related.push(f); });
    showChips(related);
  }

  function send(text, direct) {
    addMsg('user', text);
    setTimeout(function () { if (direct) answerFaq(direct); else respond(text); }, DELAY);
  }

  function welcome() {
    log.textContent = '';
    addMsg('bot', 'Hi! I am the RebuildYOU assistant. I can answer common questions about the training centre, our programmes, applying for opportunities and your account. Pick a question below or type your own.', [], 'welcome');
    showChips(startChips());
  }

  form.addEventListener('submit', function (e) {
    e.preventDefault();
    var text = input.value.replace(/\s+/g, ' ').trim();
    if (!text) return;
    input.value = '';
    send(clip(text, 200));
    input.focus();
  });
  if (restart) restart.addEventListener('click', function () { welcome(); input.focus(); });

  root.hidden = false;
  welcome();

  /* Open the right FAQ item when arriving from a search result (e.g. /Home/About#faq-fees). */
  window.addEventListener('load', function () {
    var h = window.location.hash;
    if (h.indexOf('#faq-') !== 0) return;
    var item = document.getElementById(h.slice(1));
    if (!item) return;
    var body = item.querySelector('.accordion-collapse');
    if (body && window.bootstrap && window.bootstrap.Collapse) {
      window.bootstrap.Collapse.getOrCreateInstance(body, { toggle: false }).show();
    }
    item.scrollIntoView();
  });
})();
