// Pure wheel rules shared by the browser UI and Node checks.
export const example = () => ({
  force: 5, drag: 5, varyImpulse: true,
  players: [
    { name: 'Player 1', force: null, drag: null, fields: [
      { name: 'Start', weight: 40 }, { name: 'Bonus', weight: 30 }, { name: 'Trap', weight: 30 }] },
    { name: 'Player 2', force: 7, drag: 4, fields: [
      { name: 'Coin', weight: 50 }, { name: 'Challenge', weight: 30 }, { name: 'Lose a turn', weight: 20 }] },
    { name: 'Player 3', force: 3, drag: 6, fields: [
      { name: 'Move', weight: 40 }, { name: 'Draw', weight: 40 }, { name: 'Swap', weight: 20 }] },
  ],
});

export function validate(settings) {
  if (!settings || !Array.isArray(settings.players) || settings.players.length < 1 || settings.players.length > 5)
    return 'Choose 1–5 players.';
  if (!Number.isInteger(settings.force) || settings.force < -10 || settings.force > 10 ||
      !Number.isInteger(settings.drag) || settings.drag < 1 || settings.drag > 10)
    return 'Default force must be −10 to 10 and drag 1 to 10.';
  if (typeof settings.varyImpulse !== 'boolean') return 'Impulse variation must be on or off.';
  for (const p of settings.players) {
    if (!p || typeof p.name !== 'string' || !p.name.trim()) return 'Every player needs a name.';
    if (p.force !== null && (!Number.isInteger(p.force) || p.force < -10 || p.force > 10) ||
        p.drag !== null && (!Number.isInteger(p.drag) || p.drag < 1 || p.drag > 10))
      return `${p.name}: force must be −10 to 10 and drag 1 to 10, or blank for default.`;
    if (!Array.isArray(p.fields) || p.fields.length < 1 || p.fields.length > 5)
      return `${p.name}: choose 1–5 fields.`;
    if (p.fields.some(f => !f || typeof f.name !== 'string' || !f.name.trim()))
      return `${p.name}: every field needs a name.`;
    if (p.fields.some(f => !Number.isFinite(f.weight) || f.weight < 0))
      return `${p.name}: field weights must be finite, nonnegative numbers.`;
    if (!p.fields.some(f => f.weight > 0)) return `${p.name}: at least one weight must be positive.`;
  }
  return null;
}

export const normalize = a => ((a % 360) + 360) % 360;

export function sectors(settings) {
  const size = 360 / settings.players.length;
  return settings.players.flatMap((p, pi) => {
    const max = Math.max(...p.fields.map(f => Number.isFinite(f.weight) && f.weight > 0 ? f.weight : 0));
    const values = p.fields.map(f => max && Number.isFinite(f.weight) && f.weight > 0 ? f.weight / max : 0);
    const total = values.reduce((a, b) => a + b, 0);
    let start = pi * size;
    return p.fields.map((f, fi) => {
      const sweep = total ? size * values[fi] / total : size / p.fields.length;
      const result = { player: pi, field: fi, start, sweep, fraction: sweep / size };
      start += sweep;
      return result;
    });
  });
}

export function winner(settings, angle) {
  const value = normalize(angle);
  const size = 360 / settings.players.length;
  const player = Math.min(settings.players.length - 1, Math.floor(value / size));
  const candidates = sectors(settings).filter(s => s.player === player && s.sweep > 0);
  // A floating point rounding gap belongs to the final positive field.
  return candidates.find(s => value >= s.start && value < s.start + s.sweep) ?? candidates.at(-1);
}

export function motion(start, force, drag, scale = 1) {
  if (!Number.isFinite(start) || !Number.isInteger(force) || force < -10 || force > 10 ||
      !Number.isInteger(drag) || drag < 1 || drag > 10 || !Number.isFinite(scale) || scale <= 0)
    throw new RangeError('Invalid spin parameters.');
  const speed = 20 * Math.abs(force) ** 1.5 * scale;
  const deceleration = 10 * drag;
  const duration = speed / deceleration;
  return {
    duration,
    angleAt(seconds) {
      const t = Math.max(0, Math.min(duration, seconds));
      return start + Math.sign(force) * (speed * t - 0.5 * deceleration * t * t);
    },
  };
}

