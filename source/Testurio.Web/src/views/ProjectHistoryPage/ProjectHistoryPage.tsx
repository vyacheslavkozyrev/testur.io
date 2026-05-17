'use client';

import { useCallback, useMemo, useState } from 'react';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Skeleton from '@mui/material/Skeleton';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import SettingsIcon from '@mui/icons-material/Settings';
import { useTheme, type Theme } from '@mui/material/styles';
import { useTranslation } from 'react-i18next';
import RunDetailPanel from '@/components/RunDetailPanel/RunDetailPanel';
import RunHistoryTable from '@/components/RunHistoryTable/RunHistoryTable';
import TrendChart from '@/components/TrendChart/TrendChart';
import { useProjectHistory } from '@/hooks/useProjectHistory';
import { PROJECT_SETTINGS_ROUTE } from '@/routes/routes';

export interface ProjectHistoryPageProps {
  projectId: string;
}

export default function ProjectHistoryPage({ projectId }: ProjectHistoryPageProps) {
  const { t } = useTranslation('history');
  const theme = useTheme();
  const styles = getStyles(theme);

  const [selectedRunId, setSelectedRunId] = useState<string | null>(null);

  const { data, isPending, isError, error, refetch } = useProjectHistory(projectId);

  const handleRowClick = useCallback((runId: string) => {
    setSelectedRunId(runId);
  }, []);

  const handlePanelClose = useCallback(() => {
    setSelectedRunId(null);
  }, []);

  const handleRetry = useCallback(() => {
    void refetch();
  }, [refetch]);

  // Detect 404 from API error response.
  const isNotFound = useMemo(() => {
    if (!isError || !error) return false;
    // ApiError shape: error.response?.status
    const apiError = error as { response?: { status?: number } };
    return apiError.response?.status === 404;
  }, [isError, error]);

  const content = useMemo(() => {
    if (isPending) {
      return (
        <Stack spacing={3}>
          <Skeleton variant="rectangular" height={260} />
          <Skeleton variant="rectangular" height={200} />
        </Stack>
      );
    }

    if (isNotFound) {
      return (
        <Alert severity="warning">{t('page.projectNotFound')}</Alert>
      );
    }

    if (isError) {
      return (
        <Alert
          severity="error"
          action={
            <Button color="inherit" size="small" onClick={handleRetry}>
              {t('page.retryButton')}
            </Button>
          }
        >
          {t('page.errorMessage')}
        </Alert>
      );
    }

    if (!data || data.runs.length === 0) {
      return (
        <Box sx={styles.emptyState}>
          <Typography color="text.secondary">{t('page.emptyState')}</Typography>
        </Box>
      );
    }

    return (
      <Stack spacing={4}>
        <TrendChart trendPoints={data.trendPoints} />
        <RunHistoryTable runs={data.runs} onRowClick={handleRowClick} />
      </Stack>
    );
  }, [isPending, isNotFound, isError, data, handleRetry, handleRowClick, styles.emptyState, t]);

  return (
    <Box sx={styles.root}>
      <Stack direction="row" justifyContent="space-between" alignItems="center" sx={styles.pageHeader}>
        <Typography variant="h5">{t('page.title')}</Typography>
        <Button
          variant="outlined"
          size="small"
          startIcon={<SettingsIcon />}
          component="a"
          href={PROJECT_SETTINGS_ROUTE(projectId)}
        >
          {t('page.projectSettingsButton')}
        </Button>
      </Stack>

      {content}

      <RunDetailPanel
        projectId={projectId}
        runId={selectedRunId}
        onClose={handlePanelClose}
      />
    </Box>
  );
}

// co-located at the bottom of the file
const getStyles = (theme: Theme) =>
  // eslint-disable-next-line react-hooks/rules-of-hooks
  useMemo(
    () => ({
      root: {
        p: theme.spacing(3),
      },
      pageHeader: {
        mb: theme.spacing(3),
      },
      emptyState: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        py: theme.spacing(6),
      },
    }),
    [theme],
  );
