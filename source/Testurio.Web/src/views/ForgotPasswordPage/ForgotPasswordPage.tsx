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
import { useForgotPassword, useSubmitResetCode, useSubmitNewPassword } from '@/hooks/useAuth';
import type { AuthError, ResetPasswordCodeHandle, ResetPasswordPasswordHandle } from '@/types/auth.types';
import { SIGN_IN_ROUTE } from '@/routes/routes';

const MIN_PASSWORD_LENGTH = 8;

interface EmailFormValues { email: string }
interface CodeFormValues { code: string }
interface NewPasswordFormValues { password: string; confirmPassword: string }

type Step = 'email' | 'code' | 'password' | 'done';

export default function ForgotPasswordPage() {
  const { t } = useTranslation('auth');
  const theme = useTheme();
  const styles = getStyles(theme);

  const [step, setStep] = useState<Step>('email');
  const codeHandleRef = useRef<ResetPasswordCodeHandle | null>(null);
  const passwordHandleRef = useRef<ResetPasswordPasswordHandle | null>(null);

  const forgotPassword = useForgotPassword();
  const submitResetCode = useSubmitResetCode();
  const submitNewPassword = useSubmitNewPassword();

  const emailForm = useForm<EmailFormValues>();
  const codeForm = useForm<CodeFormValues>();
  const passwordForm = useForm<NewPasswordFormValues>();
  const passwordValue = passwordForm.watch('password');

  const onSubmitEmail = useCallback(
    (data: EmailFormValues) => {
      forgotPassword.mutate(
        { email: data.email.trim() },
        {
          onSuccess: (handle) => {
            codeHandleRef.current = handle;
            setStep('code');
          },
        },
      );
    },
    [forgotPassword],
  );

  const onSubmitCode = useCallback(
    (data: CodeFormValues) => {
      if (!codeHandleRef.current) return;
      submitResetCode.mutate(
        { code: data.code.trim(), handle: codeHandleRef.current },
        {
          onSuccess: (handle) => {
            passwordHandleRef.current = handle;
            setStep('password');
          },
        },
      );
    },
    [submitResetCode],
  );

  const onSubmitNewPassword = useCallback(
    (data: NewPasswordFormValues) => {
      if (!passwordHandleRef.current) return;
      submitNewPassword.mutate(
        { password: data.password, handle: passwordHandleRef.current },
        { onSuccess: () => setStep('done') },
      );
    },
    [submitNewPassword],
  );

  if (step === 'done') {
    return (
      <Box sx={styles.page}>
        <Box sx={styles.card}>
          <Typography component="h1" sx={styles.title}>{t('forgotPassword.title')}</Typography>
          <Alert severity="success" sx={styles.alert}>{t('forgotPassword.successMessage')}</Alert>
          <Button component={Link} href={SIGN_IN_ROUTE} variant="contained" fullWidth sx={styles.submitButton}>
            {t('forgotPassword.backToSignIn')}
          </Button>
        </Box>
      </Box>
    );
  }

  if (step === 'password') {
    return (
      <Box sx={styles.page}>
        <Box sx={styles.card}>
          <Typography component="h1" sx={styles.title}>{t('forgotPassword.title')}</Typography>
          {submitNewPassword.isError && (
            <Alert severity="error" sx={styles.alert}>
              {(submitNewPassword.error as AuthError)?.code === 'INVALID_PASSWORD'
                ? t('forgotPassword.errorInvalidPassword')
                : t('forgotPassword.errorGeneric')}
            </Alert>
          )}
          <Box component="form" onSubmit={passwordForm.handleSubmit(onSubmitNewPassword)} noValidate sx={styles.form}>
            <TextField
              label={t('forgotPassword.newPasswordLabel')}
              type="password"
              autoComplete="new-password"
              fullWidth
              autoFocus
              error={Boolean(passwordForm.formState.errors.password)}
              helperText={passwordForm.formState.errors.password?.message}
              disabled={submitNewPassword.isPending}
              {...passwordForm.register('password', {
                required: t('forgotPassword.newPasswordRequired'),
                minLength: { value: MIN_PASSWORD_LENGTH, message: t('forgotPassword.passwordMinLength') },
                validate: (value) => {
                  const failures: string[] = [];
                  if (!/[A-Z]/.test(value)) failures.push(t('forgotPassword.passwordUppercase'));
                  if (!/[a-z]/.test(value)) failures.push(t('forgotPassword.passwordLowercase'));
                  if (!/[0-9]/.test(value)) failures.push(t('forgotPassword.passwordDigit'));
                  return failures.length > 0 ? failures.join(' ') : true;
                },
              })}
            />
            <TextField
              label={t('forgotPassword.confirmPasswordLabel')}
              type="password"
              autoComplete="new-password"
              fullWidth
              error={Boolean(passwordForm.formState.errors.confirmPassword)}
              helperText={passwordForm.formState.errors.confirmPassword?.message}
              disabled={submitNewPassword.isPending}
              {...passwordForm.register('confirmPassword', {
                required: t('forgotPassword.confirmPasswordRequired'),
                validate: (value) => value === passwordValue || t('forgotPassword.passwordMismatch'),
              })}
            />
            <Button
              type="submit"
              variant="contained"
              fullWidth
              disabled={submitNewPassword.isPending}
              sx={styles.submitButton}
              startIcon={submitNewPassword.isPending ? <CircularProgress size={18} color="inherit" /> : undefined}
            >
              {submitNewPassword.isPending ? t('forgotPassword.settingPassword') : t('forgotPassword.setPasswordButton')}
            </Button>
          </Box>
        </Box>
      </Box>
    );
  }

  if (step === 'code') {
    return (
      <Box sx={styles.page}>
        <Box sx={styles.card}>
          <Typography component="h1" sx={styles.title}>{t('forgotPassword.title')}</Typography>
          <Typography sx={styles.subtitle}>{t('forgotPassword.codeSent')}</Typography>
          {submitResetCode.isError && (
            <Alert severity="error" sx={styles.alert}>
              {(submitResetCode.error as AuthError)?.code === 'INVALID_CODE'
                ? t('forgotPassword.errorInvalidCode')
                : t('forgotPassword.errorGeneric')}
            </Alert>
          )}
          <Box component="form" onSubmit={codeForm.handleSubmit(onSubmitCode)} noValidate sx={styles.form}>
            <TextField
              label={t('forgotPassword.codeLabel')}
              type="text"
              autoComplete="one-time-code"
              fullWidth
              autoFocus
              error={Boolean(codeForm.formState.errors.code)}
              helperText={codeForm.formState.errors.code?.message}
              disabled={submitResetCode.isPending}
              {...codeForm.register('code', { required: t('forgotPassword.codeFieldRequired') })}
            />
            <Button
              type="submit"
              variant="contained"
              fullWidth
              disabled={submitResetCode.isPending}
              sx={styles.submitButton}
              startIcon={submitResetCode.isPending ? <CircularProgress size={18} color="inherit" /> : undefined}
            >
              {submitResetCode.isPending ? t('forgotPassword.codeVerifying') : t('forgotPassword.codeSubmitButton')}
            </Button>
          </Box>
        </Box>
      </Box>
    );
  }

  return (
    <Box sx={styles.page}>
      <Box sx={styles.card}>
        <Typography component="h1" sx={styles.title}>{t('forgotPassword.title')}</Typography>
        <Typography sx={styles.subtitle}>{t('forgotPassword.subtitle')}</Typography>

        {forgotPassword.isError && (
          <Alert severity="error" sx={styles.alert}>{t('forgotPassword.errorGeneric')}</Alert>
        )}

        <Box component="form" onSubmit={emailForm.handleSubmit(onSubmitEmail)} noValidate sx={styles.form}>
          <TextField
            label={t('forgotPassword.emailLabel')}
            type="email"
            autoComplete="email"
            fullWidth
            error={Boolean(emailForm.formState.errors.email)}
            helperText={emailForm.formState.errors.email?.message}
            disabled={forgotPassword.isPending}
            {...emailForm.register('email', { required: t('forgotPassword.emailRequired') })}
          />
          <Button
            type="submit"
            variant="contained"
            fullWidth
            disabled={forgotPassword.isPending}
            sx={styles.submitButton}
            startIcon={forgotPassword.isPending ? <CircularProgress size={18} color="inherit" /> : undefined}
          >
            {forgotPassword.isPending ? t('forgotPassword.sending') : t('forgotPassword.submitButton')}
          </Button>
          <Typography sx={styles.backLink}>
            <Typography component={Link} href={SIGN_IN_ROUTE} sx={styles.link}>
              {t('forgotPassword.backToSignIn')}
            </Typography>
          </Typography>
        </Box>
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
      submitButton: {
        mt: theme.spacing(1),
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
