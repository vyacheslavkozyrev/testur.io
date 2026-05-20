'use client';

import { useCallback, useMemo } from 'react';
import { useSearchParams } from 'next/navigation';
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
import { useSignIn } from '@/hooks/useAuth';
import type { AuthError } from '@/types/auth.types';
import { SIGN_UP_ROUTE, FORGOT_PASSWORD_ROUTE } from '@/routes/routes';

interface SignInFormValues {
  email: string;
  password: string;
}

function getErrorMessage(error: AuthError | null, t: (key: string) => string): string {
  if (!error) return '';
  const authError = error as AuthError;
  if (authError.code === 'INVALID_CREDENTIALS' || authError.code === 'USER_NOT_FOUND') {
    return t('signIn.errorInvalidCredentials');
  }
  if (authError.code === 'RATE_LIMITED') {
    return t('signIn.errorRateLimit');
  }
  return t('signIn.errorGeneric');
}

export default function SignInPage() {
  const { t } = useTranslation('auth');
  const theme = useTheme();
  const styles = getStyles(theme);
  const searchParams = useSearchParams();
  const returnUrl = searchParams.get('returnUrl') ?? undefined;

  const signIn = useSignIn(returnUrl);

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<SignInFormValues>();

  const onSubmit = useCallback(
    (data: SignInFormValues) => {
      signIn.mutate({ email: data.email.trim(), password: data.password });
    },
    [signIn],
  );

  const errorMessage = getErrorMessage(signIn.error as AuthError | null, t);

  return (
    <Box sx={styles.page}>
      <Box sx={styles.card}>
        {/* Heading */}
        <Typography component="h1" sx={styles.title}>
          {t('signIn.title')}
        </Typography>
        <Typography sx={styles.subtitle}>
          {t('signIn.subtitle')}
        </Typography>

        {/* Global error */}
        {signIn.isError && (
          <Alert severity="error" sx={styles.alert}>
            {errorMessage}
          </Alert>
        )}

        {/* Form */}
        <Box component="form" onSubmit={handleSubmit(onSubmit)} noValidate sx={styles.form}>
          <TextField
            label={t('signIn.emailLabel')}
            type="email"
            autoComplete="email"
            fullWidth
            error={Boolean(errors.email)}
            helperText={errors.email?.message}
            disabled={signIn.isPending}
            {...register('email', {
              required: t('signIn.emailRequired'),
            })}
          />

          <TextField
            label={t('signIn.passwordLabel')}
            type="password"
            autoComplete="current-password"
            fullWidth
            error={Boolean(errors.password)}
            helperText={errors.password?.message}
            disabled={signIn.isPending}
            {...register('password', {
              required: t('signIn.passwordRequired'),
            })}
          />

          {/* Forgot password link */}
          <Box sx={styles.forgotRow}>
            <Link href={FORGOT_PASSWORD_ROUTE} style={{ textDecoration: 'none' }}>
              <Typography sx={styles.link}>
                {t('signIn.forgotPassword')}
              </Typography>
            </Link>
          </Box>

          <Button
            type="submit"
            variant="contained"
            fullWidth
            disabled={signIn.isPending}
            sx={styles.submitButton}
            startIcon={signIn.isPending ? <CircularProgress size={18} color="inherit" /> : undefined}
          >
            {signIn.isPending ? t('signIn.signingIn') : t('signIn.submitButton')}
          </Button>
        </Box>

        {/* Sign-up link */}
        <Typography sx={styles.footerText}>
          {t('signIn.noAccount')}{' '}
          <Link href={SIGN_UP_ROUTE} style={{ textDecoration: 'none' }}>
            <Typography component="span" sx={styles.link}>
              {t('signIn.createAccount')}
            </Typography>
          </Link>
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
        fontWeight: 700,
        color: theme.palette.text.primary,
        textAlign: 'center',
      },
      subtitle: {
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
      forgotRow: {
        display: 'flex',
        justifyContent: 'flex-end',
        mt: theme.spacing(-1),
      },
      link: {
        color: theme.palette.primary.main,
        textDecoration: 'none',
        '&:hover': { textDecoration: 'underline' },
        cursor: 'pointer',
      },
      submitButton: {
        mt: theme.spacing(1),
        py: theme.spacing(1.25),
      },
      footerText: {
        color: theme.palette.text.secondary,
        textAlign: 'center',
        mt: theme.spacing(1),
      },
    }),
    [theme],
  );
