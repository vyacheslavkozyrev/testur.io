'use client';

import { useMemo } from 'react';
import Chip from '@mui/material/Chip';
import { useTheme, type Theme } from '@mui/material/styles';
import type { RunStatus } from '@/types/dashboard.types';

export interface RunStatusBadgeProps {
  status: RunStatus;
}

interface StatusConfig {
  label: string;
  color: 'default' | 'primary' | 'secondary' | 'error' | 'info' | 'success' | 'warning';
  /** When true, a CSS pulse animation is applied to the chip. */
  pulse?: boolean;
}

const STATUS_CONFIG: Record<RunStatus, StatusConfig> = {
  Queued:   { label: 'Queued',     color: 'default' },
  Running:  { label: 'Running',    color: 'warning', pulse: true },
  Passed:   { label: 'Passed',     color: 'success' },
  Failed:   { label: 'Failed',     color: 'error' },
  Cancelled: { label: 'Cancelled', color: 'default' },
  TimedOut: { label: 'Timed out',  color: 'warning' },
  NeverRun: { label: 'Never run',  color: 'default' },
};

export default function RunStatusBadge({ status }: RunStatusBadgeProps) {
  const theme = useTheme();
  const styles = getStyles(theme);
  const config = STATUS_CONFIG[status];

  return (
    <Chip
      label={config.label}
      color={config.color}
      size="small"
      sx={config.pulse ? styles.pulsing : undefined}
    />
  );
}

// co-located at the bottom of the file
const getStyles = (theme: Theme) =>
  // eslint-disable-next-line react-hooks/rules-of-hooks
  useMemo(
    () => ({
      pulsing: {
        '@keyframes pulse': {
          '0%':   { opacity: 1 },
          '50%':  { opacity: 0.55 },
          '100%': { opacity: 1 },
        },
        animation: 'pulse 1.4s ease-in-out infinite',
      },
    }),
    // theme intentionally omitted — pulse animation uses no theme tokens
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [],
  );
