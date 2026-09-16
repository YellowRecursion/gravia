import assert from 'node:assert/strict';
import { test } from 'node:test';
import { setTimeout as delay } from 'node:timers/promises';
import { QueryClient, QueryObserver } from '@tanstack/react-query';
import { wrapComputeMethod } from '@actuallab/fusion';
import { observeSettingsQuery } from '../renderer/shared/api/settings-sync.ts';

async function until(predicate) {
  for (let i = 0; i < 200; i++) {
    if (predicate()) return;
    await delay(5);
  }
  assert.fail('Settings synchronization timed out');
}

test('Host invalidations update independent window caches without polling or touching other scopes', async () => {
  let value = 'system';
  let reads = 0;
  const read = wrapComputeMethod(async () => {
    reads++;
    return value;
  });
  const key = ['settings', 'application', 'gravia.theme'];
  const windows = [new QueryClient(), new QueryClient()].map((client) => {
    client.setQueryData(['settings', 'project', 'gravia.theme'], 'project-value');
    const observer = new QueryObserver(client, {
      queryKey: key,
      queryFn: () => read(),
      staleTime: Infinity,
    });
    const unsubscribe = observer.subscribe(() => {});
    const stop = observeSettingsQuery(client, key, read);
    return { client, unsubscribe, stop };
  });
  try {
    await until(() => windows.every(({ client }) => client.getQueryData(key) === 'system'));
    value = 'light';
    read.invalidate();
    await until(() => windows.every(({ client }) => client.getQueryData(key) === 'light'));
    const calls = reads;
    await delay(30);
    assert.equal(reads, calls);
    for (const { client } of windows)
      assert.equal(client.getQueryData(['settings', 'project', 'gravia.theme']), 'project-value');
    windows[0].stop();
    value = 'dark';
    read.invalidate();
    await until(() => windows[1].client.getQueryData(key) === 'dark');
    assert.equal(windows[0].client.getQueryData(key), 'light');
  } finally {
    for (const { client, unsubscribe, stop } of windows) {
      stop();
      unsubscribe();
      client.clear();
    }
  }
});

test('stopping a pending capture prevents late responses from invalidating a released cache', async () => {
  let release;
  const read = wrapComputeMethod(
    () =>
      new Promise((resolve) => {
        release = resolve;
      }),
  );
  const client = new QueryClient();
  const key = ['settings', 'application', 'gravia.theme'];
  client.setQueryData(key, 'retained');
  const stop = observeSettingsQuery(client, key, read);
  try {
    await until(() => release);
    stop();
    release('late');
    await delay(30);
    assert.equal(client.getQueryState(key).isInvalidated, false);
    assert.equal(client.getQueryData(key), 'retained');
  } finally {
    stop();
    client.clear();
  }
});

test('an invalidation during a pending Query refresh is not lost', async () => {
  let value = 'old';
  let release;
  let block = false;
  const read = wrapComputeMethod(async () => value);
  const key = ['settings', 'application', 'gravia.theme'];
  const client = new QueryClient();
  const observer = new QueryObserver(client, {
    queryKey: key,
    queryFn: async () => {
      const result = await read();
      if (block)
        await new Promise((resolve) => {
          release = resolve;
        });
      return result;
    },
    staleTime: Infinity,
  });
  const unsubscribe = observer.subscribe(() => {});
  const stop = observeSettingsQuery(client, key, read);
  try {
    await until(() => client.getQueryData(key) === 'old');
    block = true;
    value = 'intermediate';
    read.invalidate();
    await until(() => release);
    value = 'latest';
    read.invalidate();
    block = false;
    release();
    await until(() => client.getQueryData(key) === 'latest');
  } finally {
    stop();
    unsubscribe();
    client.clear();
  }
});
