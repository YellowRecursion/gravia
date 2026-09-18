import { useLayoutEffect, useRef, useState } from 'react';
import { lowerBound, logRowHeight, visibleLogRange } from './log-projection';
import type { LogRow } from './log-projection';
import { displayMessage } from './log-presentation';
import { LogLevelIcon } from './LogLevelIcon';
import type { LogRevealRequest } from './panel-types';

export function VirtualLogList({
  rows,
  reveal,
  selected,
  follow,
  onFollow,
  onSelect,
  onClearSelection,
  timeFormatter,
  label,
  emptyText,
}: {
  rows: readonly LogRow[];
  reveal?: LogRevealRequest;
  selected: ReadonlySet<number>;
  follow: boolean;
  onFollow: (follow: boolean) => void;
  onSelect: (
    id: number,
    modifiers: { shiftKey: boolean; ctrlKey: boolean; metaKey: boolean },
  ) => void;
  onClearSelection: () => void;
  timeFormatter: Intl.DateTimeFormat;
  label: string;
  emptyText: string;
}) {
  const viewport = useRef<HTMLDivElement>(null);
  const previousRows = useRef(rows);
  const revealedRequest = useRef<string>(undefined);
  const [bounds, setBounds] = useState({ top: 0, height: 0 });
  useLayoutEffect(() => {
    const element = viewport.current!;
    const measure = () => setBounds({ top: element.scrollTop, height: element.clientHeight });
    const observer = new ResizeObserver(measure);
    observer.observe(element);
    return () => observer.disconnect();
  }, []);
  useLayoutEffect(() => {
    const element = viewport.current!;
    if (follow) element.scrollTop = Math.max(0, rows.length * logRowHeight - element.clientHeight);
    else if (previousRows.current !== rows) {
      const anchor = previousRows.current[Math.floor(element.scrollTop / logRowHeight)];
      if (anchor)
        element.scrollTop =
          lowerBound(rows, anchor.id, (row) => row.id) * logRowHeight +
          (element.scrollTop % logRowHeight);
    }
    previousRows.current = rows;
  }, [rows, follow, bounds.height]);
  useLayoutEffect(() => {
    if (!reveal || revealedRequest.current === reveal.requestId || bounds.height === 0) return;
    const index = lowerBound(rows, reveal.sequence, (row) => row.id);
    // The panel's projection can arrive after the navigation request.
    if (rows[index]?.id !== reveal.sequence) return;
    const element = viewport.current!;
    element.scrollTop = Math.max(
      0,
      index * logRowHeight - (element.clientHeight - logRowHeight) / 2,
    );
    element.focus({ preventScroll: true });
    revealedRequest.current = reveal.requestId;
  }, [rows, reveal, bounds.height]);
  const range = visibleLogRange(rows.length, bounds.top, bounds.height);
  return (
    <div
      ref={viewport}
      className="logs-panel__list"
      role="listbox"
      aria-label={label}
      aria-multiselectable="true"
      tabIndex={0}
      onScroll={(event) => {
        const element = event.currentTarget;
        setBounds({ top: element.scrollTop, height: element.clientHeight });
        if (
          follow &&
          element.scrollHeight - element.scrollTop - element.clientHeight > logRowHeight
        )
          onFollow(false);
      }}
      onWheel={(event) => {
        if (event.deltaY < 0) onFollow(false);
      }}
      onKeyDown={(event) => {
        if (['ArrowUp', 'PageUp', 'Home'].includes(event.key)) onFollow(false);
      }}
      onClick={(event) => {
        if (event.target instanceof Element && !event.target.closest('.logs-panel__row'))
          onClearSelection();
      }}
    >
      {rows.length === 0 ? (
        <div className="logs-panel__empty">{emptyText}</div>
      ) : (
        <div style={{ height: rows.length * logRowHeight, position: 'relative' }}>
          {rows.slice(range.start, range.end).map((row, offset) => (
            <div
              key={row.id}
              role="option"
              aria-selected={selected.has(row.id)}
              aria-setsize={rows.length}
              aria-posinset={range.start + offset + 1}
              tabIndex={0}
              data-log-id={row.id}
              className={`logs-panel__row${selected.has(row.id) ? ' logs-panel__row--selected' : ''}`}
              style={{
                position: 'absolute',
                top: (range.start + offset) * logRowHeight,
                height: logRowHeight,
                width: '100%',
              }}
              onClick={(event) => {
                event.currentTarget.focus({ preventScroll: true });
                onSelect(row.id, event);
              }}
              onKeyDown={(event) => {
                if (event.key === 'Enter' || event.key === ' ') {
                  event.preventDefault();
                  onSelect(row.id, event);
                }
              }}
            >
              <LogLevelIcon level={row.entry.level} />
              <div className="logs-panel__line" title={row.entry.message}>
                <span className="logs-panel__time">
                  {timeFormatter.format(new Date(row.entry.timestamp))}
                </span>
                <span className="logs-panel__message">{displayMessage(row.entry)}</span>
                {row.count > 1 && <span className="logs-panel__count">{row.count}</span>}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
