import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import projectEn from '@/locales/en/project.json';
import pmToolEn from '@/locales/en/pmTool.json';

i18n.use(initReactI18next).init({
  lng: 'en',
  fallbackLng: 'en',
  initImmediate: false,
  interpolation: { escapeValue: false },
  resources: {
    en: {
      project: projectEn,
      pmTool: pmToolEn,
    },
  },
});

export default i18n;
