// Логіка чату: вхід, обмін Y через хаб, попарні ключі, шифрування повідомлень
import { fromHex, toHex, generateKeys, sharedKey, fingerprint, aesKey, encrypt, decrypt } from './dh.js';

const $ = (id) => document.getElementById(id);
const state = { mode: 'big', q: 0n, a: 0n, x: 0n, y: 0n, showSecret: false, peers: new Map() };
let hub;

// Формат числа: у навчальному режимі десяткове, інакше скорочений hex
function fmt(n) {
  if (state.mode === 'small' || n < 1000n) return n.toString();
  const h = toHex(n);
  return h.length <= 24 ? '0x' + h : `0x${h.slice(0, 10)}…${h.slice(-10)}`;
}
function num(n) {
  const s = document.createElement('span');
  s.className = 'num';
  s.textContent = fmt(n);
  s.title = 'Натисніть, щоб показати повністю';
  s.onclick = () => { s.textContent = s.classList.toggle('full') && n >= 1000n ? '0x' + toHex(n) : fmt(n); };
  return s;
}
const el = (tag, cls, text) => { const e = document.createElement(tag); if (cls) e.className = cls; if (text != null) e.textContent = text; return e; };
const fpColor = (fp) => `hsl(${parseInt(fp.slice(0, 3), 16) % 360} 70% 88%)`;

// ---- Вхід ----
$('joinForm').onsubmit = async (ev) => {
  ev.preventDefault();
  $('joinError').textContent = '';
  try {
    state.mode = $('mode').value;
    const groups = await (await fetch('/api/groups')).json();
    const g = groups[state.mode];
    state.q = fromHex(g.q);
    state.a = fromHex(g.a);
    Object.assign(state, generateKeys(state.q, state.a)); // X і Y генеруються лише в браузері

    hub = new signalR.HubConnectionBuilder().withUrl('/chat').build();
    // події обробляються по черзі, щоб ключі були готові до розшифрування
    let queue = Promise.resolve();
    const serial = (fn) => (...args) => { queue = queue.then(() => fn(...args)).catch(console.error); };
    hub.on('Members', serial(onMembers));
    hub.on('Message', serial(onMessage));
    hub.on('Wire', onWire);
    hub.on('System', (t) => addMsg('system', null, t));
    hub.onclose(() => addMsg('system', null, "З'єднання з сервером втрачено"));
    await hub.start();
    await hub.invoke('Join', $('room').value, $('name').value, state.mode, toHex(state.y));

    $('roomTitle').textContent = 'Кімната «' + $('room').value.trim() + '»';
    $('who').textContent = '· ви: ' + $('name').value.trim();
    $('modeBadge').textContent = g.name + ' · ' + g.bits + ' біт';
    $('join').hidden = true;
    $('app').hidden = false;
    renderKeys();
    $('text').focus();
  } catch (e) {
    $('joinError').textContent = 'Помилка: ' + (e.message || e);
  }
};

// ---- Список учасників і обчислення K ----
async function onMembers(list, mitm) {
  $('mitm').checked = mitm;
  $('mitmNote').hidden = !mitm;
  const next = new Map();
  for (const p of list) {
    const y = fromHex(p.y);
    const old = state.peers.get(p.id);
    if (old && old.y === y) { next.set(p.id, old); continue; }
    const k = sharedKey(y, state.x, state.q); // K = Y_peer ^ X mod Q
    next.set(p.id, { name: p.name, y, k, fp: await fingerprint(k), key: await aesKey(k) });
  }
  state.peers = next;
  renderKeys();
}

function renderKeys() {
  $('q').replaceChildren(num(state.q));
  $('a').replaceChildren(num(state.a));
  $('x').replaceChildren(state.showSecret ? num(state.x) : '••••••');
  $('y').replaceChildren(num(state.y));
  const ul = $('peers');
  ul.replaceChildren();
  if (!state.peers.size) ul.append(el('li', 'muted small', 'Поки нікого немає. Відкрийте ще одну вкладку.'));
  for (const p of state.peers.values()) {
    const li = el('li', 'peer');
    const head = el('div', 'peer-head');
    const fp = el('span', 'fp', p.fp);
    fp.style.background = fpColor(p.fp);
    head.append(el('b', null, p.name), fp);
    const yRow = el('div', 'small', 'Y = ');
    yRow.append(num(p.y));
    const kRow = el('div', 'small', 'K = ');
    if (state.showSecret) {
      if (state.mode === 'small') kRow.textContent = `K = ${p.y}^${state.x} mod ${state.q} = ${p.k}`;
      else kRow.append(num(p.k));
    } else kRow.append('••••••');
    li.append(head, yRow, kRow);
    ul.append(li);
  }
}

$('eye').onclick = () => { state.showSecret = !state.showSecret; renderKeys(); };
$('mitm').onchange = (e) => hub.invoke('SetMitm', e.target.checked);

// ---- Повідомлення ----
$('sendForm').onsubmit = async (ev) => {
  ev.preventDefault();
  const text = $('text').value.trim();
  if (!text) return;
  if (!state.peers.size) { addMsg('system', null, 'У кімнаті немає отримувачів'); return; }
  // окремий шифротекст для кожного отримувача своїм ключем K_ij
  const items = [];
  for (const [id, p] of state.peers) items.push({ to: id, ...(await encrypt(p.key, text)) });
  await hub.invoke('Send', items);
  addMsg('me', 'Ви', text);
  $('text').value = '';
};

async function onMessage(m) {
  const p = state.peers.get(m.from);
  try {
    addMsg('in', m.fromName, await decrypt(p.key, m.iv, m.ct));
  } catch {
    addMsg('bad', m.fromName, 'Не вдалося розшифрувати (ключі не збігаються)');
  }
}

function addMsg(kind, from, text) {
  const li = el('li', 'msg ' + kind);
  if (from) li.append(el('div', 'from', from));
  li.append(el('div', 'body', text));
  li.append(el('time', null, new Date().toLocaleTimeString('uk-UA', { hour: '2-digit', minute: '2-digit' })));
  $('messages').append(li);
  li.scrollIntoView({ block: 'end' });
}

// ---- Журнал сервера ----
function onWire(w) {
  const li = el('li', 'wire-item' + (w.stolen != null ? ' stolen' : ''));
  li.append(el('div', 'small', `${w.from} → ${w.to}`));
  li.append(el('code', null, 'iv: ' + w.iv));
  li.append(el('code', null, 'ct: ' + w.ct));
  if (w.stolen != null) li.append(el('div', 'small', 'Сервер розшифрував: «' + w.stolen + '»'));
  $('wire').prepend(li);
  while ($('wire').children.length > 50) $('wire').lastChild.remove();
}
