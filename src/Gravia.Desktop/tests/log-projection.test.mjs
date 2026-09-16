import assert from 'node:assert/strict';
import { test } from 'node:test';
import { performance } from 'node:perf_hooks';
import {
  createLogProjection,
  createLogViewStore,
  visibleLogRange,
} from '../renderer/pages/main/ui/log-projection.ts';
import { setTimeout as delay } from 'node:timers/promises';

const options = { search: '', levels: 63, collapse: false };
async function until(predicate) {
  for (let i = 0; i < 1000; i++) {
    if (predicate()) return;
    await delay(5);
  }
  assert.fail('View indexing timed out');
}

test('chunked view indexing handles newer snapshots and React resubscription without late updates', async () => {
  const store = createLogViewStore({ ...options, collapse: true });
  const data = snapshot(Array.from({ length: 50000 }, (_, i) => entry(i + 1)));
  let calls = 0;
  let stop = store.subscribe(() => {
    calls++;
  });
  store.offer(data);
  // Abort an unfinished initial pass, as React StrictMode does during effect replay.
  stop();
  stop = store.subscribe(() => {
    calls++;
  });
  store.offer(snapshot([entry(50001, 'after clear')], 'a', 50001));
  await until(() => store.getSnapshot()?.rows[0]?.entry.sequence === 50001);
  assert.equal(store.getSnapshot().rows.length, 1);
  assert.equal(store.getSnapshot().counts[4], 1);
  stop();
  const before = calls;
  store.offer(snapshot([entry(50002)]));
  await delay(10);
  assert.equal(calls, before);
});
function entry(sequence, message = 'NRE', level = 4) {
  return {
    sequence,
    message,
    level,
    category: 'Model',
    eventId: { id: 0, name: null },
    timestamp: new Date(sequence).toISOString(),
    exception: 'NullReferenceException\n at Calculate()',
    properties: { unit: 'АВТ' },
    isTruncated: false,
  };
}
function snapshot(entries, journalId = 'a', revision = entries.at(-1)?.sequence ?? 0) {
  return {
    entries,
    error: null,
    state: {
      journalId,
      revision,
      lastSequence: entries.at(-1)?.sequence ?? revision,
      firstSequence: entries[0]?.sequence ?? revision + 1,
      entryCount: entries.length,
    },
  };
}

test('independent panels share data, never filters or collapse; status can read the last record', () => {
  const a = createLogProjection();
  const b = createLogProjection();
  const data = snapshot([entry(1), entry(2, 'warning', 3), entry(3)]);
  assert.equal(a(data, options).rows.length, 3);
  const grouped = b(data, { ...options, collapse: true });
  assert.deepEqual(
    grouped.rows.map((row) => row.count),
    [2, 1],
  );
  assert.equal(grouped.rows[0].entry.sequence, 3);
  assert.equal(a(data, { ...options, levels: 1 << 3 }).rows.length, 1);
  assert.equal(b(data, { ...options, collapse: true }), grouped);
  assert.equal(data.entries.at(-1).sequence, 3);
});

test('search includes context, eviction decrements counts, clear and restart remove selection targets', () => {
  const project = createLogProjection();
  const filtered = { ...options, collapse: true, search: 'авт' };
  let rows = project(snapshot([entry(1), entry(2), entry(3)]), filtered).rows;
  assert.equal(rows[0].count, 3);
  const id = rows[0].id;
  rows = project(snapshot([entry(2), entry(3), entry(4)]), filtered).rows;
  assert.equal(rows[0].id, id);
  assert.equal(rows[0].count, 3);
  const empty = project(snapshot([], 'a', 5), filtered);
  assert.deepEqual(empty.rows, []);
  assert.deepEqual(empty.counts, [0, 0, 0, 0, 0, 0]);
  rows = project(snapshot([entry(1, 'restart')], 'b'), filtered).rows;
  assert.equal(rows[0].entry.message, 'restart');
});

test('collapse distinguishes exception, source, level, event and properties, but not timestamp', () => {
  const base = entry(1);
  const variants = [
    base,
    entry(2),
    { ...entry(3), exception: 'different stack' },
    { ...entry(4), category: 'Other' },
    entry(5, 'NRE', 5),
    { ...entry(6), eventId: { id: 9, name: null } },
    { ...entry(7), properties: { unit: 'other' } },
  ];
  const view = createLogProjection()(snapshot(variants), { ...options, collapse: true });
  assert.equal(view.rows.length, 6);
  assert.equal(view.rows[0].count, 2);
});

test('100,000 records, incremental bursts and two panels keep virtual DOM range bounded', (t) => {
  const records = Array.from({ length: 100000 }, (_, i) => entry(i + 1));
  const plain = createLogProjection();
  const collapsed = createLogProjection();
  const start = performance.now();
  plain(snapshot([...records]), options);
  collapsed(snapshot([...records]), { ...options, collapse: true });
  const initialMs = performance.now() - start;
  const timings = [];
  for (let burst = 0; burst < 60; burst++) {
    const fresh = Array.from({ length: 100 }, (_, i) => entry(records.at(-1).sequence + i + 1));
    records.splice(0, 100);
    records.push(...fresh);
    // Production arrays are immutable; preserve that contract across the selector's snapshots.
    const data = snapshot([...records]);
    const before = performance.now();
    const view = plain(data, options);
    const groups = collapsed(data, { ...options, collapse: true });
    timings.push(performance.now() - before);
    assert.equal(view.rows.length, 100000);
    assert.equal(groups.rows[0].count, 100000);
    const range = visibleLogRange(view.rows.length, 1500000, 500);
    assert.ok(range.end - range.start <= 41);
  }
  timings.sort((a, b) => a - b);
  t.diagnostic(
    `Initial index: ${initialMs.toFixed(1)} ms; two-panel update p95: ${timings[56].toFixed(1)} ms (not a renderer FPS guarantee).`,
  );
});
