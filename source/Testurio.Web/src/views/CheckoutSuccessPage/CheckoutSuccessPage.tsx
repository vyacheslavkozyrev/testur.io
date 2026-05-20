'use client';

import { useEffect, useRef, useState, useMemo } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import Link from 'next/link';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import CircularProgress from '@mui/material/CircularProgress';
import Typography from '@mui/material/Typography';
import CheckCircleOutlineIcon from '@mui/icons-material/CheckCircleOutline';
import { useTheme, type Theme } from '@mui/material/styles';
import { useTranslation } from 'react-i18next';
import { useSubscriptionStatus } from '@/hooks/useBilling';
import { PRICING_ROUTE, NEW_PROJECT_ROUTE } from '@/routes/routes';

const TIMEOUT_MS = 30_000;

/**
 * Landing page after a successful Stripe Checkout.
 * Polls GET /v1/billing/subscription every 3 s until status becomes
 * Trialing/Active or a 30 s timeout elapses.
 */
export default function CheckoutSuccessPage() {
  const { t } = useTranslation('checkoutSuccess');
  const router = useRouter();
  const searchParams = useSearchParams();
  const theme = useTheme();
  const styles = getStyles(theme);

  const sessionId = searchParams.get('session_id');

  const [timedOut, setTimedOut] = useState(false);
  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const canPoll = !timedOut && Boolean(sessionId);
  const { data: subscription } = useSubscriptionStatus(canPoll, canPoll);

  const isConfirmed =
    subscription?.status === 'Trialing' || subscription?.status === 'Active';

  useEffect(() => {
    if (!sessionId) {
      router.replace(PRICING_ROUTE);
      return;
    }

    timerRef.current = setTimeout(() => setTimedOut(true), TIMEOUT_MS);
    return () => {
      if (timerRef.current) clearTimeout(timerRef.current);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Stop the timeout once confirmed.
  useEffect(() => {
    if (isConfirmed && timerRef.current) {
      clearTimeout(timerRef.current);
      timerRef.current = null;
    }
  }, [isConfirmed]);

  if (!sessionId) return null;

  if (isConfirmed) {
    return (
      <Box sx={styles.root}>
        <CheckCircleOutlineIcon sx={styles.successIcon} />
        <Typography variant="h4" sx={styles.heading}>
          {t('confirmed.title')}
        </Typography>
        <Typography sx={styles.body}>{t('confirmed.message')}</Typography>
        <Button
          component={Link}
          href={NEW_PROJECT_ROUTE}
          variant="contained"
          size="large"
          sx={styles.cta}
        >
          {t('confirmed.cta')}
        </Button>
      </Box>
    );
  }

  if (timedOut) {
    return (
      <Box sx={styles.root}>
        <Alert severity="warning" sx={styles.alert}>
          {t('timeout.message')}
        </Alert>
      </Box>
    );
  }

  return (
    <Box sx={styles.root}>
      <CircularProgress size={48} />
      <Typography sx={styles.body}>{t('loading.message')}</Typography>
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
        alignItems: 'center',
        justifyContent: 'center',
        minHeight: '60vh',
        gap: theme.spacing(3),
        px: theme.spacing(2),
        textAlign: 'center',
      },
      successIcon: {
        fontSize: 64,
        color: theme.palette.success.main,
      },
      heading: {
        ...theme.typography.h4,
        fontWeight: 700,
        color: theme.palette.text.primary,
      },
      body: {
        ...theme.typography.body1,
        color: theme.palette.text.secondary,
        maxWidth: 480,
      },
      cta: {
        minHeight: 48,
        fontWeight: 600,
        px: theme.spacing(4),
      },
      alert: {
        maxWidth: 560,
      },
    }),
    [theme],
  );
