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

  const [firstName, setFirstName] = useState(user.firstName ?? '');
  const [lastName, setLastName] = useState(user.lastName ?? '');
  const [saveError, setSaveError] = useState(false);
  const [firstNameError, setFirstNameError] = useState(false);

  const updateProfile = useUpdateProfile();

  const handleFirstNameChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    setFirstName(e.target.value);
    setSaveError(false);
    setFirstNameError(false);
  }, []);

  const handleLastNameChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    setLastName(e.target.value);
    setSaveError(false);
  }, []);

  const handleSave = useCallback(async () => {
    if (firstName.trim() === '') {
      setFirstNameError(true);
      return;
    }
    setSaveError(false);
    try {
      await updateProfile.mutateAsync({
        firstName: firstName.trim() || undefined,
        lastName: lastName.trim() || undefined,
      });
      onSaveSuccess();
    } catch {
      setSaveError(true);
    }
  }, [firstName, lastName, updateProfile, onSaveSuccess]);

  const isPending = updateProfile.isPending;

  return (
    <Box sx={styles.root}>
      <Typography variant="h6" sx={styles.sectionTitle}>
        {t('personalInfo.title')}
      </Typography>
      <Box sx={styles.form}>
        <Box sx={styles.nameRow}>
          <TextField
            label={t('personalInfo.fields.firstName')}
            value={firstName}
            onChange={handleFirstNameChange}
            disabled={isPending}
            fullWidth
            autoComplete="given-name"
            inputProps={{ maxLength: 100 }}
            error={firstNameError}
            helperText={firstNameError ? t('personalInfo.errors.firstNameRequired') : undefined}
          />
          <TextField
            label={t('personalInfo.fields.lastName')}
            value={lastName}
            onChange={handleLastNameChange}
            disabled={isPending}
            fullWidth
            autoComplete="family-name"
            inputProps={{ maxLength: 100 }}
          />
        </Box>
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
      nameRow: {
        display: 'flex',
        gap: theme.spacing(2),
      },
      alert: {},
      saveButton: {
        alignSelf: 'flex-start',
      },
    }),
    [theme],
  );
