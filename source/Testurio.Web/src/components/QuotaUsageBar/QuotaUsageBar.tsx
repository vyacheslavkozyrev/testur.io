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
  const styles = useMemo(() => getStyles(theme), [theme]);

  const { usedToday, dailyLimit, resetsAt } = quotaUsage;
  const hasNoPlan = dailyLimit === 0;
  const isOver = !hasNoPlan && usedToday > dailyLimit;
  const isAtLimit = !hasNoPlan && usedToday === dailyLimit;

  const progressValue = useMemo(() => {
    if (hasNoPlan || dailyLimit === 0) return 0;
    return Math.min((usedToday / dailyLimit) * 100, 100);
  }, [hasNoPlan, usedToday, dailyLimit]);

  const progressColor = isOver ? 'error' : isAtLimit ? 'warning' : 'primary';

  const resetsAtFormatted = useMemo(() => {
    try {
      return new Date(resetsAt).toLocaleTimeString(undefined, {
        hour: '2-digit',
        minute: '2-digit',
        timeZoneName: 'short',
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
        ) : (
          <Typography variant="body2" sx={styles.label}>
            {t('quota.usage', { used: usedToday, limit: dailyLimit })}
          </Typography>
        )}
        <Typography variant="caption" sx={styles.resetsAt}>
          {t('quota.resetsAt', { time: resetsAtFormatted })}
        </Typography>
      </Box>
      {!hasNoPlan && (
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
        borderRadius: theme.shape.borderRadius,
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
        ...theme.typography.body2,
        color: theme.palette.text.primary,
        fontWeight: 500,
      },
      resetsAt: {
        ...theme.typography.caption,
        color: theme.palette.text.secondary,
      },
      bar: {
        borderRadius: theme.shape.borderRadius,
        height: 6,
      },
    }),
    [theme],
  );
