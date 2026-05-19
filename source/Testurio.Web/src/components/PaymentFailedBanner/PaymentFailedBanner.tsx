'use client';

import { useCallback, useState } from 'react';
import { useTranslation } from 'react-i18next';
import Alert from '@mui/material/Alert';
import Button from '@mui/material/Button';
import CircularProgress from '@mui/material/CircularProgress';
import Snackbar from '@mui/material/Snackbar';
import { useSubscriptionStatus, useCreatePortalSession } from '@/hooks/useBilling';

export default function PaymentFailedBanner() {
  const { t } = useTranslation('subscriptionManagement');
  const { data: subscription } = useSubscriptionStatus();
  const createPortalSession = useCreatePortalSession();
  const [errorOpen, setErrorOpen] = useState(false);

  const handleUpdatePayment = useCallback(() => {
    createPortalSession.mutate(undefined, {
      onError: () => setErrorOpen(true),
    });
  }, [createPortalSession]);

  const handleErrorClose = useCallback(() => setErrorOpen(false), []);

  if (subscription?.status !== 'PaymentFailed') return null;

  const updateCta = (
    <Button
      color="inherit"
      size="small"
      onClick={handleUpdatePayment}
      disabled={createPortalSession.isPending}
      startIcon={createPortalSession.isPending ? <CircularProgress size={14} color="inherit" /> : undefined}
    >
      {t('paymentFailedBanner.updateCta')}
    </Button>
  );

  return (
    <>
      <Alert severity="error" action={updateCta} sx={{ borderRadius: 0 }}>
        {t('paymentFailedBanner.message')}
      </Alert>

      <Snackbar
        open={errorOpen}
        autoHideDuration={5000}
        onClose={handleErrorClose}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
      >
        <Alert onClose={handleErrorClose} severity="error" variant="filled">
          {t('paymentFailedBanner.errorMessage')}
        </Alert>
      </Snackbar>
    </>
  );
}
