const test = require('node:test');
const assert = require('node:assert/strict');
const { parseBootstrap } = require('../bootstrap.cjs');

test('accepts the actual loopback address supplied by Host', () => {
  assert.deepEqual(parseBootstrap('{"backendUrl":"http://127.0.0.1:12345"}'), {
    backendUrl: 'http://127.0.0.1:12345/',
  });
});

for (const backendUrl of [
  'https://example.com:1234/',
  'http://0.0.0.0:1234/',
  'http://127.0.0.1/',
  'http://user:password@127.0.0.1:1234/',
  'http://127.0.0.1:1234/path',
  'http://127.0.0.1:1234/?value=1',
  'http://127.0.0.1:1234/#fragment',
  'file:///tmp/index.html',
]) {
  test(`rejects an invalid backend address: ${backendUrl}`, () => {
    assert.throws(() => parseBootstrap(JSON.stringify({ backendUrl })));
  });
}

test('rejects malformed or missing bootstrap data', () => {
  for (const input of ['{', '{}', 'null']) assert.throws(() => parseBootstrap(input));
});
