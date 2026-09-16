import assert from 'node:assert/strict';
import { test } from 'node:test';
import {
  createValueDraft,
  readValueDraft,
  parseValueType,
  valueFits,
  equalValue,
  resolveValueTraits,
} from '../renderer/shared/lib/values.ts';

const type = (kind, extra = {}) => ({ kind, nullable: false, ...extra });
const choice = (...values) => ({
  type: 'choice',
  options: values.map((value) => ({
    value,
    label: { translations: { en: String(value), ru: 'Вариант' } },
  })),
});

test('recursive descriptors and nullable are explicit; unknown shapes are not inferred', () => {
  const schema = type('dictionary', {
    value: type('list', { element: type('bool', { nullable: true }) }),
  });
  assert.deepEqual(parseValueType(schema), schema);
  for (const invalid of [
    null,
    {},
    type('future'),
    { kind: 'string' },
    type('list'),
    type('dictionary', { value: type('future') }),
  ])
    assert.equal(parseValueType(invalid), undefined);
  assert.equal(valueFits(schema, { a: [true, null, false] }), true);
  assert.equal(valueFits(schema, { a: ['true'] }), false);
});

test('unknown, malformed and incompatible traits fall back without discarding compatible choices', () => {
  for (const trait of [
    { type: 'future' },
    null,
    choice(1),
    { type: 'choice', options: [{ value: 'a' }] },
  ])
    assert.equal(resolveValueTraits(type('string'), [trait]).choices, undefined);
  assert.deepEqual(
    resolveValueTraits(type('string'), [choice(1), choice('dark')]).choices,
    choice('dark').options,
  );
  assert.deepEqual(resolveValueTraits(type('string'), [choice()]).choices, []);
});

test('choice values retain their JSON types, including false, zero and null', () => {
  for (const [schema, values] of [
    [type('bool'), [false, true]],
    [type('int'), [0, 1]],
    [type('double', { nullable: true }), [null, 0.5]],
  ]) {
    const traits = [choice(...values)];
    for (const value of values) {
      const draft = createValueDraft(schema, value, traits);
      assert.deepEqual(readValueDraft(schema, draft, traits), { ok: true, value });
    }
  }
  assert.equal(equalValue({ a: true, b: false }, { b: false, a: true }), true);
  assert.equal(equalValue(1, '1'), false);
  assert.equal(equalValue([1, 2], [2, 1]), false);
  assert.deepEqual(
    readValueDraft(type('string'), createValueDraft(type('string'), 'old', [choice('new')]), [
      choice('new'),
    ]),
    { ok: false, error: 'invalidChoice' },
  );
});

test('collection choice is ignored until the contract defines structural choice equality', () => {
  for (const [schema, value] of [
    [type('list', { element: type('int') }), [1, 2]],
    [type('dictionary', { value: type('bool') }), { a: true }],
  ]) {
    const traits = [choice(value)];
    assert.equal(resolveValueTraits(schema, traits).choices, undefined);
    assert.equal(createValueDraft(schema, value, traits).kind, schema.kind);
    assert.deepEqual(readValueDraft(schema, createValueDraft(schema, value, traits), traits), {
      ok: true,
      value,
    });
  }
});

test('drafts preserve edits and reject invalid nested numbers or duplicate keys without mutating the source', () => {
  const schema = type('dictionary', { value: type('list', { element: type('int') }) });
  const value = { a: [1, 2] };
  const draft = createValueDraft(schema, value);
  assert.deepEqual(readValueDraft(schema, draft), { ok: true, value });
  draft.entries.push({ key: 'a', value: { kind: 'list', items: [] } });
  assert.deepEqual(readValueDraft(schema, draft), { ok: false, error: 'duplicateKey' });
  assert.deepEqual(value, { a: [1, 2] });
  for (const bad of [null, 1.5, Infinity, NaN, 2147483648])
    assert.equal(readValueDraft(type('int'), { kind: 'scalar', value: bad }).ok, false);
  assert.equal(readValueDraft(type('double'), { kind: 'scalar', value: 1.5 }).ok, true);
});

test('dictionary keys including __proto__ and empty string survive without prototype mutation', () => {
  const schema = type('dictionary', { value: type('string') });
  const value = JSON.parse('{"__proto__":"safe","":"empty","constructor":"key"}');
  const result = readValueDraft(schema, createValueDraft(schema, value));
  assert.equal(result.ok, true);
  assert.deepEqual(result.value, value);
  assert.equal(Object.getPrototypeOf(result.value), Object.prototype);
});
