'use client';

import { useMemo } from 'react';
import Link from 'next/link';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Dialog from '@mui/material/Dialog';
import DialogContent from '@mui/material/DialogContent';
import DialogTitle from '@mui/material/DialogTitle';
import IconButton from '@mui/material/IconButton';
import Typography from '@mui/material/Typography';
import CloseIcon from '@mui/icons-material/Close';
import LockOutlinedIcon from '@mui/icons-material/LockOutlined';
import { useTheme, type Theme } from '@mui/material/styles';
import { useTranslation } from 'react-i18next';
import { PRICING_ROUTE } from '@/routes/routes';

export interface UpgradeModalProps {
  open: boolean;
  onClose: () => void;
}

/**
 * Modal shown when a user without an active subscription attempts a gated action.
 * Provides a direct link to the pricing page with monthly interval pre-selected.
 */
export default function UpgradeModal({ open, onClose }: UpgradeModalProps) {
  const { t } = useTranslation('billing');
  const theme = useTheme();
  const styles = getStyles(theme);

  const pricingUrl = `${PRICING_ROUTE}?interval=monthly`;

  return (
    <Dialog open={open} onClose={onClose} maxWidth="xs" fullWidth>
      <DialogTitle sx={styles.title}>
        <Box sx={styles.titleContent}>
          <LockOutlinedIcon sx={styles.lockIcon} />
          {t('upgradeModal.title')}
        </Box>
        <IconButton
          aria-label={t('upgradeModal.close')}
          onClick={onClose}
          size="small"
          sx={styles.closeButton}
        >
          <CloseIcon fontSize="small" />
        </IconButton>
      </DialogTitle>
      <DialogContent sx={styles.content}>
        <Typography sx={styles.body}>{t('upgradeModal.message')}</Typography>
        <Button
          component={Link}
          href={pricingUrl}
          variant="contained"
          fullWidth
          size="large"
          sx={styles.cta}
          onClick={onClose}
        >
          {t('upgradeModal.cta')}
        </Button>
        <Button variant="text" fullWidth onClick={onClose} sx={styles.dismiss}>
          {t('upgradeModal.dismiss')}
        </Button>
      </DialogContent>
    </Dialog>
  );
}

// co-located at the bottom of the file
const getStyles = (theme: Theme) =>
  // eslint-disable-next-line react-hooks/rules-of-hooks
  useMemo(
    () => ({
      title: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        pb: 0,
      },
      titleContent: {
        display: 'flex',
        alignItems: 'center',
        gap: theme.spacing(1),
        ...theme.typography.h6,
        fontWeight: 700,
      },
      lockIcon: {
        color: theme.palette.warning.main,
        fontSize: 22,
      },
      closeButton: {
        ml: 'auto',
      },
      content: {
        display: 'flex',
        flexDirection: 'column',
        gap: theme.spacing(2),
        pt: `${theme.spacing(2)} !important`,
      },
      body: {
        ...theme.typography.body2,
        color: theme.palette.text.secondary,
      },
      cta: {
        fontWeight: 600,
      },
      dismiss: {
        color: theme.palette.text.secondary,
      },
    }),
    [theme],
  );
