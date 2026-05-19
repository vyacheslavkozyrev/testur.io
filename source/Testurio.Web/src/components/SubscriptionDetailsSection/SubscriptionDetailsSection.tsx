'use client';

import { useCallback, useMemo, useState } from 'react';
import Link from 'next/link';
import { useTranslation } from 'react-i18next';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import CircularProgress from '@mui/material/CircularProgress';
import Snackbar from '@mui/material/Snackbar';
import Alert from '@mui/material/Alert';
import Table from '@mui/material/Table';
import TableBody from '@mui/material/TableBody';
import TableCell from '@mui/material/TableCell';
import TableHead from '@mui/material/TableHead';
import TableRow from '@mui/material/TableRow';
import Typography from '@mui/material/Typography';
import { useTheme, type Theme } from '@mui/material/styles';
import { useSubscriptionStatus, useCreatePortalSession } from '@/hooks/useBilling';
import { PRICING_ROUTE } from '@/routes/routes';
import type { InvoiceDto } from '@/types/plan.types';

export default function SubscriptionDetailsSection() {
  const { t } = useTranslation('subscriptionManagement');
  const theme = useTheme();
  const styles = useMemo(() => getStyles(theme), [theme]);

  const { data: subscription } = useSubscriptionStatus();
  const createPortalSession = useCreatePortalSession();
  const [errorOpen, setErrorOpen] = useState(false);

  const handleManageBilling = useCallback(() => {
    createPortalSession.mutate(undefined, {
      onError: () => setErrorOpen(true),
    });
  }, [createPortalSession]);

  const handleErrorClose = useCallback(() => setErrorOpen(false), []);

  const formattedPeriodEnd = useMemo(() => {
    if (!subscription?.currentPeriodEnd) return '';
    return new Intl.DateTimeFormat(undefined, { dateStyle: 'long' }).format(
      new Date(subscription.currentPeriodEnd),
    );
  }, [subscription?.currentPeriodEnd]);

  if (!subscription || subscription.status === 'None' || subscription.status === 'Expired') {
    return (
      <Box sx={styles.root}>
        <Typography variant="h6" sx={styles.sectionTitle}>
          {t('details.title')}
        </Typography>
        <Typography variant="body2" color="text.secondary" sx={styles.emptyMessage}>
          {subscription?.status === 'Expired'
            ? t('details.expiredMessage')
            : t('details.noPlan')}
        </Typography>
        <Button component={Link} href={PRICING_ROUTE} variant="outlined" size="small" sx={styles.ctaButton}>
          {subscription?.status === 'Expired' ? t('details.expiredCta') : t('details.noPlanCta')}
        </Button>
      </Box>
    );
  }

  const showManageBilling =
    subscription.status === 'Active' ||
    subscription.status === 'Trialing' ||
    subscription.status === 'CancelledPendingExpiry';

  return (
    <Box sx={styles.root}>
      <Typography variant="h6" sx={styles.sectionTitle}>
        {t('details.title')}
      </Typography>

      <Box sx={styles.grid}>
        <Typography variant="body2" color="text.secondary">{t('details.plan')}</Typography>
        <Typography variant="body2">{subscription.plan ?? '—'}</Typography>

        <Typography variant="body2" color="text.secondary">{t('details.billingInterval')}</Typography>
        <Typography variant="body2">
          {subscription.billingInterval === 'annual' ? t('details.annual') : t('details.monthly')}
        </Typography>

        <Typography variant="body2" color="text.secondary">{t('details.nextBillingDate')}</Typography>
        <Typography variant="body2">{formattedPeriodEnd}</Typography>

        {subscription.paymentMethodLast4 && (
          <>
            <Typography variant="body2" color="text.secondary">{t('details.paymentMethod')}</Typography>
            <Box>
              <Typography variant="body2">
                {t('details.cardEndingIn', { last4: subscription.paymentMethodLast4 })}
              </Typography>
              {subscription.paymentMethodExpMonth && subscription.paymentMethodExpYear && (
                <Typography variant="caption" color="text.secondary">
                  {t('details.cardExpiry', {
                    month: String(subscription.paymentMethodExpMonth).padStart(2, '0'),
                    year: subscription.paymentMethodExpYear,
                  })}
                </Typography>
              )}
            </Box>
          </>
        )}
      </Box>

      {showManageBilling && (
        <Button
          variant="outlined"
          size="small"
          onClick={handleManageBilling}
          disabled={createPortalSession.isPending}
          startIcon={createPortalSession.isPending ? <CircularProgress size={14} /> : undefined}
          sx={styles.ctaButton}
        >
          {t('details.manageBilling')}
        </Button>
      )}

      <Typography variant="h6" sx={styles.invoicesTitle}>
        {t('details.invoices')}
      </Typography>

      {subscription.invoices.length === 0 ? (
        <Typography variant="body2" color="text.secondary">
          {t('details.noInvoices')}
        </Typography>
      ) : (
        <Table size="small" sx={styles.table}>
          <TableHead>
            <TableRow>
              <TableCell>{t('details.invoiceDate')}</TableCell>
              <TableCell>{t('details.invoiceAmount')}</TableCell>
              <TableCell>{t('details.invoiceStatus')}</TableCell>
              <TableCell>{t('details.invoicePdf')}</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {subscription.invoices.map((invoice: InvoiceDto, idx: number) => (
              <TableRow key={`${invoice.date}-${idx}`}>
                <TableCell>
                  {new Intl.DateTimeFormat(undefined, { dateStyle: 'medium' }).format(
                    new Date(invoice.date),
                  )}
                </TableCell>
                <TableCell>
                  {new Intl.NumberFormat(undefined, {
                    style: 'currency',
                    currency: invoice.currency.toUpperCase(),
                  }).format(invoice.amount)}
                </TableCell>
                <TableCell>{invoice.status}</TableCell>
                <TableCell>
                  {invoice.pdfUrl ? (
                    <Button
                      component="a"
                      href={invoice.pdfUrl}
                      target="_blank"
                      rel="noopener noreferrer"
                      size="small"
                      variant="text"
                    >
                      {t('details.invoicePdfLink')}
                    </Button>
                  ) : (
                    '—'
                  )}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}

      <Snackbar
        open={errorOpen}
        autoHideDuration={5000}
        onClose={handleErrorClose}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
      >
        <Alert onClose={handleErrorClose} severity="error" variant="filled">
          {t('details.manageBillingError')}
        </Alert>
      </Snackbar>
    </Box>
  );
}

const getStyles = (theme: Theme) => ({
  root: {
    display: 'flex',
    flexDirection: 'column',
    gap: theme.spacing(2),
  },
  sectionTitle: {
    color: theme.palette.text.primary,
  },
  emptyMessage: {
    marginBottom: theme.spacing(1),
  },
  grid: {
    display: 'grid',
    gridTemplateColumns: '180px 1fr',
    gap: `${theme.spacing(1)} ${theme.spacing(2)}`,
    alignItems: 'start',
  },
  ctaButton: {
    alignSelf: 'flex-start',
  },
  invoicesTitle: {
    color: theme.palette.text.primary,
    marginTop: theme.spacing(2),
  },
  table: {
    maxWidth: 600,
  },
});
