// Invoked by LogJournalWireTests against its isolated C# Host, never the user's running app.
import assert from 'node:assert/strict';
import { setTimeout as delay } from 'node:timers/promises';
import { FusionHub } from '@actuallab/fusion-rpc';
import { RpcClientPeer } from '@actuallab/rpc';
import { QueryClient } from '@tanstack/react-query';
import { createLogJournalApi } from '../renderer/shared/api/log-journal-client.ts';
import { logJournalQueryKey, observeLogJournal } from '../renderer/shared/api/log-journal-sync.ts';

const timeout = setTimeout(() => {
  console.error('Fusion logging wire test timed out');
  process.exit(1);
}, 20000);
const hub = new FusionHub();
const peer = new RpcClientPeer(hub, process.argv[2], false);
hub.addPeer(peer);
const api = createLogJournalApi(hub, peer);
peer.start();
const query = new QueryClient();
let observation;
async function until(predicate) {
  for (let i = 0; i < 1000; i++) {
    if (predicate()) return;
    await delay(5);
  }
  assert.fail(JSON.stringify(query.getQueryData(logJournalQueryKey)));
}
try {
  const initial = await api.Read(null, 0);
  assert.equal(initial.isSuccess, true);
  assert.equal(initial.data.entries.length, 700);
  assert.equal(initial.data.reset, true);
  const entry = initial.data.entries[0];
  assert.equal(entry.message, 'Ошибка 🌡');
  assert.equal(entry.level, 4);
  assert.deepEqual(entry.eventId, { id: 42, name: 'ModelError' });
  assert.equal(entry.properties.unit, 'АВТ');
  assert.match(entry.exception, /NullReferenceException/);
  assert.equal(typeof entry.timestamp, 'string');
  const delta = await api.Read(initial.data.state.journalId, initial.data.state.lastSequence);
  assert.deepEqual(delta.data.entries, []);
  const invalid = await api.Read(null, -1);
  assert.equal(invalid.isSuccess, false);
  observation = observeLogJournal(query, api);
  await until(() => query.getQueryData(logJournalQueryKey)?.entries.length === 700);
  console.log('READY');
  await until(
    () => query.getQueryData(logJournalQueryKey)?.entries.at(-1)?.message === 'live-from-host',
  );
  assert.equal(query.getQueryData(logJournalQueryKey).entries.length, 701);
  assert.equal((await api.Clear()).isSuccess, true);
  await until(() => query.getQueryData(logJournalQueryKey)?.entries.length === 0);
  console.log('PASS');
} finally {
  observation?.stop();
  await observation?.completion;
  query.clear();
  hub.close();
  clearTimeout(timeout);
}
