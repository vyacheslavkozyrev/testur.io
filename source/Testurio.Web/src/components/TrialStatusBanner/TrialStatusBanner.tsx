'use client';

import { useMemo } from 'react';
import Link from 'next/link';
import Alert from '@mui/material/Alert';
import Button from '@mui/material/Button';
import { useTranslation } from 'react-i18next';
import { useSubscriptionStatus } from '@/hooks/useBilling';
import { PRICING_ROUTE } from '@/routes/routes';

const AMBER_THRESHOLD_DAYS = 3;

function daysRemaining(trialEndsAt: string): number {
  const endMs = new Date(trialEndsAt).getTime();
  const nowMs = Date.now();
  return Math.max(0, Math.ceil((endMs - nowMs) / (1000 * 60 * 60 * 24)));
}

/**
 * Displays a banner with trial status information on every authenticated portal page.
 * Hidden when status is Active or None.
 */
export default function TrialStatusBanner() {
  const { t } = useTranslation('billing');
  const { data: subscription } = useSubscriptionStatus();

  const days = useMemo(() => {
    if (!subscription?.trialEndsAt) return null;
    return daysRemaining(subscription.trialEndsAt);
  }, [subscription?.trialEndsAt]);

  if (!subscription) return null;
  if (subscription.status === 'Active' || subscription.status === 'None') return null;

  const isExpired = subscription.status === 'Expired';
  const isAmber = !isExpired && days !== null && days <= AMBER_THRESHOLD_DAYS;

  const severity = isAmber || isExpired ? 'warning' : 'info';

  const upgradeCta = (
    <Button
      component={Link}
      href={PRICING_ROUTE}
      color="inherit"
      size="small"
    >
      {t('banner.upgradeCta')}
    </Button>
  );

  return (
    <Alert severity={severity} action={upgradeCta} sx={{ borderRadius: 0 }}>
      {isExpired
        ? t('banner.expired')
        : t('banner.daysRemaining', { count: days ?? 0 })}
    </Alert>
  );
}
