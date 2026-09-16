export interface LogSelection {
  ids: number[];
  anchor: number | null;
}

export function selectLog(
  previous: LogSelection,
  order: number[],
  sequence: number,
  modifiers: { shiftKey: boolean; ctrlKey: boolean; metaKey: boolean },
): LogSelection {
  const additive = modifiers.ctrlKey || modifiers.metaKey;
  const anchorIndex = previous.anchor === null ? -1 : order.indexOf(previous.anchor);
  const targetIndex = order.indexOf(sequence);
  if (targetIndex < 0) return previous;
  if (modifiers.shiftKey && anchorIndex >= 0) {
    const range = order.slice(
      Math.min(anchorIndex, targetIndex),
      Math.max(anchorIndex, targetIndex) + 1,
    );
    return {
      anchor: previous.anchor,
      ids: additive ? [...new Set([...previous.ids, ...range])] : range,
    };
  }
  const ids = additive
    ? previous.ids.includes(sequence)
      ? previous.ids.filter((id) => id !== sequence)
      : [...previous.ids, sequence]
    : previous.ids.length === 1 && previous.ids[0] === sequence
      ? []
      : [sequence];
  return { ids, anchor: ids.includes(sequence) ? sequence : (ids.at(-1) ?? null) };
}
