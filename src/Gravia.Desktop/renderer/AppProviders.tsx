import type { PropsWithChildren } from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ConfigProvider, theme } from 'antd';
import enUS from 'antd/locale/en_US';
import ruRU from 'antd/locale/ru_RU';
import { useTranslation } from 'react-i18next';

const queryClient = new QueryClient();

export function AppProviders({ children }: PropsWithChildren) {
  const { i18n } = useTranslation();
  return (
    <QueryClientProvider client={queryClient}>
      <ConfigProvider
        locale={i18n.resolvedLanguage === 'ru' ? ruRU : enUS}
        theme={{ algorithm: theme.darkAlgorithm }}
      >
        {children}
      </ConfigProvider>
    </QueryClientProvider>
  );
}
