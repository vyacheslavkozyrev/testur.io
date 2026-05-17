'use client';

import { useCallback, useMemo } from 'react';
import Link from 'next/link';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import CircularProgress from '@mui/material/CircularProgress';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import { useTheme, type Theme } from '@mui/material/styles';
import { useTranslation } from 'react-i18next';
import { useForm } from 'react-hook-form';
import { useForgotPassword } from '@/hooks/useAuth';
import { SIGN_IN_ROUTE } from '@/routes/routes';

interface ForgotPasswordFormValues {
  email: string;
}

export default function ForgotPasswordPage() {
  const { t } = useTranslation('auth');
  const theme = useTheme();
  const styles = getStyles(theme);

  const forgotPassword = useForgotPassword();

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<ForgotPasswordFormValues>();

  const onSubmit = useCallback(
    (data: ForgotPasswordFormValues) => {
      forgotPassword.mutate({ email: data.email.trim() });
    },
    [forgotPassword],
  );

  return (
    <Box sx={styles.page}>
      <Box sx={styles.card}>
        {/* Heading */}
        <Typography component="h1" sx={styles.title}>
          {t('forgotPassword.title')}
        </Typography>
        <Typography sx={styles.subtitle}>
          {t('forgotPassword.subtitle')}
        </Typography>

        {/* Confirmation state */}
        {forgotPassword.isSuccess ? (
          <Box sx={styles.confirmationBox}>
            <Alert severity="success" sx={styles.alert}>
              {t('forgotPassword.confirmationMessage')}
            </Alert>
            <Button
              component={Link}
              href={SIGN_IN_ROUTE}
              variant="outlined"
              fullWidth
              sx={styles.backButton}
            >
              {t('forgotPassword.backToSignIn')}
            </Button>
          </Box>
        ) : (
          /* Request form */
          <Box component="form" onSubmit={handleSubmit(onSubmit)} noValidate sx={styles.form}>
            <TextField
              label={t('forgotPassword.emailLabel')}
              type="email"
              autoComplete="email"
              fullWidth
              error={Boolean(errors.email)}
              helperText={errors.email?.message}
              disabled={forgotPassword.isPending}
              {...register('email', {
                required: t('forgotPassword.emailRequired'),
              })}
            />

            <Button
              type="submit"
              variant="contained"
              fullWidth
              disabled={forgotPassword.isPending}
              sx={styles.submitButton}
              startIcon={
                forgotPassword.isPending ? <CircularProgress size={18} color="inherit" /> : undefined
              }
            >
              {forgotPassword.isPending
                ? t('forgotPassword.sending')
                : t('forgotPassword.submitButton')}
            </Button>

            <Typography sx={styles.backLink}>
              <Typography component={Link} href={SIGN_IN_ROUTE} sx={styles.link}>
                {t('forgotPassword.backToSignIn')}
              </Typography>
            </Typography>
          </Box>
        )}
      </Box>
    </Box>
  );
}

// co-located at the bottom of the file
const getStyles = (theme: Theme) =>
  // eslint-disable-next-line react-hooks/rules-of-hooks
  useMemo(
    () => ({
      page: {
        minHeight: '100vh',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        backgroundColor: theme.palette.background.default,
        p: theme.spacing(2),
      },
      card: {
        backgroundColor: theme.palette.background.paper,
        borderRadius: `${theme.shape.borderRadius * 2}px`,
        boxShadow: '0 2px 12px rgba(0,0,0,0.08)',
        p: theme.spacing(4),
        width: '100%',
        maxWidth: 440,
        display: 'flex',
        flexDirection: 'column',
        gap: theme.spacing(2),
      },
      title: {
        ...theme.typography.h5,
        fontWeight: 700,
        color: theme.palette.text.primary,
        textAlign: 'center',
      },
      subtitle: {
        ...theme.typography.body2,
        color: theme.palette.text.secondary,
        textAlign: 'center',
        mb: theme.spacing(1),
      },
      alert: {
        borderRadius: `${theme.shape.borderRadius}px`,
      },
      confirmationBox: {
        display: 'flex',
        flexDirection: 'column',
        gap: theme.spacing(2),
      },
      form: {
        display: 'flex',
        flexDirection: 'column',
        gap: theme.spacing(2),
      },
      link: {
        ...theme.typography.body2,
        color: theme.palette.primary.main,
        textDecoration: 'none',
        '&:hover': { textDecoration: 'underline' },
        cursor: 'pointer',
      },
      submitButton: {
        mt: theme.spacing(1),
        py: theme.spacing(1.25),
      },
      backButton: {
        py: theme.spacing(1.25),
      },
      backLink: {
        ...theme.typography.body2,
        color: theme.palette.text.secondary,
        textAlign: 'center',
      },
    }),
    [theme],
  );
