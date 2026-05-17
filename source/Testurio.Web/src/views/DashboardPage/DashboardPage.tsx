'use client';

import { useCallback, useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useTranslation } from 'react-i18next';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Chip from '@mui/material/Chip';
import Skeleton from '@mui/material/Skeleton';
import Typography from '@mui/material/Typography';
import DashboardOutlinedIcon from '@mui/icons-material/DashboardOutlined';
import AddOutlinedIcon from '@mui/icons-material/AddOutlined';
import { useTheme, type Theme } from '@mui/material/styles';
import ProjectCard from '@/components/ProjectCard/ProjectCard';
import QuotaUsageBar from '@/components/QuotaUsageBar/QuotaUsageBar';
import { useDashboard } from '@/hooks/useDashboard';
import { useDashboardStream } from '@/hooks/useDashboardStream';
import { NEW_PROJECT_ROUTE } from '@/routes/routes';
import type {
  DashboardProjectSummary,
  DashboardUpdatedEvent,
  QuotaUsage,
} from '@/types/dashboard.types';

const SKELETON_COUNT = 6;

export default function DashboardPage() {
  const { t } = useTranslation('dashboard');
  const router = useRouter();
  const theme = useTheme();
  const styles = getStyles(theme);

  const { data, isPending, isError, refetch } = useDashboard();

  // Local overlay state — projects may be patched by SSE events without a full re-fetch.
  const [projectOverrides, setProjectOverrides] = useState<
    Record<string, DashboardProjectSummary>
  >({});
  const [quotaOverride, setQuotaOverride] = useState<QuotaUsage | null>(null);
  const [isReconnecting, setIsReconnecting] = useState(false);
  const [isFallback, setIsFallback] = useState(false);

  // Merge snapshot with any SSE-patched project summaries.
  const projects = useMemo<DashboardProjectSummary[]>(() => {
    if (!data) return [];
    return data.projects.map((p) => projectOverrides[p.projectId] ?? p);
  }, [data, projectOverrides]);

  const quotaUsage = quotaOverride ?? data?.quotaUsage ?? null;

  const handleCreateProject = useCallback(() => {
    router.push(NEW_PROJECT_ROUTE);
  }, [router]);

  const handleRetry = useCallback(() => {
    void refetch();
  }, [refetch]);

  const handleStreamUpdate = useCallback(
    (event: DashboardUpdatedEvent) => {
      setIsReconnecting(false);

      // If the project is not in the snapshot, trigger a full re-fetch to pick it up.
      const known = data?.projects.some((p) => p.projectId === event.projectId);
      if (!known) {
        void refetch();
        return;
      }

      setProjectOverrides((prev) => ({
        ...prev,
        [event.projectId]: {
          ...(prev[event.projectId] ?? data!.projects.find((p) => p.projectId === event.projectId)!),
          latestRun: event.latestRun,
        },
      }));

      if (event.quotaUsage !== null) {
        setQuotaOverride(event.quotaUsage);
      }
    },
    [data, refetch],
  );

  const handleFallback = useCallback(() => {
    setIsReconnecting(false);
    setIsFallback(true);
    void refetch();
  }, [refetch]);

  const handleReconnecting = useCallback((reconnecting: boolean) => {
    setIsReconnecting(reconnecting);
  }, []);

  // Open the SSE stream only after the snapshot has loaded successfully.
  useDashboardStream({
    enabled: !!data && !isFallback,
    onUpdate: handleStreamUpdate,
    onFallback: handleFallback,
    onReconnecting: handleReconnecting,
  });

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

    if (data && projects.length === 0) {
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
        {projects.map((project) => (
          <ProjectCard key={project.projectId} project={project} />
        ))}
      </Box>
    );
  }, [isPending, isError, data, projects, styles, t, handleRetry, handleCreateProject]);

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

      {/* SSE connection indicators — non-blocking, do not overlap interactive controls */}
      {isReconnecting && !isFallback && (
        <Chip
          label={t('stream.reconnecting')}
          color="warning"
          size="small"
          sx={styles.streamChip}
        />
      )}
      {isFallback && (
        <Alert severity="warning" sx={styles.fallbackAlert}>
          {t('stream.unavailable')}
        </Alert>
      )}

      {quotaUsage && <QuotaUsageBar quotaUsage={quotaUsage} />}

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
        borderRadius: `${theme.shape.borderRadius}px`,
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
      streamChip: {
        alignSelf: 'flex-start' as const,
      },
      fallbackAlert: {
        alignSelf: 'stretch' as const,
      },
    }),
    [theme],
  );
