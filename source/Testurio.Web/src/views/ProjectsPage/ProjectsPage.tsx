'use client';

import { useCallback, useMemo } from 'react';
import { useRouter } from 'next/navigation';
import { useTranslation } from 'react-i18next';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Grid from '@mui/material/Grid';
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
  const styles = useMemo(() => getStyles(theme), [theme]);

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
        <Grid container spacing={3}>
          {Array.from({ length: SKELETON_COUNT }).map((_, i) => (
            // eslint-disable-next-line react/no-array-index-key
            <Grid key={i} size={{ xs: 12, sm: 6, md: 4 }}>
              <Skeleton variant="rectangular" sx={styles.skeleton} />
            </Grid>
          ))}
        </Grid>
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
      <Grid container spacing={3}>
        {projects?.map((project) => (
          <Grid key={project.projectId} size={{ xs: 12, sm: 6, md: 4 }}>
            <ProjectListCard project={project} />
          </Grid>
        ))}
      </Grid>
    );
  }, [isPending, isError, projects, styles, t, handleRetry, handleCreateProject]);

  return (
    <Box sx={styles.root}>
      <Box sx={styles.header}>
        <Typography variant="h4" sx={styles.pageTitle}>
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
    display: 'flex',
    flexDirection: 'column' as const,
    gap: theme.spacing(4),
  },
  header: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
    gap: theme.spacing(2),
  },
  pageTitle: {
    ...theme.typography.h4,
    color: theme.palette.text.primary,
  },
  skeleton: {
    height: 160,
    borderRadius: theme.shape.borderRadius,
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
