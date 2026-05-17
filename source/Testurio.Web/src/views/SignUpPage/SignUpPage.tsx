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
import { useSignUp } from '@/hooks/useAuth';
import type { AuthError } from '@/types/auth.types';
import { SIGN_IN_ROUTE } from '@/routes/routes';

/** Minimum password length enforced by B2C policy. */
const MIN_PASSWORD_LENGTH = 8;

interface SignUpFormValues {
  email: string;
  password: string;
  confirmPassword: string;
}

function getErrorMessage(error: AuthError | { status?: number } | null, t: (key: string) => string): string {
  if (!error) return '';
  const authError = error as AuthError;
  if (authError.code === 'USER_ALREADY_EXISTS') {
    return t('signUp.errorUserExists');
  }
  if (authError.code === 'INVALID_PASSWORD') {
    return t('signUp.errorInvalidPassword');
  }
  return t('signUp.errorGeneric');
}

export default function SignUpPage() {
  const { t } = useTranslation('auth');
  const theme = useTheme();
  const styles = getStyles(theme);

  const signUp = useSignUp();

  const {
    register,
    handleSubmit,
    watch,
    formState: { errors },
  } = useForm<SignUpFormValues>();

  const passwordValue = watch('password');

  const onSubmit = useCallback(
    (data: SignUpFormValues) => {
      signUp.mutate({ email: data.email.trim(), password: data.password });
    },
    [signUp],
  );

  const errorMessage = getErrorMessage(signUp.error, t);

  return (
    <Box sx={styles.page}>
      <Box sx={styles.card}>
        {/* Heading */}
        <Typography component="h1" sx={styles.title}>
          {t('signUp.title')}
        </Typography>
        <Typography sx={styles.subtitle}>
          {t('signUp.subtitle')}
        </Typography>

        {/* Global error */}
        {signUp.isError && (
          <Alert severity="error" sx={styles.alert}>
            {errorMessage}{' '}
            {(signUp.error as AuthError)?.code === 'USER_ALREADY_EXISTS' && (
              <Typography component={Link} href={SIGN_IN_ROUTE} sx={styles.inlineLink}>
                {t('signUp.signInInstead')}
              </Typography>
            )}
          </Alert>
        )}

        {/* Form */}
        <Box component="form" onSubmit={handleSubmit(onSubmit)} noValidate sx={styles.form}>
          <TextField
            label={t('signUp.emailLabel')}
            type="email"
            autoComplete="email"
            fullWidth
            error={Boolean(errors.email)}
            helperText={errors.email?.message}
            disabled={signUp.isPending}
            {...register('email', {
              required: t('signUp.emailRequired'),
            })}
          />

          <TextField
            label={t('signUp.passwordLabel')}
            type="password"
            autoComplete="new-password"
            fullWidth
            error={Boolean(errors.password)}
            helperText={errors.password?.message}
            disabled={signUp.isPending}
            {...register('password', {
              required: t('signUp.passwordRequired'),
              minLength: {
                value: MIN_PASSWORD_LENGTH,
                message: t('signUp.passwordMinLength'),
              },
              validate: (value) => {
                if (!/[A-Z]/.test(value)) return t('signUp.passwordUppercase');
                if (!/[a-z]/.test(value)) return t('signUp.passwordLowercase');
                if (!/[0-9]/.test(value)) return t('signUp.passwordDigit');
                return true;
              },
            })}
          />

          <TextField
            label={t('signUp.confirmPasswordLabel')}
            type="password"
            autoComplete="new-password"
            fullWidth
            error={Boolean(errors.confirmPassword)}
            helperText={errors.confirmPassword?.message}
            disabled={signUp.isPending}
            {...register('confirmPassword', {
              required: t('signUp.confirmPasswordRequired'),
              validate: (value) =>
                value === passwordValue || t('signUp.passwordMismatch'),
            })}
          />

          <Button
            type="submit"
            variant="contained"
            fullWidth
            disabled={signUp.isPending}
            sx={styles.submitButton}
            startIcon={signUp.isPending ? <CircularProgress size={18} color="inherit" /> : undefined}
          >
            {signUp.isPending ? t('signUp.signingUp') : t('signUp.submitButton')}
          </Button>
        </Box>

        {/* Sign-in link */}
        <Typography sx={styles.footerText}>
          {t('signUp.hasAccount')}{' '}
          <Typography component={Link} href={SIGN_IN_ROUTE} sx={styles.link}>
            {t('signUp.signIn')}
          </Typography>
        </Typography>
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
      inlineLink: {
        ...theme.typography.body2,
        color: 'inherit',
        textDecoration: 'underline',
        cursor: 'pointer',
        display: 'inline',
      },
      submitButton: {
        mt: theme.spacing(1),
        py: theme.spacing(1.25),
      },
      footerText: {
        ...theme.typography.body2,
        color: theme.palette.text.secondary,
        textAlign: 'center',
        mt: theme.spacing(1),
      },
    }),
    [theme],
  );
