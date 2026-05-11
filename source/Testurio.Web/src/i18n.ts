import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import projectEn from '@/locales/en/project.json';

i18n.use(initReactI18next).init({
  lng: 'en',
  fallbackLng: 'en',
  interpolation: { escapeValue: false },
  resources: {
    en: {
      project: projectEn,
    },
  },
});

export default i18n;
