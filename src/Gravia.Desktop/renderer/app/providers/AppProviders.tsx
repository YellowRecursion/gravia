import type { PropsWithChildren } from 'react';
import { useEffect, useState, useSyncExternalStore } from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ConfigProvider } from 'antd';
import enUS from 'antd/locale/en_US';
import ruRU from 'antd/locale/ru_RU';
import { useTranslation } from 'react-i18next';
import { appThemes, defaultColorScheme } from '../config/theme';
import { ThemeScope } from './ThemeScope';
import { ColorSchemeContext } from '@/shared/ui';
import type { ColorScheme } from '@/shared/ui';
import { languagePreference, themePreference, useSetting } from '@/entities/settings';

function createQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: {
        // The Host is loopback-only and remains reachable when the machine is offline.
        networkMode: 'always',
        refetchOnWindowFocus: false,
      },
      mutations: { networkMode: 'always' },
    },
  });
}

export function AppProviders({ children }: PropsWithChildren) {
  // Server cache only. Editor state must not duplicate these responses.
  const [queryClient] = useState(createQueryClient);
  return (
    <QueryClientProvider client={queryClient}>
      <AppearanceProvider>{children}</AppearanceProvider>
    </QueryClientProvider>
  );
}

function subscribeSystemTheme(notify: () => void) {
  const media = window.matchMedia('(prefers-color-scheme: dark)');
  media.addEventListener('change', notify);
  return () => media.removeEventListener('change', notify);
}

function AppearanceProvider({ children }: PropsWithChildren) {
  const { i18n } = useTranslation();
  const themeSetting = useSetting('application', 'gravia.theme');
  const languageSetting = useSetting('application', 'gravia.language');
  const preference = themePreference(themeSetting.data?.value);
  const systemDark = useSyncExternalStore(
    subscribeSystemTheme,
    () => window.matchMedia('(prefers-color-scheme: dark)').matches,
  );
  const colorScheme: ColorScheme = themeSetting.data
    ? preference === 'system'
      ? systemDark
        ? 'dark'
        : 'light'
      : preference
    : defaultColorScheme;
  const language = languagePreference(languageSetting.data?.value);
  useEffect(() => {
    if (i18n.resolvedLanguage !== language) void i18n.changeLanguage(language);
    document.documentElement.lang = language;
  }, [i18n, language]);
  return (
    <ConfigProvider
      locale={i18n.resolvedLanguage === 'ru' ? ruRU : enUS}
      theme={appThemes[colorScheme]}
    >
      <ColorSchemeContext.Provider value={colorScheme}>
        <ThemeScope themeSource={preference}>{children}</ThemeScope>
      </ColorSchemeContext.Provider>
    </ConfigProvider>
  );
}
