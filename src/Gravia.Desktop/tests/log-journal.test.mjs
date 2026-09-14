import assert from 'node:assert/strict';
import { test } from 'node:test';
import { setTimeout as delay } from 'node:timers/promises';
import { QueryClient } from '@tanstack/react-query';
import { wrapComputeMethod } from '@actuallab/fusion';
import {
  applyLogBatch,
  observeLogJournal,
  logJournalQueryKey,
} from '../renderer/shared/api/log-journal-sync.ts';

const ok = (data) => ({ isSuccess: true, data });
const entry = (sequence) => ({ sequence, message: 'NRE', level: 4 });
const state = (revision, firstSequence, lastSequence, journalId = 'session-a') => ({
  journalId,
  revision,
  firstSequence,
  lastSequence,
  entryCount: lastSequence - firstSequence + 1,
  usedBytes: 0,
  capacityBytes: 8388608,
});
async function until(predicate) {
  for (let i = 0; i < 200; i++) {
    if (predicate()) return;
    await delay(5);
  }
  assert.fail('Journal synchronization timed out');
}
function source() {
  let current = state(0, 1, 0);
  let entries = [];
  let readHook;
  let calls = 0;
  const getState = wrapComputeMethod(async () => ok(current));
  return {
    api: {
      GetState: getState,
      async Read(id, after) {
        calls++;
        const batch = {
          state: current,
          reset: id !== current.journalId,
          entries: id !== current.journalId ? entries : entries.filter((x) => x.sequence > after),
        };
        if (readHook) await readHook();
        return ok(batch);
      },
      async Clear() {
        current = state(
          current.revision + 1,
          current.lastSequence + 1,
          current.lastSequence,
          current.journalId,
        );
        entries = [];
        getState.invalidate();
        return ok();
      },
    },
    publish(next, records) {
      current = next;
      entries = records;
      getState.invalidate();
    },
    hook(value) {
      readHook = value;
    },
    get calls() {
      return calls;
    },
  };
}

test('backend bounds evict, clear and replace history; repetitions remain distinct', () => {
  let snapshot = applyLogBatch(undefined, {
    reset: true,
    state: state(3, 1, 3),
    entries: [entry(1), entry(2), entry(3)],
  });
  snapshot = applyLogBatch(snapshot, { reset: false, state: state(4, 2, 4), entries: [entry(4)] });
  assert.deepEqual(
    snapshot.entries.map((x) => x.sequence),
    [2, 3, 4],
  );
  snapshot = applyLogBatch(snapshot, { reset: false, state: state(5, 5, 4), entries: [] });
  assert.deepEqual(snapshot.entries, []);
  snapshot = applyLogBatch(snapshot, {
    reset: true,
    state: state(1, 1, 1, 'session-b'),
    entries: [entry(1)],
  });
  assert.deepEqual(
    snapshot.entries.map((x) => x.sequence),
    [1],
  );
});

test('stale same-session response cannot restore cleared history', () => {
  const cleared = { state: state(8, 8, 7), entries: [], error: null };
  assert.equal(
    applyLogBatch(cleared, { reset: false, state: state(7, 1, 7), entries: [entry(7)] }),
    cleared,
  );
});

test('live Fusion invalidations drive deltas and Clear without polling; consumers share lifetime', async () => {
  const query = new QueryClient();
  const fixture = source();
  const observer = observeLogJournal(query, fixture.api);
  const second = observeLogJournal(query, fixture.api);
  try {
    await until(() => query.getQueryData(logJournalQueryKey)?.state?.revision === 0);
    assert.equal(fixture.calls, 1);
    assert.equal(query.getQueryDefaults(logJournalQueryKey).gcTime, Infinity);
    fixture.publish(state(3, 1, 3), [entry(1), entry(2), entry(3)]);
    await until(() => query.getQueryData(logJournalQueryKey)?.entries.length === 3);
    await delay(30);
    assert.equal(fixture.calls, 2);
    observer.stop();
    await fixture.api.Clear();
    await until(() => query.getQueryData(logJournalQueryKey)?.state?.revision === 4);
    assert.deepEqual(query.getQueryData(logJournalQueryKey).entries, []);
    second.stop();
    await second.completion;
    assert.equal(query.getQueryData(logJournalQueryKey), undefined);
  } finally {
    observer.stop();
    second.stop();
    query.clear();
  }
});

test('invalidation during Read is not lost', async () => {
  const query = new QueryClient();
  const fixture = source();
  let release;
  fixture.hook(
    () =>
      new Promise((resolve) => {
        release = resolve;
      }),
  );
  const observer = observeLogJournal(query, fixture.api);
  try {
    await until(() => release);
    fixture.publish(state(1, 1, 1), [entry(1)]);
    fixture.hook(undefined);
    release();
    await until(() => query.getQueryData(logJournalQueryKey)?.entries.length === 1);
  } finally {
    observer.stop();
    await observer.completion;
    query.clear();
  }
});

test('stop completes while RPC is pending and late replies cannot recreate cache', async () => {
  const query = new QueryClient();
  const fixture = source();
  let release;
  fixture.hook(
    () =>
      new Promise((resolve) => {
        release = resolve;
      }),
  );
  const observer = observeLogJournal(query, fixture.api);
  await until(() => release);
  observer.stop();
  await observer.completion;
  release();
  await delay(10);
  assert.equal(query.getQueryData(logJournalQueryKey), undefined);
  query.clear();
});

test('failed reads retain history and retry; backend restart resets sequence', async () => {
  const query = new QueryClient();
  const fixture = source();
  fixture.publish(state(1, 1, 1), [entry(1)]);
  const observer = observeLogJournal(query, fixture.api);
  try {
    await until(() => query.getQueryData(logJournalQueryKey)?.entries.length === 1);
    fixture.hook(() => {
      throw new Error('disconnected');
    });
    fixture.publish(state(2, 1, 2), [entry(1), entry(2)]);
    await until(() => query.getQueryData(logJournalQueryKey)?.error === 'disconnected');
    assert.equal(query.getQueryData(logJournalQueryKey).entries.length, 1);
    fixture.hook(undefined);
    fixture.publish(state(1, 1, 1, 'session-b'), [entry(1)]);
    await until(() => query.getQueryData(logJournalQueryKey)?.state?.journalId === 'session-b');
    assert.equal(query.getQueryData(logJournalQueryKey).error, null);
    assert.equal(query.getQueryData(logJournalQueryKey).entries.length, 1);
  } finally {
    observer.stop();
    await observer.completion;
    query.clear();
  }
});
