// app.js — логіка інтерфейсу: вкладки, виклики API, обмін з сервером.
"use strict";

const $ = (id) => document.getElementById(id);
const esc = (s) => String(s).replace(/[&<>"]/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" }[c]));

// Поточна ключова пара з вкладки 1 (hex-рядки) — використовується у вкладці 2
let currentKey = null;
// Стан обміну: ключі клієнта (BigInt) та відкритий ключ сервера
const ex = { client: null, server: null };

// ---- Вкладки ----
document.querySelectorAll(".tab").forEach((btn) => btn.addEventListener("click", () => {
  document.querySelectorAll(".tab, .panel").forEach((el) => el.classList.remove("active"));
  btn.classList.add("active");
  $(btn.dataset.tab).classList.add("active");
}));

// POST/GET JSON з обробкою помилок сервера
async function api(url, body) {
  const res = await fetch(url, body === undefined ? {} : {
    method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(body),
  });
  const data = await res.json();
  if (!res.ok) throw new Error(data.error || res.statusText);
  return data;
}

// Блокування кнопки на час операції
async function busy(btn, fn) {
  btn.disabled = true;
  try { await fn(); } catch (e) { alert("Помилка: " + e.message); } finally { btn.disabled = false; }
}

// ---- 1. Генерація ключів ----
function renderKey() {
  if (!currentKey) return;
  const fmt = $("keyFormat").value, k = currentKey;
  const rows = [
    ["P", "просте"], ["Q", "просте"], ["N", "P·Q"], ["Phi", "(P−1)(Q−1)"], ["E", "відкрита"], ["D", "E⁻¹ mod φ"],
  ];
  $("keyOutput").innerHTML = rows.map(([name, note]) => {
    const v = k[name.toLowerCase()];
    const label = name === "Phi" ? "φ(N)" : name;
    return `<div class="k">${label}<small>${note}</small></div><div class="num">${fmt === "hex" ? v.hex : v.dec}</div>`;
  }).join("") +
    `<div class="k">Перевірка</div><div class="${k.checkOk ? "ok" : "err"}">${esc(k.check)} ${k.checkOk ? "✓" : "✗"}</div>`;
  $("keyStatus").innerHTML = `N = ${k.bits} біт, блок відкритого тексту = ${k.blockBytes} байт, час генерації ${k.elapsedMs} мс`;
  $("cryptKeyInfo").innerHTML = `Використовується ключ з вкладки 1: N = ${k.bits} біт, E = ${k.e.dec}, блок = ${k.blockBytes} байт`;
}

$("btnGenerate").onclick = (e) => busy(e.target, async () => {
  $("keyStatus").textContent = "Генерація простих чисел…";
  currentKey = await api("/api/keys", { bits: +$("keyBits").value, e: +$("keyE").value });
  renderKey();
});
$("keyFormat").onchange = renderKey;

$("btnSmall").onclick = (e) => busy(e.target, async () => {
  const r = await api("/api/demo-small", { p: $("smP").value, q: $("smQ").value, e: $("smE").value, m: $("smM").value });
  $("smallOutput").textContent = r.steps.join("\n");
});

// ---- 2. Шифрування / розшифрування ----
const cell = (v) => `<td><div>${esc(v)}</div></td>`;

$("btnEncrypt").onclick = (e) => busy(e.target, async () => {
  if (!currentKey) throw new Error("спершу згенеруйте ключі у вкладці 1");
  const r = await api("/api/encrypt", { text: $("plainText").value, e: currentKey.e.hex, n: currentKey.n.hex });
  $("encryptTable").innerHTML = `<p class="status">Блоків: ${r.blocks.length}, по ${r.blockBytes} байт</p>
    <table><tr><th class="n">i</th><th>Байти UTF-8 (hex)</th><th>Mᵢ (dec)</th><th>Cᵢ = Mᵢ^E mod N (hex)</th></tr>
    ${r.blocks.map((b) => `<tr><td class="n">${b.index}</td>${cell(b.bytesHex)}${cell(b.mDec)}${cell(b.cHex)}</tr>`).join("")}</table>`;
  $("cipherText").value = r.blocks.map((b) => b.cHex).join("\n");
});

