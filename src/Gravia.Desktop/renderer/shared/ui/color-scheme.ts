import { createContext, useContext } from 'react';

export type ColorScheme = 'dark' | 'light';

// App chooses the theme; lower layers only consume its appearance, not application state.
export const ColorSchemeContext = createContext<ColorScheme>('dark');

export function useColorScheme() {
  return useContext(ColorSchemeContext);
}
