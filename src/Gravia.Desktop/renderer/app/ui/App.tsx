import { useTranslation } from 'react-i18next';

export function App() {
  const { t } = useTranslation();
  // Compose pages here once the first screen is designed.
  return <main aria-label={t('applicationName')} />;
}
