import assert from 'node:assert/strict';
import { test } from 'node:test';
import { selectLog } from '../renderer/pages/main/ui/log-selection.ts';

const order = [1, 2, 3, 4, 5, 6];
const click = (state, id, modifiers = {}) =>
  selectLog(state, order, id, { shiftKey: false, ctrlKey: false, metaKey: false, ...modifiers });

test('Ctrl removes selected entries and clears the anchor when selection becomes empty', () => {
  const state = click({ ids: [2, 4], anchor: 4 }, 4, { ctrlKey: true });
  assert.deepEqual(state, { ids: [2], anchor: 2 });
  assert.deepEqual(click(state, 2, { ctrlKey: true }), { ids: [], anchor: null });
});

test('repeated plain click clears a single selection; clicking a group member selects only it', () => {
  assert.deepEqual(click({ ids: [2], anchor: 2 }, 2), { ids: [], anchor: null });
  assert.deepEqual(click({ ids: [2, 4], anchor: 4 }, 2), { ids: [2], anchor: 2 });
});

test('Shift extends from a selected anchor, including after deselection or clearing', () => {
  const state = click({ ids: [2, 4], anchor: 4 }, 4, { ctrlKey: true });
  assert.deepEqual(click(state, 5, { shiftKey: true }), { ids: [2, 3, 4, 5], anchor: 2 });
  assert.deepEqual(click({ ids: [], anchor: null }, 5, { shiftKey: true }), {
    ids: [5],
    anchor: 5,
  });
});

test('Cmd toggles and Ctrl+Shift adds a range without duplicates', () => {
  assert.deepEqual(click({ ids: [2], anchor: 2 }, 4, { metaKey: true }), {
    ids: [2, 4],
    anchor: 4,
  });
  assert.deepEqual(click({ ids: [1, 4], anchor: 4 }, 2, { ctrlKey: true, shiftKey: true }), {
    ids: [1, 4, 2, 3],
    anchor: 4,
  });
});
