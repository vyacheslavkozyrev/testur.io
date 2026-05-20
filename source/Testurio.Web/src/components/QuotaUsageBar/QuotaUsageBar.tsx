'use client';

import { useMemo } from 'react';
import Box from '@mui/material/Box';
import LinearProgress from '@mui/material/LinearProgress';
import Typography from '@mui/material/Typography';
import { useTheme, type Theme } from '@mui/material/styles';
import { useTranslation } from 'react-i18next';
import type { QuotaUsage } from '@/types/dashboard.types';

export interface QuotaUsageBarProps {
  quotaUsage: QuotaUsage;
}

export default function QuotaUsageBar({ quotaUsage }: QuotaUsageBarProps) {
  const { t } = useTranslation('dashboard');
  const theme = useTheme();
  const styles = getStyles(theme);

  const { usedThisMonth, monthlyLimit, resetsAt } = quotaUsage;
  const hasNoPlan = monthlyLimit === 0;
  const isUnlimited = monthlyLimit === -1;
  const showBar = !hasNoPlan && !isUnlimited;

  // AC-041: red when at or over limit; AC-042: amber when >= 80% of limit.
  const isAtOrOverLimit = showBar && usedThisMonth >= monthlyLimit;
  const isNearLimit = showBar && !isAtOrOverLimit && usedThisMonth >= monthlyLimit * 0.8;

  const progressValue = useMemo(() => {
    if (!showBar || monthlyLimit === 0) return 0;
    return Math.min((usedThisMonth / monthlyLimit) * 100, 100);
  }, [showBar, usedThisMonth, monthlyLimit]);

  const progressColor = isAtOrOverLimit ? 'error' : isNearLimit ? 'warning' : 'primary';

  const resetsAtFormatted = useMemo(() => {
    try {
      return new Date(resetsAt).toLocaleDateString(undefined, {
        year: 'numeric',
        month: 'long',
        day: 'numeric',
      });
    } catch {
      return resetsAt;
    }
  }, [resetsAt]);

  return (
    <Box sx={styles.root}>
      <Box sx={styles.row}>
        {hasNoPlan ? (
          <Typography variant="body2" sx={styles.label}>
            {t('quota.noActivePlan')}
          </Typography>
        ) : isUnlimited ? (
          <Typography variant="body2" sx={styles.label}>
            {t('quota.usageUnlimited', { used: usedThisMonth })}
          </Typography>
        ) : (
          <Typography variant="body2" sx={styles.label}>
            {t('quota.usage', { used: usedThisMonth, limit: monthlyLimit })}
          </Typography>
        )}
        {showBar && (
          <Typography variant="caption" sx={styles.resetsAt}>
            {t('quota.resetsOn', { date: resetsAtFormatted })}
          </Typography>
        )}
      </Box>
      {showBar && (
        <LinearProgress
          variant="determinate"
          value={progressValue}
          color={progressColor}
          sx={styles.bar}
        />
      )}
    </Box>
  );
}

// co-located at the bottom of the file
const getStyles = (theme: Theme) =>
  // eslint-disable-next-line react-hooks/rules-of-hooks
  useMemo(
    () => ({
      root: {
        padding: theme.spacing(1.5, 2),
        backgroundColor: theme.palette.background.paper,
        border: `1px solid ${theme.palette.divider}`,
        borderRadius: `${theme.shape.borderRadius}px`,
        display: 'flex',
        flexDirection: 'column' as const,
        gap: theme.spacing(0.75),
      },
      row: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        gap: theme.spacing(2),
      },
      label: {
        color: theme.palette.text.primary,
        fontWeight: 500,
      },
      resetsAt: {
        color: theme.palette.text.secondary,
      },
      bar: {
        borderRadius: `${theme.shape.borderRadius}px`,
        height: 6,
      },
    }),
    [theme],
  );
