import { example, validate, normalize, sectors, winner, motion, shuffle, parseIni, serializeIni } from './core.js';

const $ = id => document.getElementById(id);
const NS = 'http://www.w3.org/2000/svg';
const colors = ['#539bf5', '#eb738a', '#4abe9e', '#c097f5', '#e8b14c'];
const storageKey = 'fortune-wheel-web-v1';
let settings = loadSaved();
let pointerAngle = 0;
let round = null;
let selected = null;

function loadSaved() {
  try {
    const saved = JSON.parse(localStorage.getItem(storageKey));
    if (saved && !validate(saved)) return saved;
  } catch { /* Storage can be disabled; the example remains usable. */ }
  return example();
}

function persist() {
  const error = validate(settings);
  const badInput = [...$('editor').querySelectorAll('input:not([type=file])')].some(i => !i.checkValidity());
  $('feedback').textContent = badInput ? 'Correct the highlighted numeric input.' : error ?? 'Ready · Valid changes are saved in this browser.';
  if (badInput || error) return false;
  try { localStorage.setItem(storageKey, JSON.stringify(settings)); }
  catch { $('feedback').textContent = 'Settings are valid, but this browser cannot save them. Export an .ini file.'; }
  return true;
}

function element(tag, className, text) {
  const node = document.createElement(tag);
  if (className) node.className = className;
  if (text !== undefined) node.textContent = text;
  return node;
}

function input(value, label, type, onChange, attributes = {}) {
  const control = element('input');
  control.type = type;
  control.value = value ?? '';
  control.setAttribute('aria-label', label);
  for (const [key, content] of Object.entries(attributes)) control.setAttribute(key, String(content));
  control.addEventListener('input', () => { onChange(control.value, control); selected = null; drawWheel(); persist(); });
  return control;
}

function button(label, action, className) {
  const control = element('button', className, label);
  control.type = 'button';
  control.addEventListener('click', action);
  return control;
}

function numberValue(value) { return value.trim() === '' ? NaN : Number(value); }

function renderEditor() {
  const players = $('players');
  const spinSettings = $('spin-settings');
  players.replaceChildren(); spinSettings.replaceChildren();
  settings.players.forEach((player, pi) => {
    const card = element('div', 'player');
    card.style.setProperty('--player-color', colors[pi]);
    const header = element('div', 'player-top');
    header.append(element('span', 'dot'));
    header.append(input(player.name, `Player ${pi + 1} name`, 'text', value => {
      player.name = value; spinSettings.children[pi].querySelector('span').textContent = value;
    }));
    header.append(button('Remove', () => {
      if (settings.players.length === 1) { $('feedback').textContent = 'Keep at least one player.'; return; }
      settings.players.splice(pi, 1); selected = null; renderEditor();
    }, 'danger'));
    card.append(header);
    player.fields.forEach((field, fi) => {
      const row = element('div', 'field-row');
      row.append(input(field.name, `${player.name} field ${fi + 1} name`, 'text', value => { field.name = value; }));
      row.append(input(field.weight, `${player.name} field ${fi + 1} weight`, 'number', value => {
        field.weight = numberValue(value);
      }, { min: 0, step: 'any', class: 'weight' }));
      row.append(button('×', () => {
        if (player.fields.length === 1) { $('feedback').textContent = 'Keep at least one field per player.'; return; }
        player.fields.splice(fi, 1); selected = null; renderEditor();
      }, 'danger'));
      card.append(row);
    });
    const actions = element('div', 'row player-actions');
    actions.append(button('+ Field', () => {
      if (player.fields.length === 5) { $('feedback').textContent = 'Each player can have up to 5 fields.'; return; }
      player.fields.push({ name: 'New field', weight: 1 }); renderEditor();
    }));
    actions.append(button('Equal weights', () => { player.fields.forEach(f => { f.weight = 1; }); renderEditor(); }));
    card.append(actions);
    players.append(card);

    const settingsRow = element('div', 'setting-row');
    const playerLabel = element('span', '', player.name);
    playerLabel.style.color = colors[pi];
    settingsRow.append(playerLabel);
    settingsRow.append(input(player.force, `${player.name} force override`, 'number', value => {
      player.force = value.trim() === '' ? null : Number(value);
    }, { min: -10, max: 10, step: 1, placeholder: String(settings.force) }));
    settingsRow.append(input(player.drag, `${player.name} drag override`, 'number', value => {
      player.drag = value.trim() === '' ? null : Number(value);
    }, { min: 1, max: 10, step: 1, placeholder: String(settings.drag) }));
    spinSettings.append(settingsRow);
  });
  $('default-force').value = String(settings.force);
  $('default-drag').value = String(settings.drag);
  $('vary').checked = settings.varyImpulse;
  drawWheel(); persist();
}

