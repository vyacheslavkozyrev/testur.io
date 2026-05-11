'use client';

import { useCallback, useMemo } from 'react';
import { useRouter } from 'next/navigation';
import { useTranslation } from 'react-i18next';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import { useTheme, type Theme } from '@mui/material/styles';
import ProjectForm from '@/components/ProjectForm/ProjectForm';
import { useCreateProject } from '@/hooks/useProject';
import type { CreateProjectRequest } from '@/types/project.types';

export default function ProjectCreatePage() {
  const { t } = useTranslation('project');
  const router = useRouter();
  const theme = useTheme();
  const styles = getStyles(theme);

  const createProject = useCreateProject();

  const handleSubmit = useCallback(
    (data: CreateProjectRequest) => {
      createProject.mutate(data, {
        onSuccess: (project) => {
          router.push(`/projects/${project.projectId}/settings`);
        },
      });
    },
    [createProject, router],
  );

  return (
    <Box sx={styles.root}>
      <Typography variant="h4" sx={styles.pageTitle}>
        {t('create.pageTitle')}
      </Typography>
      <ProjectForm isSubmitting={createProject.isPending} onSubmit={handleSubmit} />
    </Box>
  );
}

const getStyles = (theme: Theme) =>
  // eslint-disable-next-line react-hooks/rules-of-hooks
  useMemo(
    () => ({
      root: {
        padding: theme.spacing(4),
        maxWidth: 800,
        margin: '0 auto',
        display: 'flex',
        flexDirection: 'column',
        gap: theme.spacing(4),
      },
      pageTitle: {
        ...theme.typography.h4,
        color: theme.palette.text.primary,
      },
    }),
    [theme],
  );
