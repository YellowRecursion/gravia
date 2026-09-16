import type { LogEntry, LogJournalSnapshot } from '@/shared/api';

export interface LogRow {
  readonly id: number;
  readonly entry: LogEntry;
  readonly count: number;
}
export interface LogViewOptions {
  readonly search: string;
  readonly levels: number;
  readonly collapse: boolean;
}
export interface LogView {
  readonly rows: readonly LogRow[];
  readonly counts: readonly number[];
  readonly journalId: string | undefined;
  readonly error: string | null;
}

// Shared weak metadata: expensive text/key construction happens once per immutable record,
// even with multiple panels. Evicted records are not retained by this cache.
const metadata = new WeakMap<LogEntry, { key: string; search: string }>();
function describe(entry: LogEntry) {
  let result = metadata.get(entry);
  if (!result) {
    const properties = Object.entries(entry.properties).sort(([a], [b]) => a.localeCompare(b));
    const key = JSON.stringify([
      entry.level,
      entry.category,
      entry.message,
      entry.eventId.id,
      entry.eventId.name,
      entry.exception,
      properties,
      entry.isTruncated,
    ]);
    result = {
      key,
      search: [
        entry.message,
        entry.category,
        entry.exception,
        entry.eventId.name,
        entry.eventId.id,
        ...properties.flat(),
      ]
        .join('\n')
        .toLocaleLowerCase(),
    };
    metadata.set(entry, result);
  }
  return result;
}

export function lowerBound<T>(items: readonly T[], value: number, id: (item: T) => number) {
  let lo = 0;
  let hi = items.length;
  while (lo < hi) {
    const mid = (lo + hi) >>> 1;
    if (id(items[mid]!) < value) lo = mid + 1;
    else hi = mid;
  }
  return lo;
}

/** One selector per panel. Only additions/evictions are processed while filters stay unchanged. */
export function createLogProjection() {
  let previous: LogJournalSnapshot | undefined;
  let configuration = '';
  let counts = [0, 0, 0, 0, 0, 0];
  const groups = new Map<string | number, LogRow>();
  let result: LogView = { rows: [], counts, journalId: undefined, error: null };
  function* steps(snapshot: LogJournalSnapshot, options: LogViewOptions): Generator<void, LogView> {
    const search = options.search.trim().toLocaleLowerCase();
    const signature = JSON.stringify([search, options.levels, options.collapse]);
    if (previous === snapshot && configuration === signature) return result;
    const reset =
      configuration !== signature ||
      previous?.state?.journalId !== snapshot.state?.journalId ||
      snapshot.entries.length === 0;
    if (reset) {
      groups.clear();
      counts = [0, 0, 0, 0, 0, 0];
    }
    const matches = (entry: LogEntry) =>
      (options.levels & (1 << entry.level)) !== 0 &&
      (!search || describe(entry).search.includes(search));
    const keyOf = (entry: LogEntry) => (options.collapse ? describe(entry).key : entry.sequence);
    let visited = 0;
    if (!reset && previous) {
      const first = snapshot.state?.firstSequence ?? Infinity;
      for (const entry of previous.entries) {
        if (entry.sequence >= first) break;
        if (++visited % 128 === 0) yield;
        counts[entry.level]!--;
        if (!matches(entry)) continue;
        const key = keyOf(entry);
        const group = groups.get(key)!;
        if (group.count === 1) groups.delete(key);
        else groups.set(key, { ...group, count: group.count - 1 });
      }
    }
    const start =
      reset || !previous
        ? 0
        : lowerBound(
            snapshot.entries,
            (previous.state?.lastSequence ?? 0) + 1,
            (entry) => entry.sequence,
          );
    for (let i = start; i < snapshot.entries.length; i++) {
      if (++visited % 128 === 0) yield;
      const entry = snapshot.entries[i]!;
      counts[entry.level]!++;
      if (!matches(entry)) continue;
      const key = keyOf(entry);
      const group = groups.get(key);
      groups.set(key, { id: group?.id ?? entry.sequence, entry, count: (group?.count ?? 0) + 1 });
    }
    result = {
      rows: [...groups.values()],
      counts: [...counts],
      journalId: snapshot.state?.journalId,
      error: snapshot.error,
    };
    previous = snapshot;
    configuration = signature;
    return result;
  }
  const select = (snapshot: LogJournalSnapshot, options: LogViewOptions) => {
    const work = steps(snapshot, options);
    let next = work.next();
    while (!next.done) next = work.next();
    return next.value;
  };
  return Object.assign(select, { steps });
}

/** Latest-only pending snapshot; UI indexing yields to input/paint rather than blocking a frame. */
export function createLogViewStore(options: LogViewOptions) {
  const listeners = new Set<() => void>();
  let value: LogView | undefined;
  let latest: LogJournalSnapshot | undefined;
  let generation = 0;
  let busy = false;
  let project = createLogProjection();
  async function run() {
    if (busy || !listeners.size || !latest) return;
    busy = true;
    const epoch = generation;
    try {
      while (latest && listeners.size && epoch === generation) {
        const input: LogJournalSnapshot = latest;
        const work = project.steps(input, options);
        let start = performance.now();
        let step = work.next();
        while (!step.done) {
          if (performance.now() - start >= 4) {
            await new Promise<void>((resolve) => setTimeout(resolve, 0));
            if (epoch !== generation || !listeners.size) return;
            start = performance.now();
          }
          step = work.next();
        }
        // Do not briefly restore stale rows if Clear/restart arrived during a long initial pass.
        if (latest !== input) continue;
        value = step.value;
        for (const listener of listeners) listener();
        if (latest === input) break;
      }
    } finally {
      busy = false;
      if (epoch !== generation && listeners.size) void run();
    }
  }
  return {
    getSnapshot: () => value,
    subscribe: (listener: () => void) => {
      if (!listeners.size) {
        generation++;
        project = createLogProjection();
      }
      listeners.add(listener);
      void run();
      return () => {
        listeners.delete(listener);
        if (!listeners.size) generation++;
      };
    },
    offer(snapshot: LogJournalSnapshot) {
      latest = snapshot;
      void run();
    },
  };
}

export const logRowHeight = 21;
export function visibleLogRange(count: number, top: number, height: number) {
  const start = Math.max(0, Math.min(count, Math.floor(top / logRowHeight) - 8));
  return { start, end: Math.min(count, Math.ceil((top + height) / logRowHeight) + 8) };
}
