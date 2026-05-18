'use client';

import { useCallback, useMemo, useRef, useState } from 'react';
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
import { useSignUp, useSubmitSignUpCode } from '@/hooks/useAuth';
import type { AuthError } from '@/types/auth.types';
import { SIGN_IN_ROUTE } from '@/routes/routes';

/** Minimum password length enforced by B2C policy. */
const MIN_PASSWORD_LENGTH = 8;

interface SignUpFormValues {
  firstName: string;
  lastName: string;
  email: string;
  password: string;
  confirmPassword: string;
}

interface CodeFormValues {
  code: string;
}

function getSignUpErrorMessage(error: AuthError | null, t: (key: string) => string): string {
  if (!error) return '';
  if (error.code === 'USER_ALREADY_EXISTS') return t('signUp.errorUserExists');
  if (error.code === 'INVALID_PASSWORD') return t('signUp.errorInvalidPassword');
  return t('signUp.errorGeneric');
}

function getCodeErrorMessage(error: AuthError | null, t: (key: string) => string): string {
  if (!error) return '';
  if (error.code === 'INVALID_CODE') return t('signUp.errorInvalidCode');
  return t('signUp.errorGeneric');
}

export default function SignUpPage() {
  const { t } = useTranslation('auth');
  const theme = useTheme();
  const styles = getStyles(theme);

  const [codeStep, setCodeStep] = useState(false);
  const signUp = useSignUp();
  const submitCode = useSubmitSignUpCode();
  const codeHandleRef = useRef<import('@/types/auth.types').SignUpCodeHandle | null>(null);

  const { register, handleSubmit, watch, formState: { errors } } = useForm<SignUpFormValues>();
  const { register: registerCode, handleSubmit: handleSubmitCode, formState: { errors: codeErrors } } = useForm<CodeFormValues>();

  const passwordValue = watch('password');

  const onSubmit = useCallback(
    (data: SignUpFormValues) => {
      signUp.mutate(
        { firstName: data.firstName.trim(), lastName: data.lastName.trim(), email: data.email.trim(), password: data.password },
        { onError: (err) => { if ((err as AuthError).code === 'CODE_REQUIRED') { codeHandleRef.current = (err as AuthError).signUpCodeHandle ?? null; setCodeStep(true); } } },
      );
    },
    [signUp],
  );

  const onSubmitCode = useCallback(
    (data: CodeFormValues) => {
      if (!codeHandleRef.current) return;
      submitCode.mutate({ code: data.code.trim(), handle: codeHandleRef.current });
    },
    [submitCode],
  );

  if (codeStep) {
    return (
      <Box sx={styles.page}>
        <Box sx={styles.card}>
          <Typography component="h1" sx={styles.title}>{t('signUp.title')}</Typography>
          <Typography sx={styles.subtitle}>{t('signUp.codeRequired')}</Typography>
          {submitCode.isError && (
            <Alert severity="error" sx={styles.alert}>
              {getCodeErrorMessage(submitCode.error as AuthError | null, t)}
            </Alert>
          )}
          <Box component="form" onSubmit={handleSubmitCode(onSubmitCode)} noValidate sx={styles.form}>
            <TextField
              label={t('signUp.codeLabel')}
              type="text"
              autoComplete="off"
              fullWidth
              autoFocus
              error={Boolean(codeErrors.code)}
              helperText={codeErrors.code?.message}
              disabled={submitCode.isPending}
              {...registerCode('code', { required: t('signUp.codeFieldRequired') })}
            />
            <Button
              type="submit"
              variant="contained"
              fullWidth
              disabled={submitCode.isPending}
              sx={styles.submitButton}
              startIcon={submitCode.isPending ? <CircularProgress size={18} color="inherit" /> : undefined}
            >
              {submitCode.isPending ? t('signUp.codeVerifying') : t('signUp.codeSubmitButton')}
            </Button>
          </Box>
        </Box>
      </Box>
    );
  }

  const signUpError = signUp.error as AuthError | null;

  return (
    <Box sx={styles.page}>
      <Box sx={styles.card}>
        <Typography component="h1" sx={styles.title}>{t('signUp.title')}</Typography>
        <Typography sx={styles.subtitle}>{t('signUp.subtitle')}</Typography>

        {signUp.isError && signUpError?.code !== 'CODE_REQUIRED' && (
          <Alert severity="error" sx={styles.alert}>
            {getSignUpErrorMessage(signUpError, t)}{' '}
            {signUpError?.code === 'USER_ALREADY_EXISTS' && (
              <Link href={SIGN_IN_ROUTE} style={{ color: 'inherit', textDecoration: 'underline' }}>
                {t('signUp.signInInstead')}
              </Link>
            )}
          </Alert>
        )}

        <Box component="form" onSubmit={handleSubmit(onSubmit)} noValidate sx={styles.form}>
          <Box sx={styles.nameRow}>
            <TextField
              label={t('signUp.firstNameLabel')}
              type="text"
              autoComplete="given-name"
              fullWidth
              error={Boolean(errors.firstName)}
              helperText={errors.firstName?.message}
              disabled={signUp.isPending}
              {...register('firstName', { required: t('signUp.firstNameRequired') })}
            />
            <TextField
              label={t('signUp.lastNameLabel')}
              type="text"
              autoComplete="family-name"
              fullWidth
              error={Boolean(errors.lastName)}
              helperText={errors.lastName?.message}
              disabled={signUp.isPending}
              {...register('lastName', { required: t('signUp.lastNameRequired') })}
            />
          </Box>
          <TextField
            label={t('signUp.emailLabel')}
            type="email"
            autoComplete="email"
            fullWidth
            error={Boolean(errors.email)}
            helperText={errors.email?.message}
            disabled={signUp.isPending}
            {...register('email', { required: t('signUp.emailRequired') })}
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
              minLength: { value: MIN_PASSWORD_LENGTH, message: t('signUp.passwordMinLength') },
              validate: (value) => {
                const failures: string[] = [];
                if (!/[A-Z]/.test(value)) failures.push(t('signUp.passwordUppercase'));
                if (!/[a-z]/.test(value)) failures.push(t('signUp.passwordLowercase'));
                if (!/[0-9]/.test(value)) failures.push(t('signUp.passwordDigit'));
                if (failures.length > 0) return failures.join(' ');
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
              validate: (value) => value === passwordValue || t('signUp.passwordMismatch'),
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

        <Typography sx={styles.footerText}>
          {t('signUp.hasAccount')}{' '}
          <Link href={SIGN_IN_ROUTE} style={{ textDecoration: 'none' }}>
            <Typography component="span" sx={styles.link}>{t('signUp.signIn')}</Typography>
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
      nameRow: {
        display: 'flex',
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
      footerText: {
        ...theme.typography.body2,
        color: theme.palette.text.secondary,
        textAlign: 'center',
        mt: theme.spacing(1),
      },
    }),
    [theme],
  );
