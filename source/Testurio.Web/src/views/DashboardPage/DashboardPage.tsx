'use client';

import { useCallback, useMemo } from 'react';
import { useRouter } from 'next/navigation';
import { useTranslation } from 'react-i18next';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Skeleton from '@mui/material/Skeleton';
import Typography from '@mui/material/Typography';
import DashboardOutlinedIcon from '@mui/icons-material/DashboardOutlined';
import AddOutlinedIcon from '@mui/icons-material/AddOutlined';
import { useTheme, type Theme } from '@mui/material/styles';
import ProjectCard from '@/components/ProjectCard/ProjectCard';
import QuotaUsageBar from '@/components/QuotaUsageBar/QuotaUsageBar';
import { useDashboard } from '@/hooks/useDashboard';
import { NEW_PROJECT_ROUTE } from '@/routes/routes';
import type { DashboardUpdatedEvent } from '@/types/dashboard.types';

const SKELETON_COUNT = 6;

export interface DashboardPageProps {
  /**
   * Optional callback invoked when an SSE stream update is received.
   * No-op in this feature — wired by feature 0043 (DashboardEventRelay).
   */
  onStreamUpdate?: (event: DashboardUpdatedEvent) => void;
}

export default function DashboardPage({ onStreamUpdate: _onStreamUpdate }: DashboardPageProps) {
  const { t } = useTranslation('dashboard');
  const router = useRouter();
  const theme = useTheme();
  const styles = useMemo(() => getStyles(theme), [theme]);

  const { data, isPending, isError, refetch } = useDashboard();

  const handleCreateProject = useCallback(() => {
    router.push(NEW_PROJECT_ROUTE);
  }, [router]);

  const handleRetry = useCallback(() => {
    void refetch();
  }, [refetch]);

  const content = useMemo(() => {
    if (isPending) {
      return (
        <Box sx={styles.grid}>
          {Array.from({ length: SKELETON_COUNT }).map((_, i) => (
            // eslint-disable-next-line react/no-array-index-key
            <Skeleton key={i} variant="rectangular" sx={styles.skeleton} />
          ))}
        </Box>
      );
    }

    if (isError) {
      return (
        <Alert
          severity="error"
          action={
            <Button color="inherit" size="small" onClick={handleRetry}>
              {t('error.retryButton')}
            </Button>
          }
        >
          {t('error.message')}
        </Alert>
      );
    }

    if (data && data.projects.length === 0) {
      return (
        <Box sx={styles.emptyState}>
          <DashboardOutlinedIcon sx={styles.emptyIcon} />
          <Typography variant="h6" sx={styles.emptyHeading}>
            {t('emptyState.heading')}
          </Typography>
          <Typography variant="body2" sx={styles.emptyDescription}>
            {t('emptyState.description')}
          </Typography>
          <Button variant="contained" onClick={handleCreateProject}>
            {t('emptyState.ctaLabel')}
          </Button>
        </Box>
      );
    }

    return (
      <Box sx={styles.grid}>
        {data?.projects.map((project) => (
          <ProjectCard key={project.projectId} project={project} />
        ))}
      </Box>
    );
  }, [isPending, isError, data, styles, t, handleRetry, handleCreateProject]);

  return (
    <Box sx={styles.root}>
      <Box sx={styles.header}>
        <Typography variant="h5" sx={styles.pageTitle}>
          {t('page.title')}
        </Typography>
        <Button
          variant="contained"
          startIcon={<AddOutlinedIcon />}
          onClick={handleCreateProject}
        >
          {t('page.createButton')}
        </Button>
      </Box>

      {data && <QuotaUsageBar quotaUsage={data.quotaUsage} />}

      {content}
    </Box>
  );
}

// co-located at the bottom of the file
const getStyles = (theme: Theme) =>
  // eslint-disable-next-line react-hooks/rules-of-hooks
  useMemo(
    () => ({
      root: {
        padding: theme.spacing(4),
        maxWidth: 1100,
        margin: '0 auto',
        display: 'flex',
        flexDirection: 'column' as const,
        gap: theme.spacing(2),
      },
      header: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        gap: theme.spacing(2),
      },
      pageTitle: {
        ...theme.typography.h5,
        fontWeight: 600,
        color: theme.palette.text.primary,
      },
      grid: {
        display: 'grid',
        gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr', md: '1fr 1fr 1fr' },
        gap: theme.spacing(3),
      },
      skeleton: {
        height: 160,
        borderRadius: 1,
      },
      emptyState: {
        display: 'flex',
        flexDirection: 'column' as const,
        alignItems: 'center',
        justifyContent: 'center',
        gap: theme.spacing(2),
        padding: theme.spacing(8, 2),
        textAlign: 'center' as const,
      },
      emptyIcon: {
        fontSize: 64,
        color: theme.palette.text.disabled,
      },
      emptyHeading: {
        ...theme.typography.h6,
        color: theme.palette.text.primary,
      },
      emptyDescription: {
        ...theme.typography.body2,
        color: theme.palette.text.secondary,
        maxWidth: 400,
      },
    }),
    [theme],
  );
