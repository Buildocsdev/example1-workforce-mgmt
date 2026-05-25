import i18n from 'i18next';
  import { initReactI18next } from 'react-i18next';

  i18n
    .use(initReactI18next)
    .init({
      resources: {
        en: {
          translation: {
            // Your English translations (can be empty for now)
          }
        },
        de: {
          translation: {
            // Your German translations
          }
        },
        et: {
          translation: {
            // Your Estonian translations
          }
        },
        // Add other languages as needed
      },
      lng: 'en-US', // Default language
      fallbackLng: 'en-US',
      interpolation: {
        escapeValue: false, // React already escapes
      },
    });

  export default i18n;