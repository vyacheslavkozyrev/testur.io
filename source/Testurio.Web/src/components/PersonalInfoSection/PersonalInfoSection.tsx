'use client';

import { useState, useCallback, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import CircularProgress from '@mui/material/CircularProgress';
import Alert from '@mui/material/Alert';
import { useTheme, type Theme } from '@mui/material/styles';
import { useUpdateProfile } from '@/hooks/useAccount';
import type { AuthUser } from '@/types/layout.types';

export interface PersonalInfoSectionProps {
  user: AuthUser;
  onSaveSuccess: () => void;
}

export default function PersonalInfoSection({ user, onSaveSuccess }: PersonalInfoSectionProps) {
  const { t } = useTranslation('settings');
  const theme = useTheme();
  const styles = getStyles(theme);

  const [displayName, setDisplayName] = useState(user.displayName ?? '');
  const [validationError, setValidationError] = useState<string | null>(null);
  const [saveError, setSaveError] = useState(false);

  const updateProfile = useUpdateProfile();

  const handleChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    setDisplayName(e.target.value);
    setValidationError(null);
    setSaveError(false);
  }, []);

  const handleSave = useCallback(async () => {
    const trimmed = displayName.trim();

    if (!trimmed) {
      setValidationError(t('personalInfo.validation.displayNameRequired'));
      return;
    }
    if (trimmed.length > 100) {
      setValidationError(t('personalInfo.validation.displayNameMaxLength'));
      return;
    }

    setSaveError(false);
    try {
      await updateProfile.mutateAsync({ displayName: trimmed });
      onSaveSuccess();
    } catch {
      setSaveError(true);
    }
  }, [displayName, t, updateProfile, onSaveSuccess]);

  const isPending = updateProfile.isPending;

  return (
    <Box sx={styles.root}>
      <Typography variant="h6" sx={styles.sectionTitle}>
        {t('personalInfo.title')}
      </Typography>
      <Box sx={styles.form}>
        <TextField
          label={t('personalInfo.fields.displayName')}
          value={displayName}
          onChange={handleChange}
          disabled={isPending}
          error={Boolean(validationError)}
          helperText={validationError ?? undefined}
          fullWidth
          inputProps={{ maxLength: 101 }}
        />
        {saveError && (
          <Alert severity="error" sx={styles.alert}>
            {t('personalInfo.errors.saveFailed')}
          </Alert>
        )}
        <Button
          variant="contained"
          onClick={handleSave}
          disabled={isPending}
          startIcon={isPending ? <CircularProgress size={16} color="inherit" /> : undefined}
          sx={styles.saveButton}
        >
          {isPending ? t('personalInfo.actions.saving') : t('personalInfo.actions.save')}
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
      form: {
        display: 'flex',
        flexDirection: 'column',
        gap: theme.spacing(2),
        maxWidth: 480,
      },
      alert: {},
      saveButton: {
        alignSelf: 'flex-start',
      },
    }),
    [theme],
  );
