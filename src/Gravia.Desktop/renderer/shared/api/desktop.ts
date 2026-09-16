export type SettingsScope = 'application' | 'project';

export interface WindowAppearance {
  readonly colorScheme: 'dark' | 'light';
  readonly themeSource: 'system' | 'dark' | 'light';
  readonly backgroundColor: string;
  readonly symbolColor: string;
}

declare global {
  interface Window {
    readonly graviaDesktop?: {
      readonly chooseDirectory: () => Promise<string | null>;
      readonly openSettings: (scope: SettingsScope) => Promise<void>;
      readonly setWindowAppearance: (appearance: WindowAppearance) => Promise<void>;
    };
  }
}

export async function setWindowAppearance(appearance: WindowAppearance): Promise<void> {
  // Browser previews have no native chrome to synchronize.
  await window.graviaDesktop?.setWindowAppearance(appearance);
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

export function openSettings(scope: SettingsScope): Promise<void> {
  if (!window.graviaDesktop) {
    return Promise.reject(new DesktopUnavailableError('Settings windows require the desktop app.'));
  }
  return window.graviaDesktop.openSettings(scope);
}
