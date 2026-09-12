import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';

export async function initializeI18n() {
  await i18n.use(initReactI18next).init({
    lng: 'ru',
    fallbackLng: 'en',
    supportedLngs: ['ru', 'en'],
    interpolation: { escapeValue: false },
    resources: {
      ru: { translation: { applicationName: 'Gravia Lab' } },
      en: { translation: { applicationName: 'Gravia Lab' } },
    },
  });
}
