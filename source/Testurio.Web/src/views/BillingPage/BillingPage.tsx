'use client';

import { useEffect, useMemo } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import Box from '@mui/material/Box';
import CircularProgress from '@mui/material/CircularProgress';
import Typography from '@mui/material/Typography';
import { useTheme, type Theme } from '@mui/material/styles';
import { useTranslation } from 'react-i18next';
import { useCreateCheckoutSession } from '@/hooks/useBilling';
import { PRICING_ROUTE } from '@/routes/routes';
import type { BillingInterval } from '@/types/plan.types';

/**
 * Gateway page that reads plan+interval from query params and immediately
 * initiates a Stripe Checkout session, redirecting the user to Stripe.
 *
 * If plan or interval params are missing, redirects back to /pricing.
 */
export default function BillingPage() {
  const { t } = useTranslation('billing');
  const router = useRouter();
  const searchParams = useSearchParams();
  const theme = useTheme();
  const styles = getStyles(theme);

  const plan = searchParams.get('plan');
  const interval = searchParams.get('interval') as BillingInterval | null;

  const createCheckoutSession = useCreateCheckoutSession();

  useEffect(() => {
    if (!plan || !interval) {
      router.replace(PRICING_ROUTE);
      return;
    }

    createCheckoutSession.mutate({ plan, billingInterval: interval });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return (
    <Box sx={styles.root}>
      <CircularProgress size={48} />
      <Typography sx={styles.message}>{t('checkout.redirecting')}</Typography>
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
      },
      message: {
        color: theme.palette.text.secondary,
      },
    }),
    [theme],
  );
