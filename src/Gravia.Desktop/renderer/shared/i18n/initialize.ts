import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';

export async function initializeI18n() {
  await i18n.use(initReactI18next).init({
    lng: 'ru',
    fallbackLng: 'en',
    supportedLngs: ['ru', 'en'],
    interpolation: { escapeValue: false },
    resources: {
      ru: {
        translation: {
          applicationName: 'Gravia Lab',
          unexpectedError: {
            title: 'Интерфейс не смог продолжить работу',
            description:
              'Перезапустите интерфейс. Если ошибка повторится, сообщите её разработчику.',
            reloadAction: 'Перезапустить интерфейс',
          },
        },
      },
      en: {
        translation: {
          applicationName: 'Gravia Lab',
          unexpectedError: {
            title: 'The interface could not continue',
            description: 'Reload the interface. If the error repeats, report it to the developer.',
            reloadAction: 'Reload interface',
          },
        },
      },
    },
  });
}