function svg(tag, attrs = {}, text) {
  const node = document.createElementNS(NS, tag);
  for (const [key, value] of Object.entries(attrs)) node.setAttribute(key, String(value));
  if (text !== undefined) node.textContent = text;
  return node;
}

function point(radius, angle) {
  const rad = angle * Math.PI / 180;
  return [300 + radius * Math.sin(rad), 300 - radius * Math.cos(rad)];
}

function pie(radius, start, sweep) {
  if (sweep >= 359.999999) return null;
  const [x1, y1] = point(radius, start), [x2, y2] = point(radius, start + sweep);
  return `M 300 300 L ${x1} ${y1} A ${radius} ${radius} 0 ${sweep > 180 ? 1 : 0} 1 ${x2} ${y2} Z`;
}

function wedge(radius, start, sweep, attrs) {
  if (sweep >= 359.999999) return svg('circle', { cx: 300, cy: 300, r: radius, ...attrs });
  return svg('path', { d: pie(radius, start, sweep), ...attrs });
}

function drawWheel() {
  const group = $('sectors');
  group.replaceChildren();
  const all = sectors(settings);
  all.forEach(s => {
    if (s.sweep <= 0) return;
    const color = colors[s.player];
    const darker = s.field * 9;
    const rgb = color.slice(1).match(/../g).map(v => Math.max(0, parseInt(v, 16) - darker));
    group.append(wedge(251, s.start, s.sweep, {
      fill: `rgb(${rgb.join(',')})`, stroke: '#162030', 'stroke-width': 2,
      ...(selected?.player === s.player && selected.field === s.field ? { 'stroke-width': 5, stroke: '#ffffff' } : {}),
    }));
    if (s.sweep >= 12) {
      const [x, y] = point(183, s.start + s.sweep / 2);
      const label = svg('text', { x, y: y - 6, class: 'sector-label' });
      label.append(svg('tspan', { x, dy: 0 }, settings.players[s.player].fields[s.field].name));
      label.append(svg('tspan', { x, dy: 18, class: 'sector-share' }, `${(s.fraction * 100).toFixed(1).replace(/\.0$/, '')}%`));
      group.append(label);
    }
  });
  const size = 360 / settings.players.length;
  settings.players.forEach((p, i) => {
    group.append(wedge(110, i * size, size, { fill: colors[i], 'fill-opacity': .45, stroke: '#dce7f7', 'stroke-width': 1 }));
    const [x, y] = point(73, (i + .5) * size);
    group.append(svg('text', { x, y, class: 'player-label' }, p.name));
  });
  group.append(svg('circle', { cx: 300, cy: 300, r: 14, fill: '#111b29', stroke: 'white', 'stroke-width': 3 }));
  group.append(svg('circle', { cx: 300, cy: 300, r: 251, fill: 'none', stroke: '#869dbb', 'stroke-width': 3 }));
}

function setPointer(angle) {
  pointerAngle = normalize(angle);
  $('pointer').setAttribute('transform', `rotate(${pointerAngle} 300 300)`);
}

function animate(spin, signal) {
  return new Promise((resolve, reject) => {
    let started = performance.now();
    let hiddenAt = document.hidden ? started : null;
    let frame;
    const cleanup = () => { cancelAnimationFrame(frame); signal.removeEventListener('abort', abort); document.removeEventListener('visibilitychange', visibility); };
    const abort = () => { cleanup(); reject(new DOMException('Cancelled', 'AbortError')); };
    const visibility = () => {
      if (document.hidden) hiddenAt = performance.now();
      else if (hiddenAt !== null) { started += performance.now() - hiddenAt; hiddenAt = null; frame = requestAnimationFrame(tick); }
    };
    const tick = now => {
      if (document.hidden) return;
      const elapsed = Math.max(0, (now - started) / 1000);
      setPointer(spin.angleAt(elapsed));
      if (elapsed >= spin.duration) { cleanup(); resolve(); }
      else frame = requestAnimationFrame(tick);
    };
    signal.addEventListener('abort', abort, { once: true });
    document.addEventListener('visibilitychange', visibility);
    if (signal.aborted) abort();
    else frame = requestAnimationFrame(tick);
  });
}

