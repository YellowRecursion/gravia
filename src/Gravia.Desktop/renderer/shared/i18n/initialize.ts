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
            locationPlaceholder: 'Полный путь к каталогу',
            loading: 'Загрузка проектов',
            sessionError: 'Не удалось получить текущий проект',
            recentError: 'Не удалось загрузить недавние проекты',
            retry: 'Повторить',
            empty: 'Пока нет недавних проектов',
            mainScreen: 'Основной экран',
            switchProject: 'Перейти к проектам',
            switchToProjects: '{{name}} — перейти к проектам',
            errors: {
              desktop_required: 'Выбор каталога доступен в приложении Gravia Lab.',
              unavailable:
                'Не удалось выполнить действие. Проверьте доступность каталога и повторите попытку.',
              validation_failed: 'Проверьте данные проекта.',
              invalid_value: 'Проверьте значение.',
              NotEmptyValidator: 'Заполните это поле.',
              invalid_project_path: 'Укажите корректный полный путь к каталогу.',
              invalid_project_name: 'Укажите допустимое имя папки без запрещённых символов.',
              project_directory_exists: 'Папка или файл с таким именем уже существует.',
              project_directory_not_found: 'Каталог не существует или недоступен.',
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
            locationPlaceholder: 'Absolute directory path',
            loading: 'Loading projects',
            sessionError: 'Could not load the current project',
            recentError: 'Could not load recent projects',
            retry: 'Retry',
            empty: 'No recent projects yet',
            mainScreen: 'Main screen',
            switchProject: 'Go to projects',
            switchToProjects: '{{name}} — go to projects',
            errors: {
              desktop_required: 'The directory picker is available in the Gravia Lab desktop app.',
              unavailable: 'Could not complete the action. Check directory access and try again.',
              validation_failed: 'Check the project details.',
              invalid_value: 'Check this value.',
              NotEmptyValidator: 'Fill in this field.',
              invalid_project_path: 'Enter a valid absolute directory path.',
              invalid_project_name: 'Enter a valid folder name without forbidden characters.',
              project_directory_exists: 'A file or folder with this name already exists.',
              project_directory_not_found: 'The directory does not exist or is unavailable.',
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
