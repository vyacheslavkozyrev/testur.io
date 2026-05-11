'use client';

import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import CircularProgress from '@mui/material/CircularProgress';
import Typography from '@mui/material/Typography';
import { useTheme, type Theme } from '@mui/material/styles';
import type { TestConnectionResult } from '@/types/pmTool.types';

export interface TestConnectionButtonProps {
  isLoading: boolean;
  result: TestConnectionResult | null;
  onTest: () => void;
}

export default function TestConnectionButton({
  isLoading,
  result,
  onTest,
}: TestConnectionButtonProps) {
  const { t } = useTranslation('pmTool');
  const theme = useTheme();
  const styles = getStyles(theme);

  return (
    <Box sx={styles.root}>
      <Button
        variant="outlined"
        onClick={onTest}
        disabled={isLoading}
        startIcon={isLoading ? <CircularProgress size={16} color="inherit" /> : null}
      >
        {t('testConnection.button')}
      </Button>

      {result && (
        <Typography
          variant="body2"
          sx={
            result.status === 'ok'
              ? styles.successText
              : styles.errorText
          }
        >
          {result.status === 'ok' && t('testConnection.success')}
          {result.status === 'auth_error' && t('testConnection.authError')}
          {result.status === 'unreachable' && t('testConnection.unreachable')}
        </Typography>
      )}
    </Box>
  );
}

const getStyles = (theme: Theme) =>
  // eslint-disable-next-line react-hooks/rules-of-hooks
  useMemo(
    () => ({
      root: {
        display: 'flex',
        alignItems: 'center',
        gap: theme.spacing(2),
      },
      successText: {
        color: theme.palette.success.main,
        ...theme.typography.body2,
      },
      errorText: {
        color: theme.palette.error.main,
        ...theme.typography.body2,
      },
    }),
    [theme],
  );
