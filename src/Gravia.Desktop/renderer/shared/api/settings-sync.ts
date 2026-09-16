import { Computed } from '@actuallab/fusion';
import type { QueryClient, QueryKey } from '@tanstack/react-query';

// Fusion owns change notifications; Query owns server responses and request errors.
// Each mounted consumer releases its watcher, including a pending capture, on unmount.
export function observeSettingsQuery<T>(
  client: QueryClient,
  key: QueryKey,
  read: () => Promise<T>,
) {
  const controller = new AbortController();
  const { signal } = controller;
  void (async () => {
    while (!signal.aborted) {
      let computed: Computed<T> | undefined;
      try {
        computed = await abortable(Computed.capture(read), signal);
        if (signal.aborted) return;
        // Also refresh on first capture: the initial Query read may precede subscription.
        await client.invalidateQueries({ queryKey: key, exact: true });
        await computed.whenInvalidated(signal);
      } catch {
        if (signal.aborted) return;
        computed?.invalidate();
        // Failure retry only, never polling a healthy Host.
        await new Promise<void>((resolve) => {
          const done = () => {
            clearTimeout(timer);
            signal.removeEventListener('abort', done);
            resolve();
          };
          const timer = setTimeout(done, 1000);
          signal.addEventListener('abort', done, { once: true });
          if (signal.aborted) done();
        });
      }
    }
  })();
  return () => controller.abort();
}

function abortable<T>(promise: Promise<T>, signal: AbortSignal): Promise<T> {
  return new Promise<T>((resolve, reject) => {
    const cancel = () => reject(new Error('Settings observation stopped.'));
    signal.addEventListener('abort', cancel, { once: true });
    if (signal.aborted) cancel();
    void promise.then(resolve, reject).finally(() => signal.removeEventListener('abort', cancel));
  });
}
