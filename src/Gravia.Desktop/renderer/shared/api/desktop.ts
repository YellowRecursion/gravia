declare global {
  interface Window {
    readonly graviaDesktop?: {
      readonly chooseDirectory: () => Promise<string | null>;
    };
  }
}

export class DesktopUnavailableError extends Error {}

export function chooseDirectory(): Promise<string | null> {
  if (!window.graviaDesktop) {
    return Promise.reject(
      new DesktopUnavailableError('Directory picker requires the desktop app.'),
    );
  }
  return window.graviaDesktop.chooseDirectory();
}
