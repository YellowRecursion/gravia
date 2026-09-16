import { theme } from 'antd';
import type { GlobalToken, ThemeConfig } from 'antd';
import type { ColorScheme } from '@/shared/ui';

export const defaultColorScheme: ColorScheme = 'dark';

// Ant derives the complete palettes. Do not seed neutral surfaces with a tinted background.
export const appThemes: Record<ColorScheme, ThemeConfig> = {
  dark: { algorithm: theme.darkAlgorithm, cssVar: { key: 'gravia-dark' } },
  light: { algorithm: theme.defaultAlgorithm, cssVar: { key: 'gravia-light' } },
};

/** Shared surfaces for the window chrome and third-party controls, resolved by Ant. */
export function getThemeVariables(token: GlobalToken) {
  return {
    '--gravia-bg-layout': token.colorBgLayout,
    '--gravia-bg-container': token.colorBgContainer,
    '--gravia-bg-elevated': token.colorBgElevated,
    '--gravia-text': token.colorText,
    '--gravia-text-secondary': token.colorTextSecondary,
    '--gravia-text-tertiary': token.colorTextTertiary,
    '--gravia-border': token.colorBorder,
    '--gravia-border-secondary': token.colorBorderSecondary,
    '--gravia-primary': token.colorPrimary,
    '--gravia-primary-bg': token.colorPrimaryBg,
    '--gravia-primary-border': token.colorPrimaryBorder,
    '--gravia-fill': token.colorFill,
    '--gravia-fill-secondary': token.colorFillSecondary,
    '--gravia-fill-tertiary': token.colorFillTertiary,
    '--gravia-shadow': token.boxShadowSecondary,
    '--gravia-font-size-small': `${token.fontSizeSM}px`,
  };
}
