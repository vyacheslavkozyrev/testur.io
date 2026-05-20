'use client';

import { useCallback, useMemo } from 'react';
import Box from '@mui/material/Box';
import ToggleButton from '@mui/material/ToggleButton';
import ToggleButtonGroup from '@mui/material/ToggleButtonGroup';
import Typography from '@mui/material/Typography';
import Chip from '@mui/material/Chip';
import { useTheme, type Theme } from '@mui/material/styles';
import { useTranslation } from 'react-i18next';
import type { BillingInterval } from '@/types/plan.types';

export interface BillingIntervalToggleProps {
  value: BillingInterval;
  annualDiscountPercent?: number;
  onChange: (interval: BillingInterval) => void;
}

export default function BillingIntervalToggle({
  value,
  annualDiscountPercent,
  onChange,
}: BillingIntervalToggleProps) {
  const { t } = useTranslation('pricing');
  const theme = useTheme();
  const styles = getStyles(theme);

  const handleChange = useCallback(
    (_: React.MouseEvent<HTMLElement>, newInterval: BillingInterval | null) => {
      // ToggleButtonGroup can return null if same button clicked — keep current value
      if (newInterval !== null) {
        onChange(newInterval);
      }
    },
    [onChange],
  );

  return (
    <Box sx={styles.root}>
      <ToggleButtonGroup
        value={value}
        exclusive
        onChange={handleChange}
        aria-label={t('billingToggle.ariaLabel')}
        sx={styles.group}
      >
        <ToggleButton value="monthly" aria-label={t('billingToggle.monthly')} sx={styles.button}>
          <Typography sx={styles.label}>{t('billingToggle.monthly')}</Typography>
        </ToggleButton>
        <ToggleButton value="annual" aria-label={t('billingToggle.annual')} sx={styles.button}>
          <Typography sx={styles.label}>{t('billingToggle.annual')}</Typography>
          {annualDiscountPercent && annualDiscountPercent > 0 && (
            <Chip
              label={t('billingToggle.saveBadge', { percent: annualDiscountPercent })}
              size="small"
              color="success"
              sx={styles.badge}
            />
          )}
        </ToggleButton>
      </ToggleButtonGroup>
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
        justifyContent: 'center',
        mb: theme.spacing(4),
      },
      group: {
        borderRadius: theme.shape.borderRadius * 4,
        border: `1px solid ${theme.palette.divider}`,
        backgroundColor: theme.palette.background.paper,
        '& .MuiToggleButton-root': {
          border: 'none',
          borderRadius: `${theme.shape.borderRadius * 4}px !important`,
          px: theme.spacing(3),
          py: theme.spacing(1),
          minHeight: 44,
          textTransform: 'none',
        },
        '& .MuiToggleButton-root.Mui-selected': {
          backgroundColor: theme.palette.primary.main,
          color: '#ffffff',
          '&:hover': {
            backgroundColor: theme.palette.primary.dark,
          },
        },
      },
      button: {
        display: 'flex',
        gap: theme.spacing(1),
        alignItems: 'center',
      },
      label: {
        fontWeight: 600,
      },
      badge: {
        height: 20,
        fontSize: '0.7rem',
        fontWeight: 700,
      },
    }),
    [theme],
  );
