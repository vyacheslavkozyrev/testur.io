import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import layoutEn from '@/locales/en/layout.json';
import projectEn from '@/locales/en/project.json';
import projectsEn from '@/locales/en/projects.json';
import pmToolEn from '@/locales/en/pmTool.json';
import reportSettingsEn from '@/locales/en/reportSettings.json';
import projectAccessEn from '@/locales/en/projectAccess.json';
import projectApiAuthEn from '@/locales/en/projectApiAuth.json';
import dashboardEn from '@/locales/en/dashboard.json';
import historyEn from '@/locales/en/history.json';
import authEn from '@/locales/en/auth.json';
import landingEn from '@/locales/en/landing.json';
import pricingEn from '@/locales/en/pricing.json';
import settingsEn from '@/locales/en/settings.json';
import settingsUk from '@/locales/uk/settings.json';
import settingsEs from '@/locales/es/settings.json';
import settingsBe from '@/locales/be/settings.json';

const SUPPORTED_LANGUAGES = ['en', 'uk', 'es', 'be'] as const;

i18n.use(initReactI18next).init({
  lng: (() => {
    try {
      const stored = localStorage.getItem('testurio.language');
      if (stored && (SUPPORTED_LANGUAGES as readonly string[]).includes(stored)) return stored;
    } catch {
      // localStorage unavailable
    }
    return 'en';
  })(),
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
      projectApiAuth: projectApiAuthEn,
      dashboard: dashboardEn,
      history: historyEn,
      auth: authEn,
      landing: landingEn,
      pricing: pricingEn,
      settings: settingsEn,
    },
    uk: {
      settings: settingsUk,
    },
    es: {
      settings: settingsEs,
    },
    be: {
      settings: settingsBe,
    },
  },
});

export default i18n;
