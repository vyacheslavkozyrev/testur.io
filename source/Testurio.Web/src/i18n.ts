import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import layoutEn from '@/locales/en/layout.json';
import projectEn from '@/locales/en/project.json';
import projectsEn from '@/locales/en/projects.json';
import pmToolEn from '@/locales/en/pmTool.json';
import reportSettingsEn from '@/locales/en/reportSettings.json';
import projectAccessEn from '@/locales/en/projectAccess.json';

i18n.use(initReactI18next).init({
  lng: 'en',
  fallbackLng: 'en',
  initImmediate: false,
  interpolation: { escapeValue: false },
  resources: {
    en: {
      layout: layoutEn,
      project: projectEn,
      projects: projectsEn,
      pmTool: pmToolEn,
      reportSettings: reportSettingsEn,
      projectAccess: projectAccessEn,
    },
  },
});

export default i18n;