export function shuffle(count, random = Math.random) {
  const result = Array.from({ length: count }, (_, i) => i);
  for (let i = count - 1; i > 0; i--) {
    const j = Math.floor(random() * (i + 1));
    [result[i], result[j]] = [result[j], result[i]];
  }
  return result;
}

export function parseIni(text) {
  const sections = new Map();
  let current;
  text.split(/\r?\n/).forEach((raw, i) => {
    const line = raw.trim();
    if (!line || line.startsWith(';') || line.startsWith('#')) return;
    if (line.startsWith('[') && line.endsWith(']')) {
      const name = line.slice(1, -1).trim().toLowerCase();
      if (sections.has(name)) throw new Error(`Duplicate section at line ${i + 1}.`);
      current = new Map(); sections.set(name, current); return;
    }
    const split = line.indexOf('=');
    if (!current || split < 1) throw new Error(`Invalid INI syntax at line ${i + 1}.`);
    const key = line.slice(0, split).trim().toLowerCase();
    if (current.has(key)) throw new Error(`Duplicate key at line ${i + 1}.`);
    current.set(key, line.slice(split + 1).trim());
  });
  const get = (section, key, fallback) => {
    const value = sections.get(section.toLowerCase())?.get(key.toLowerCase());
    if (value === undefined && fallback === undefined) throw new Error(`Missing [${section}] ${key}.`);
    return value ?? fallback;
  };
  const integer = (section, key, fallback) => {
    const raw = get(section, key, fallback);
    if (!/^[+-]?\d+$/.test(raw)) throw new Error(`[${section}] ${key} must be an integer.`);
    return Number(raw);
  };
  const count = integer('Wheel', 'Players');
  if (count < 1 || count > 5) throw new Error('Players must be 1–5.');
  const rawVary = get('Wheel', 'VaryImpulse', 'true').toLowerCase();
  if (!['true', 'false'].includes(rawVary)) throw new Error('VaryImpulse must be true or false.');
  const result = { force: integer('Wheel', 'Force', '5'), drag: integer('Wheel', 'Drag', '5'), varyImpulse: rawVary === 'true', players: [] };
  for (let i = 1; i <= count; i++) {
    const section = `Player${i}`;
    const optional = key => get(section, key, '') === '' ? null : integer(section, key);
    const p = { name: get(section, 'Name'), force: optional('Force'), drag: optional('Drag'), fields: [] };
    const length = integer(section, 'Fields');
    if (length < 1 || length > 5) throw new Error(`${section}: Fields must be 1–5.`);
    for (let j = 1; j <= length; j++) {
      const raw = get(section, `Field${j}Share`);
      if (!/^[+-]?(?:\d+\.?\d*|\.\d+)(?:[eE][+-]?\d+)?$/.test(raw))
        throw new Error(`${section}: Field${j}Share must be a nonnegative number with a decimal point.`);
      p.fields.push({ name: get(section, `Field${j}Name`), weight: Number(raw) });
    }
    result.players.push(p);
  }
  const error = validate(result);
  if (error) throw new Error(error);
  return result;
}

export function serializeIni(settings) {
  const error = validate(settings);
  if (error) throw new Error(error);
  const name = value => {
    if (/[\r\n]/.test(value)) throw new Error('Names cannot contain line breaks.');
    return value.trim();
  };
  const lines = [
    '; Fortune Wheel — relative weights within each player sector.',
    '; Blank player Force/Drag inherits the defaults. Decimal separator: dot.',
    '[Wheel]', `Players=${settings.players.length}`, `Force=${settings.force}`,
    `Drag=${settings.drag}`, `VaryImpulse=${settings.varyImpulse}`, '',
  ];
  settings.players.forEach((p, i) => {
    lines.push(`[Player${i + 1}]`, `Name=${name(p.name)}`, `Force=${p.force ?? ''}`, `Drag=${p.drag ?? ''}`, `Fields=${p.fields.length}`);
    p.fields.forEach((f, j) => lines.push(`Field${j + 1}Name=${name(f.name)}`, `Field${j + 1}Share=${f.weight}`));
    lines.push('');
  });
  return lines.join('\n');
}