$("btnDecrypt").onclick = (e) => busy(e.target, async () => {
  if (!currentKey) throw new Error("спершу згенеруйте ключі у вкладці 1");
  const blocks = $("cipherText").value.split(/\s+/).filter(Boolean);
  const r = await api("/api/decrypt", { cipherBlocks: blocks, d: currentKey.d.hex, n: currentKey.n.hex });
  $("decryptTable").innerHTML = `<table><tr><th class="n">i</th><th>Cᵢ (hex)</th><th>Mᵢ = Cᵢ^D mod N (dec)</th><th>Байти (hex)</th></tr>
    ${r.blocks.map((b) => `<tr><td class="n">${b.index}</td>${cell(b.cHex)}${cell(b.mDec)}${cell(b.bytesHex)}</tr>`).join("")}</table>`;
  const same = r.text === $("plainText").value;
  $("decryptResult").innerHTML = `Розшифрований текст: <b>${esc(r.text)}</b> ` +
    (same ? `<span class="ok">✓ збігається з відкритим текстом</span>` : "");
});

// ---- 3. Обмін з сервером ----
function log(side, title, body) {
  const cls = { "Клієнт": "client", "Сервер": "server", "Мережа": "wire" }[side];
  const li = document.createElement("li");
  li.innerHTML = `<span class="side ${cls}">${side}</span>${esc(title)}` + (body ? `<pre>${esc(body)}</pre>` : "");
  $("exLog").appendChild(li);
}

// (а) формування ключів клієнта у браузері
async function makeClientKeys() {
  const bits = +$("clientBits").value;
  const t0 = performance.now();
  await new Promise((r) => setTimeout(r, 10));           // дати браузеру оновити інтерфейс
  ex.client = Rsa.generateKeyPair(bits, 65537n);
  const ms = Math.round(performance.now() - t0);
  log("Клієнт", `Згенеровано ключі клієнта (${bits} біт) за ${ms} мс. Закритий ключ D залишається в браузері.`,
    `E = ${Rsa.toHex(ex.client.e)}\nN = ${Rsa.toHex(ex.client.n)}\nперевірка D·E mod φ = ${(ex.client.d * ex.client.e) % ex.client.phi}`);
}

async function fetchServerKey() {
  const k = await api("/api/server-key");
  ex.server = { e: Rsa.fromHex(k.e), n: Rsa.fromHex(k.n) };
  log("Мережа", `GET /api/server-key ← відкритий ключ сервера (${k.bits} біт, блок ${k.blockBytes} байт)`,
    JSON.stringify(k, null, 2));
}

// (б) шифрування, відправлення та отримання відповіді
async function sendMessage() {
  if (!ex.client) await makeClientKeys();
  if (!ex.server) await fetchServerKey();
  const text = $("exMessage").value;

  const blocks = Rsa.textToBlocks(text, ex.server.n);
  log("Клієнт", `Відкритий текст: "${text}" → ${blocks.length} блок(ів) Mᵢ по ≤ ${Rsa.blockBytes(ex.server.n)} байт`,
    blocks.map((m, i) => `M${i + 1} = ${Rsa.toHex(m)}`).join("\n"));

  const cipher = Rsa.encrypt(text, ex.server).map(Rsa.toHex);
  const body = { ciphertextBlocks: cipher, clientPublicKey: { e: Rsa.toHex(ex.client.e), n: Rsa.toHex(ex.client.n) } };
  log("Мережа", "POST /api/exchange → (по мережі лише шифротекст і відкритий ключ клієнта)", JSON.stringify(body, null, 2));

  const r = await api("/api/exchange", body);
  log("Сервер", "Сервер розшифрував своїм закритим ключем і сформував відповідь (журнал сервера):",
    `бачить текст: "${r.serverLog.decryptedText}"\nвідповідь: "${r.serverLog.replyText}"`);
  log("Мережа", "Відповідь ← зашифрована відкритим ключем клієнта", JSON.stringify({ replyBlocks: r.replyBlocks }, null, 2));

  const reply = Rsa.decrypt(r.replyBlocks.map(Rsa.fromHex), ex.client.privateKey);
  log("Клієнт", "Розшифровано закритим ключем клієнта:", reply);
  $("exReply").innerHTML = `Відповідь сервера: <b>${esc(reply)}</b>`;
}

$("btnClientKeys").onclick = (e) => busy(e.target, makeClientKeys);
$("btnServerKey").onclick = (e) => busy(e.target, fetchServerKey);
$("btnSend").onclick = (e) => busy(e.target, sendMessage);
$("btnClearLog").onclick = () => { $("exLog").innerHTML = ""; $("exReply").textContent = ""; };
$("clientBits").onchange = () => { ex.client = null; };
