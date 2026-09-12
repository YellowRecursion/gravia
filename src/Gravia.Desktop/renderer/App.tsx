import { useTranslation } from 'react-i18next';

export function App() {
  const { t } = useTranslation();
  // The first screen and editor UX have not been designed yet.
  return <main aria-label={t('applicationName')} />;
}
