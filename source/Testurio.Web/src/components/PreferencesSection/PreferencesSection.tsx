'use client';

import { useState, useCallback, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Typography from '@mui/material/Typography';
import FormControl from '@mui/material/FormControl';
import InputLabel from '@mui/material/InputLabel';
import Select from '@mui/material/Select';
import MenuItem from '@mui/material/MenuItem';
import ToggleButton from '@mui/material/ToggleButton';
import ToggleButtonGroup from '@mui/material/ToggleButtonGroup';
import CircularProgress from '@mui/material/CircularProgress';
import Alert from '@mui/material/Alert';
import { useTheme, type Theme } from '@mui/material/styles';
import i18n from '@/i18n';
import { useThemeMode } from '@/theme/ThemeContext';
import { useUpdatePreferences } from '@/hooks/useAccount';
import type { AccountPreferencesDto, ThemeMode, SupportedLanguage } from '@/types/account.types';

export interface PreferencesSectionProps {
  preferences: AccountPreferencesDto | null;
  onSaveSuccess: () => void;
}

const SUPPORTED_LANGUAGES: Array<{ value: SupportedLanguage; label: string }> = [
  { value: 'en', label: 'English' },
  { value: 'uk', label: 'Українська' },
];

function detectBrowserLanguage(): SupportedLanguage {
  const browserLang = navigator.language.split('-')[0];
  return browserLang === 'uk' ? 'uk' : 'en';
}

export default function PreferencesSection({ preferences, onSaveSuccess }: PreferencesSectionProps) {
  const { t } = useTranslation('settings');
  const theme = useTheme();
  const styles = getStyles(theme);
  const { themeMode, setThemeMode } = useThemeMode();

  const resolveInitialLanguage = (): SupportedLanguage => {
    if (preferences?.language === 'en' || preferences?.language === 'uk') {
      return preferences.language;
    }
    try {
      return detectBrowserLanguage();
    } catch {
      return 'en';
    }
  };

  const [language, setLanguage] = useState<SupportedLanguage>(resolveInitialLanguage);
  const [previousTheme, setPreviousTheme] = useState<ThemeMode>(themeMode);
  const [saveError, setSaveError] = useState(false);

  const updatePreferences = useUpdatePreferences();

  const handleThemeChange = useCallback(
    (_: React.MouseEvent<HTMLElement>, value: ThemeMode | null) => {
      if (value === null) return; // ToggleButtonGroup requires at least one selected
      setThemeMode(value);
      setSaveError(false);
    },
    [setThemeMode],
  );

  const handleLanguageChange = useCallback((value: SupportedLanguage) => {
    setLanguage(value);
    setSaveError(false);
  }, []);

  const handleSave = useCallback(async () => {
    setSaveError(false);
    const prevTheme = previousTheme;
    const prevLang = i18n.language as SupportedLanguage;

    try {
      await updatePreferences.mutateAsync({ language, theme: themeMode });
      // Persist language preference in localStorage for optimistic restore on next page load
      try {
        localStorage.setItem('testurio.language', language);
      } catch {
        // localStorage unavailable — silently ignore
      }
      // Apply language change immediately so portal switches without reload
      await i18n.changeLanguage(language);
      setPreviousTheme(themeMode);
      onSaveSuccess();
    } catch {
      // Roll back optimistic theme and language changes on failure
      setThemeMode(prevTheme);
      await i18n.changeLanguage(prevLang);
      setSaveError(true);
    }
  }, [language, themeMode, previousTheme, updatePreferences, setThemeMode, onSaveSuccess]);

  const isPending = updatePreferences.isPending;

  return (
    <Box sx={styles.root}>
      <Typography variant="h6" sx={styles.sectionTitle}>
        {t('preferences.title')}
      </Typography>

      <Box sx={styles.fields}>
        <FormControl fullWidth sx={styles.languageControl}>
          <InputLabel id="language-label">{t('preferences.fields.language')}</InputLabel>
          <Select
            labelId="language-label"
            value={language}
            label={t('preferences.fields.language')}
            disabled={isPending}
            onChange={(e) => handleLanguageChange(e.target.value as SupportedLanguage)}
          >
            {SUPPORTED_LANGUAGES.map(({ value, label }) => (
              <MenuItem key={value} value={value}>
                {label}
              </MenuItem>
            ))}
          </Select>
        </FormControl>

        <Box sx={styles.themeField}>
          <Typography variant="body2" sx={styles.themeLabel}>
            {t('preferences.fields.appearance')}
          </Typography>
          <ToggleButtonGroup
            value={themeMode}
            exclusive
            onChange={handleThemeChange}
            aria-label={t('preferences.fields.appearance')}
            disabled={isPending}
          >
            <ToggleButton value="light" aria-label={t('preferences.theme.light')}>
              {t('preferences.theme.light')}
            </ToggleButton>
            <ToggleButton value="dark" aria-label={t('preferences.theme.dark')}>
              {t('preferences.theme.dark')}
            </ToggleButton>
          </ToggleButtonGroup>
        </Box>

        {saveError && (
          <Alert severity="error">
            {t('preferences.errors.saveFailed')}
          </Alert>
        )}

        <Button
          variant="contained"
          onClick={handleSave}
          disabled={isPending}
          startIcon={isPending ? <CircularProgress size={16} color="inherit" /> : undefined}
          sx={styles.saveButton}
        >
          {isPending ? t('preferences.actions.saving') : t('preferences.actions.save')}
        </Button>
      </Box>
    </Box>
  );
}

// co-located at the bottom of the file
const getStyles = (theme: Theme) =>
  // eslint-disable-next-line react-hooks/rules-of-hooks
  useMemo(
    () => ({
      root: {
        display: 'flex',
        flexDirection: 'column',
        gap: theme.spacing(2),
      },
      sectionTitle: {
        color: theme.palette.text.primary,
      },
      fields: {
        display: 'flex',
        flexDirection: 'column',
        gap: theme.spacing(2),
        maxWidth: 480,
      },
      languageControl: {},
      themeField: {
        display: 'flex',
        flexDirection: 'column',
        gap: theme.spacing(1),
      },
      themeLabel: {
        color: theme.palette.text.secondary,
      },
      saveButton: {
        alignSelf: 'flex-start',
      },
    }),
    [theme],
  );
