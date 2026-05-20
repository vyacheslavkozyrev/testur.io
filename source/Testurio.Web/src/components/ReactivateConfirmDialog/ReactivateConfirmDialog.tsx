'use client';

import { useCallback, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import Button from '@mui/material/Button';
import CircularProgress from '@mui/material/CircularProgress';
import Dialog from '@mui/material/Dialog';
import DialogActions from '@mui/material/DialogActions';
import DialogContent from '@mui/material/DialogContent';
import DialogTitle from '@mui/material/DialogTitle';
import Snackbar from '@mui/material/Snackbar';
import Alert from '@mui/material/Alert';
import Typography from '@mui/material/Typography';
import { useReactivateSubscription } from '@/hooks/useBilling';
import type { SubscriptionStatusResponse } from '@/types/plan.types';

interface ReactivateConfirmDialogProps {
  open: boolean;
  onClose: () => void;
  subscription: SubscriptionStatusResponse;
}

export default function ReactivateConfirmDialog({
  open,
  onClose,
  subscription,
}: ReactivateConfirmDialogProps) {
  const { t } = useTranslation('subscriptionManagement');
  const reactivate = useReactivateSubscription();
  const [errorOpen, setErrorOpen] = useState(false);

  const handleConfirm = useCallback(() => {
    reactivate.mutate(undefined, {
      onSuccess: onClose,
      onError: () => setErrorOpen(true),
    });
  }, [reactivate, onClose]);

  const handleErrorClose = useCallback(() => setErrorOpen(false), []);

  const nextBillingDate = useMemo(
    () =>
      new Intl.DateTimeFormat(undefined, { dateStyle: 'long' }).format(
        new Date(subscription.currentPeriodEnd),
      ),
    [subscription.currentPeriodEnd],
  );

  return (
    <>
      <Dialog open={open} onClose={onClose} maxWidth="xs" fullWidth>
        <DialogTitle>{t('reactivateDialog.title')}</DialogTitle>
        <DialogContent>
          <Typography variant="body2">
            {t('reactivateDialog.message', {
              plan: subscription.plan ?? '',
              amount: '—',
              date: nextBillingDate,
            })}
          </Typography>
        </DialogContent>
        <DialogActions>
          <Button onClick={onClose} disabled={reactivate.isPending}>
            {t('reactivateDialog.cancelCta')}
          </Button>
          <Button
            variant="contained"
            onClick={handleConfirm}
            disabled={reactivate.isPending}
            startIcon={reactivate.isPending ? <CircularProgress size={14} /> : undefined}
          >
            {t('reactivateDialog.confirmCta')}
          </Button>
        </DialogActions>
      </Dialog>

      <Snackbar
        open={errorOpen}
        autoHideDuration={5000}
        onClose={handleErrorClose}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
      >
        <Alert onClose={handleErrorClose} severity="error" variant="filled">
          {t('reactivateDialog.errorMessage')}
        </Alert>
      </Snackbar>
    </>
  );
}
