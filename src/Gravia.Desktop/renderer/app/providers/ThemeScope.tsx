import type { PropsWithChildren } from 'react';
import { useLayoutEffect } from 'react';
import { theme } from 'antd';
import { getThemeVariables } from '../config/theme';
import { useColorScheme } from '@/shared/ui';
import { setWindowAppearance } from '@/shared/api';

export function ThemeScope({
  children,
  themeSource,
}: PropsWithChildren<{ themeSource: 'system' | 'light' | 'dark' }>) {
  const { token } = theme.useToken();
  const colorScheme = useColorScheme();

  useLayoutEffect(() => {
    const root = document.documentElement;
    const previous = { background: root.style.backgroundColor, scheme: root.style.colorScheme };
    root.style.backgroundColor = token.colorBgLayout;
    root.style.colorScheme = colorScheme;
    void setWindowAppearance({
      colorScheme,
      themeSource,
      backgroundColor: token.colorBgContainer,
      symbolColor: token.colorTextBase,
    }).catch((error: unknown) => console.error('Could not synchronize window appearance.', error));
    return () => {
      root.style.backgroundColor = previous.background;
      root.style.colorScheme = previous.scheme;
    };
  }, [colorScheme, themeSource, token.colorBgLayout, token.colorBgContainer, token.colorTextBase]);

  return (
    <div
      className="app-theme"
      data-color-scheme={colorScheme}
      style={{
        ...getThemeVariables(token),
        colorScheme,
        color: token.colorText,
        background: token.colorBgLayout,
        fontFamily: token.fontFamily,
        fontSize: token.fontSize,
      }}
    >
      {children}
    </div>
  );
}
