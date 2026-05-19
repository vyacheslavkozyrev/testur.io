'use client';

import { useCallback, useState } from 'react';
import { useTranslation } from 'react-i18next';
import Alert from '@mui/material/Alert';
import Button from '@mui/material/Button';
import { useSubscriptionStatus } from '@/hooks/useBilling';
import ReactivateConfirmDialog from '@/components/ReactivateConfirmDialog/ReactivateConfirmDialog';

export default function CancellationPendingBanner() {
  const { t } = useTranslation('subscriptionManagement');
  const { data: subscription } = useSubscriptionStatus();
  const [dialogOpen, setDialogOpen] = useState(false);

  const handleReactivateClick = useCallback(() => setDialogOpen(true), []);
  const handleDialogClose = useCallback(() => setDialogOpen(false), []);

  if (subscription?.status !== 'CancelledPendingExpiry') return null;

  const periodEnd = new Intl.DateTimeFormat(undefined, { dateStyle: 'long' }).format(
    new Date(subscription.currentPeriodEnd),
  );

  const reactivateCta = (
    <Button color="inherit" size="small" onClick={handleReactivateClick}>
      {t('cancellationBanner.reactivateCta')}
    </Button>
  );

  return (
    <>
      <Alert severity="warning" action={reactivateCta} sx={{ borderRadius: 0 }}>
        {t('cancellationBanner.message', { date: periodEnd })}
      </Alert>

      <ReactivateConfirmDialog
        open={dialogOpen}
        onClose={handleDialogClose}
        subscription={subscription}
      />
    </>
  );
}