function delay(ms, signal) {
  return new Promise((resolve, reject) => {
    const abort = () => { clearTimeout(timer); reject(new DOMException('Cancelled', 'AbortError')); };
    const timer = setTimeout(() => { signal.removeEventListener('abort', abort); resolve(); }, ms);
    signal.addEventListener('abort', abort, { once: true });
    if (signal.aborted) abort();
  });
}

async function startRound() {
  if (round || !persist()) return;
  round = new AbortController();
  const signal = round.signal;
  $('editor').disabled = $('start').disabled = true;
  $('cancel').disabled = false;
  $('results').replaceChildren();
  const order = shuffle(settings.players.length);
  $('order').textContent = `Order: ${order.map(i => settings.players[i].name).join(' → ')}`;
  try {
    for (let turn = 0; turn < order.length; turn++) {
      const p = settings.players[order[turn]];
      const force = p.force ?? settings.force, drag = p.drag ?? settings.drag;
      const scale = settings.varyImpulse ? .9 + Math.random() * .2 : 1;
      const spin = motion(pointerAngle, force, drag, scale);
      selected = null; drawWheel();
      $('round-status').textContent = `Spin ${turn + 1}/${order.length} · ${p.name} · ${force > 0 ? '↻' : force < 0 ? '↺' : 'No impulse'}`;
      $('feedback').textContent = `Force ${force} · Drag ${drag} · Impulse ${scale.toFixed(2)}×`;
      await animate(spin, signal);
      if (signal.aborted) throw new DOMException('Cancelled', 'AbortError');
      selected = winner(settings, pointerAngle);
      drawWheel();
      const owner = settings.players[selected.player];
      const field = owner.fields[selected.field];
      $('results').append(element('li', '', `${p.name} spun → ${owner.name}: ${field.name}`));
      $('round-status').textContent = `${owner.name}: ${field.name}`;
      if (turn < order.length - 1) {
        $('feedback').textContent = 'Next spin in 2 seconds…';
        await delay(2000, signal);
      }
    }
    $('feedback').textContent = 'Round complete. Start another round whenever you’re ready.';
  } catch (error) {
    if (error.name !== 'AbortError') throw error;
    $('round-status').textContent = 'Round cancelled';
    $('feedback').textContent = 'Completed results are kept. The interrupted spin has no result.';
  } finally {
    round = null;
    $('editor').disabled = $('start').disabled = false;
    $('cancel').disabled = true;
  }
}

$('add-player').addEventListener('click', () => {
  if (settings.players.length === 5) { $('feedback').textContent = 'The maximum is 5 players.'; return; }
  settings.players.push({ name: `Player ${settings.players.length + 1}`, force: null, drag: null, fields: [{ name: 'New field', weight: 1 }] });
  selected = null; renderEditor();
});
$('default-force').addEventListener('input', event => {
  settings.force = numberValue(event.target.value);
  $('spin-settings').querySelectorAll('input:first-of-type').forEach(i => { if (!i.value) i.placeholder = event.target.value; });
  persist();
});
$('default-drag').addEventListener('input', event => { settings.drag = numberValue(event.target.value); persist(); });
$('vary').addEventListener('change', event => { settings.varyImpulse = event.target.checked; persist(); });
$('start').addEventListener('click', startRound);
$('cancel').addEventListener('click', () => round?.abort());
$('import').addEventListener('click', () => $('file').click());
$('file').addEventListener('change', async event => {
  const file = event.target.files[0];
  if (!file) return;
  try {
    const parsed = parseIni(await file.text());
    settings = parsed; selected = null; setPointer(0);
    $('results').replaceChildren(); $('round-status').textContent = 'Ready to spin';
    $('order').textContent = 'Each player spins once. Two seconds between spins.';
    renderEditor(); $('feedback').textContent = `Imported ${file.name}`;
  } catch (error) { $('feedback').textContent = `Import failed: ${error.message} Current settings are kept.`; }
  finally { event.target.value = ''; }
});
$('export').addEventListener('click', () => {
  if (!persist()) return;
  const url = URL.createObjectURL(new Blob([serializeIni(settings)], { type: 'text/plain;charset=utf-8' }));
  const link = element('a'); link.href = url; link.download = 'fortune-wheel.ini';
  document.body.append(link); link.click(); link.remove();
  setTimeout(() => URL.revokeObjectURL(url), 60000);
  $('feedback').textContent = 'Exported fortune-wheel.ini';
});

renderEditor();
