import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { example, validate, sectors, winner, motion, shuffle, parseIni, serializeIni } from '../core.js';

test('weights normalize within equal player sectors, including zero and large values', () => {
  for (let n = 1; n <= 5; n++) {
    const s = example();
    s.players = Array.from({ length: n }, (_, i) => ({ name: `P${i}`, force: null, drag: null, fields: [{ name: 'A', weight: 1 }, { name: 'B', weight: 3 }] }));
    assert.equal(validate(s), null);
    const sectorList = sectors(s);
    assert.ok(Math.abs(sectorList.reduce((sum, x) => sum + x.sweep, 0) - 360) < 1e-9);
    assert.ok(Math.abs(sectorList[0].sweep - 90 / n) < 1e-9);
    for (const part of sectorList) assert.deepEqual(winner(s, part.start + part.sweep / 2), part);
    const order = shuffle(n, () => .5);
    assert.deepEqual([...order].sort(), Array.from({ length: n }, (_, i) => i));
  }
  const s = example(); s.players = [{ name: 'Solo', force: null, drag: null, fields: [{ name: 'A', weight: 1e307 }, { name: 'B', weight: 3e307 }] }];
  assert.ok(Math.abs(sectors(s)[0].sweep - 90) < 1e-9);
  s.players[0].fields[0].weight = 0;
  assert.equal(winner(s, 0).field, 1);
  s.players[0].fields[1].weight = 0;
  assert.match(validate(s), /at least one/);
});

test('pointer angle maps directly to the stationary sector', () => {
  const s = example(); s.players = [{ name: 'Solo', force: null, drag: null, fields: ['N', 'E', 'S', 'W'].map(name => ({ name, weight: 1 })) }];
  for (const [angle, index] of [[45, 0], [90, 1], [135, 1], [225, 2], [315, 3], [-45, 3], [405, 0]])
    assert.equal(winner(s, angle).field, index);
});

test('signed force sets direction and drag always stops; slow case travels two degrees', () => {
  assert.equal(motion(0, 1, 10).angleAt(100), 2);
  for (const scale of [.9, 1.1]) assert.ok(motion(0, 1, 10, scale).angleAt(100) >= 1 && motion(0, 1, 10, scale).angleAt(100) <= 3);
  for (const force of [-10, -5, -1, 0, 1, 5, 10]) {
    for (let drag = 1; drag <= 10; drag++) {
      const m = motion(45, force, drag);
      assert.ok(Number.isFinite(m.duration));
      assert.equal(m.angleAt(0), 45);
      assert.equal(m.angleAt(100), m.angleAt(m.duration));
      assert.ok((m.angleAt(m.duration) - 45) * force >= 0);
    }
  }
  for (const drag of [-10, -1, 0, 11]) assert.throws(() => motion(0, 1, drag), RangeError);
});

test('INI imports the desktop preset format and round-trips without a 100% sum', () => {
  const desktop = parseIni(readFileSync(new URL('../../fortune-wheel.ini', import.meta.url), 'utf8'));
  assert.equal(desktop.players.length, 3);
  assert.equal(desktop.players[1].force, 7);
  const s = example();
  s.players[0].fields[0].weight = 7;
  s.players[0].fields[1].weight = 2;
  s.players[0].fields[2].weight = 1;
  assert.equal(validate(s), null);
  const text = serializeIni(s);
  assert.deepEqual(parseIni(text), s);
  assert.deepEqual(parseIni(text.replace('Field1Share=7', 'Field1Share=700')), { ...s, players: [{ ...s.players[0], fields: [{ ...s.players[0].fields[0], weight: 700 }, ...s.players[0].fields.slice(1)] }, ...s.players.slice(1)] });
  assert.throws(() => parseIni(text.replace('Drag=5', 'Drag=0')));
  assert.throws(() => parseIni(text + '\n[Wheel]\nPlayers=3'));
  assert.throws(() => parseIni(text.replace('Field1Share=7', 'Field1Share=NaN')));
});
