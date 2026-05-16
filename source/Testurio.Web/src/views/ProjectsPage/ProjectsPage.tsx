'use client';

import { useCallback, useMemo } from 'react';
import { useRouter } from 'next/navigation';
import { useTranslation } from 'react-i18next';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Skeleton from '@mui/material/Skeleton';
import Typography from '@mui/material/Typography';
import AddOutlinedIcon from '@mui/icons-material/AddOutlined';
import FolderOpenOutlinedIcon from '@mui/icons-material/FolderOpenOutlined';
import { useTheme, type Theme } from '@mui/material/styles';
import ProjectListCard from '@/components/ProjectListCard/ProjectListCard';
import { useProjects } from '@/hooks/useProject';
import { NEW_PROJECT_ROUTE } from '@/routes/routes';

const SKELETON_COUNT = 6;

export default function ProjectsPage() {
  const { t } = useTranslation('projects');
  const router = useRouter();
  const theme = useTheme();
  const styles = getStyles(theme);

  const { data: projects, isPending, isError, refetch } = useProjects();

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

    if (projects && projects.length === 0) {
      return (
        <Box sx={styles.emptyState}>
          <FolderOpenOutlinedIcon sx={styles.emptyIcon} />
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
        {projects?.map((project) => (
          <ProjectListCard key={project.projectId} project={project} />
        ))}
      </Box>
    );
  }, [isPending, isError, projects, styles, t, handleRetry, handleCreateProject]);

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

      {content}
    </Box>
  );
}

// co-located at the bottom of the file
const getStyles = (theme: Theme) => ({
  root: {
    padding: theme.spacing(4),
    maxWidth: 860,
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
  grid: {
    display: 'grid',
    gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr', md: '1fr 1fr 1fr' },
    gap: theme.spacing(3),
  },
  pageTitle: {
    ...theme.typography.h5,
    fontWeight: 600,
    color: theme.palette.text.primary,
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
});
