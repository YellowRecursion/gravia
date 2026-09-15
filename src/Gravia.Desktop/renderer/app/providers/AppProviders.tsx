import type { PropsWithChildren } from 'react';
import { useState } from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ConfigProvider } from 'antd';
import enUS from 'antd/locale/en_US';
import ruRU from 'antd/locale/ru_RU';
import { useTranslation } from 'react-i18next';
import { appTheme } from '../config/theme';

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
  const { i18n } = useTranslation();
  // Server cache only. Editor state must not duplicate these responses.
  const [queryClient] = useState(createQueryClient);
  return (
    <QueryClientProvider client={queryClient}>
      <ConfigProvider locale={i18n.resolvedLanguage === 'ru' ? ruRU : enUS} theme={appTheme}>
        {children}
      </ConfigProvider>
    </QueryClientProvider>
  );
}
