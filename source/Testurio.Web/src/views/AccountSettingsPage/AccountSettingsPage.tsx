'use client';

import { useState, useCallback, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Skeleton from '@mui/material/Skeleton';
import Snackbar from '@mui/material/Snackbar';
import Alert from '@mui/material/Alert';
import Divider from '@mui/material/Divider';
import { useTheme, type Theme } from '@mui/material/styles';
import { useQuery } from '@tanstack/react-query';
import { authService } from '@/services/auth/authService';
import { AUTH_KEYS } from '@/hooks/useAuth';
import { useAccountPreferences } from '@/hooks/useAccount';
import PersonalInfoSection from '@/components/PersonalInfoSection/PersonalInfoSection';
import PreferencesSection from '@/components/PreferencesSection/PreferencesSection';
import type { AuthUser } from '@/types/layout.types';
import type { ApiError } from '@/types/api.types';

export default function AccountSettingsPage() {
  const { t } = useTranslation('settings');
  const theme = useTheme();
  const styles = getStyles(theme);
  const [successOpen, setSuccessOpen] = useState(false);

  const {
    data: user,
    isPending: isUserPending,
  } = useQuery<AuthUser | null>({
    queryKey: AUTH_KEYS.me,
    queryFn: authService.getSession,
    staleTime: 5 * 60 * 1000,
  });

  const {
    data: preferences,
    isPending: isPrefsPending,
    isError: isPrefsError,
    error: prefsError,
  } = useAccountPreferences();

  const handleSaveSuccess = useCallback(() => {
    setSuccessOpen(true);
  }, []);

  const handleSnackbarClose = useCallback(() => {
    setSuccessOpen(false);
  }, []);

  const isLoading = isUserPending || isPrefsPending;

  // 404 on preferences is expected for new users — not an error
  const isPrefsLoadError = isPrefsError && (prefsError as ApiError)?.status !== 404;

  return (
    <Box sx={styles.root}>
      <Typography variant="h5" sx={styles.pageTitle}>
        {t('page.title')}
      </Typography>

      <Box sx={styles.sections}>
        {/* Personal Information */}
        {isLoading ? (
          <Box sx={styles.skeletonSection}>
            <Skeleton variant="text" width={200} height={32} />
            <Skeleton variant="rounded" height={56} />
            <Skeleton variant="rounded" width={100} height={36} />
          </Box>
        ) : user ? (
          <PersonalInfoSection user={user} onSaveSuccess={handleSaveSuccess} />
        ) : null}

        <Divider />

        {/* Preferences */}
        {isLoading ? (
          <Box sx={styles.skeletonSection}>
            <Skeleton variant="text" width={200} height={32} />
            <Skeleton variant="rounded" height={56} />
            <Skeleton variant="rounded" height={48} width={200} />
            <Skeleton variant="rounded" width={100} height={36} />
          </Box>
        ) : (
          <>
            {isPrefsLoadError && (
              <Alert severity="error" sx={styles.prefsError}>
                {t('preferences.errors.loadFailed')}
              </Alert>
            )}
            <PreferencesSection
              preferences={preferences ?? null}
              onSaveSuccess={handleSaveSuccess}
            />
          </>
        )}
      </Box>

      <Snackbar
        open={successOpen}
        autoHideDuration={3000}
        onClose={handleSnackbarClose}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
      >
        <Alert onClose={handleSnackbarClose} severity="success" variant="filled">
          {t('page.saveSuccess')}
        </Alert>
      </Snackbar>
    </Box>
  );
}

// co-located at the bottom of the file
const getStyles = (theme: Theme) =>
  // eslint-disable-next-line react-hooks/rules-of-hooks
  useMemo(
    () => ({
      root: {
        maxWidth: 720,
      },
      pageTitle: {
        marginBottom: theme.spacing(3),
        color: theme.palette.text.primary,
      },
      sections: {
        display: 'flex',
        flexDirection: 'column',
        gap: theme.spacing(4),
      },
      skeletonSection: {
        display: 'flex',
        flexDirection: 'column',
        gap: theme.spacing(2),
        maxWidth: 480,
      },
      prefsError: {},
    }),
    [theme],
  );
