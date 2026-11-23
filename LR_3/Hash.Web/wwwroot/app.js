// Допоміжні функції
const $ = id => document.getElementById(id);
const esc = s => String(s).replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' })[c]);
const dig = d => `<code class="bits">${d.binary}</code> <span class="muted">${d.hex}</span>`;

async function call(url, options) {
  const res = await fetch(url, options);
  const body = await res.json();
  if (!res.ok) throw new Error(body.error || res.statusText);
  return body;
}

function bind(formId, outId, handler) {
  $(formId).addEventListener('submit', async e => {
    e.preventDefault();
    const out = $(outId);
    out.innerHTML = '<span class="muted">Обчислення…</span>';
    try { out.innerHTML = await handler(new FormData(e.target)); }
    catch (err) { out.innerHTML = `<span class="err">Помилка: ${esc(err.message)}</span>`; }
  });
}

// 1. Дайджест
bind('digestForm', 'digestOut', async fd => {
  const r = await call('/api/digest', { method: 'POST', body: fd });
  return `<p><b>${esc(r.name)}</b> (${r.size} байт), ${r.bits} біт:</p>
          <p class="big">${dig(r.digest)}</p>
          <p class="muted">Внутрішній 64-бітний стан: ${r.full64}</p>`;
});

// 2. Лавинний ефект
bind('avaForm', 'avaOut', async fd => {
  const rows = await call('/api/avalanche?trials=' + encodeURIComponent(fd.get('trials')));
  return `<table><tr><th>Біт</th><th>Змін</th><th>Змінених бітів, %</th>
            <th>Дайджест змінився, %</th><th>Ідеал, %</th><th>64-біт стан, %</th></tr>
          ${rows.map(r => `<tr><td>${r.bits}</td><td>${r.trials}</td>
            <td class="${r.avgChangedPercent >= 30 ? 'ok' : 'err'}">${r.avgChangedPercent.toFixed(2)}</td>
            <td>${r.digestChangedPercent.toFixed(2)}</td><td>${r.expectedChangedPercent.toFixed(2)}</td>
            <td>${r.full64ChangedPercent.toFixed(2)}</td></tr>`).join('')}</table>
          <p class="muted">Вимога: ≥ 30% змінених бітів. «Ідеал» = 1 − 2<sup>−n</sup> — частка змін, що змінюють n-бітний дайджест.</p>`;
});

// 3. Колізія
bind('colForm', 'colOut', async fd => {
  const r = await call('/api/collide', { method: 'POST', body: fd });
  const bytes = Uint8Array.from(atob(r.fileBase64), c => c.charCodeAt(0));
  const url = URL.createObjectURL(new Blob([bytes]));
  const same = r.original.binary === r.after.binary;
  return `<table>
      <tr><td>Дайджест оригіналу</td><td>${dig(r.original)}</td></tr>
      <tr><td>Змінений файл до nonce</td><td>${dig(r.before)}</td></tr>
      <tr><td>Змінений файл з nonce</td><td>${dig(r.after)} <span class="${same ? 'ok' : 'err'}">${same ? '✔ збігається' : '✘'}</span></td></tr>
      <tr><td>Спосіб</td><td>${esc(r.method)}</td></tr>
      <tr><td>nonce</td><td><code>${esc(r.nonce || '—')}</code></td></tr>
      <tr><td>Спроб</td><td>${r.attempts} (очікувано ≈ 2<sup>${r.bits}</sup> = ${2 ** r.bits})</td></tr>
    </table>
    <p><a class="btn" href="${url}" download="${esc(r.fileName)}">Завантажити ${esc(r.fileName)}</a></p>`;
});
