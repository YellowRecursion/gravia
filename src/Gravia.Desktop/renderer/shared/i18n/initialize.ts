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
          projects: {
            recentTitle: 'Недавние проекты',
            createTitle: 'Создать проект',
            openOther: 'Открыть другой проект…',
            nameLabel: 'Название',
            namePlaceholder: 'Новый проект',
            locationLabel: 'Расположение',
            locationHint: 'В выбранном каталоге будет создана папка с названием проекта.',
            browseLocation: 'Выбрать каталог',
            createAction: 'Создать проект',
            examples: {
              refinery: 'Оптимизация НПЗ',
              avt: 'Установка АВТ',
              blending: 'Смешение бензинов',
            },
          },
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
          projects: {
            recentTitle: 'Recent projects',
            createTitle: 'Create project',
            openOther: 'Open another project…',
            nameLabel: 'Name',
            namePlaceholder: 'New project',
            locationLabel: 'Location',
            locationHint: 'A folder named after the project will be created in this directory.',
            browseLocation: 'Choose directory',
            createAction: 'Create project',
            examples: {
              refinery: 'Refinery optimization',
              avt: 'Crude distillation unit',
              blending: 'Gasoline blending',
            },
          },
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
