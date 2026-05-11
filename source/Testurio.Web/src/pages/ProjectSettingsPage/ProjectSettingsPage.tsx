import { useCallback, useMemo, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import CircularProgress from '@mui/material/CircularProgress';
import Typography from '@mui/material/Typography';
import { useTheme, type Theme } from '@mui/material/styles';
import ProjectDeleteDialog from '@/components/ProjectDeleteDialog/ProjectDeleteDialog';
import ProjectForm from '@/components/ProjectForm/ProjectForm';
import { useProject, useUpdateProject, useDeleteProject } from '@/hooks/useProject';
import type { UpdateProjectRequest } from '@/types/project.types';

export default function ProjectSettingsPage() {
  const { projectId = '' } = useParams<{ projectId: string }>();
  const { t } = useTranslation('project');
  const navigate = useNavigate();
  const theme = useTheme();
  const styles = getStyles(theme);

  const { data: project, isPending, isError } = useProject(projectId);
  const updateProject = useUpdateProject(projectId);
  const deleteProject = useDeleteProject();

  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);

  const handleUpdate = useCallback(
    (data: UpdateProjectRequest) => {
      updateProject.mutate(data);
    },
    [updateProject],
  );

  const handleDeleteConfirm = useCallback(() => {
    deleteProject.mutate(projectId, {
      onSuccess: () => {
        setDeleteDialogOpen(false);
        navigate('/projects');
      },
    });
  }, [deleteProject, projectId, navigate]);

  const handleDeleteCancel = useCallback(() => {
    setDeleteDialogOpen(false);
  }, []);

  const handleOpenDeleteDialog = useCallback(() => {
    setDeleteDialogOpen(true);
  }, []);

  if (isPending) {
    return (
      <Box sx={styles.centered}>
        <CircularProgress />
      </Box>
    );
  }

  if (isError || !project) {
    return (
      <Box sx={styles.root}>
        <Alert severity="error">{t('settings.loadError')}</Alert>
      </Box>
    );
  }

  return (
    <Box sx={styles.root}>
      <Typography variant="h4" sx={styles.pageTitle}>
        {t('settings.title', { name: project.name })}
      </Typography>

      {updateProject.isError && (
        <Alert severity="error" sx={styles.alert}>
          {t('settings.saveError')}
        </Alert>
      )}

      {updateProject.isSuccess && (
        <Alert severity="success" sx={styles.alert}>
          {t('settings.saveSuccess')}
        </Alert>
      )}

      <ProjectForm
        project={project}
        isSubmitting={updateProject.isPending}
        onSubmit={handleUpdate}
      />

      <Box sx={styles.dangerZone}>
        <Typography variant="h6" color="error">
          {t('settings.dangerZone.title')}
        </Typography>
        <Button
          variant="outlined"
          color="error"
          onClick={handleOpenDeleteDialog}
          disabled={deleteProject.isPending}
        >
          {t('settings.dangerZone.deleteButton')}
        </Button>
      </Box>

      <ProjectDeleteDialog
        open={deleteDialogOpen}
        projectName={project.name}
        isDeleting={deleteProject.isPending}
        onConfirm={handleDeleteConfirm}
        onCancel={handleDeleteCancel}
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
        padding: theme.spacing(4),
        maxWidth: 800,
        margin: '0 auto',
        display: 'flex',
        flexDirection: 'column',
        gap: theme.spacing(4),
      },
      centered: {
        display: 'flex',
        justifyContent: 'center',
        padding: theme.spacing(8),
      },
      pageTitle: {
        ...theme.typography.h4,
        color: theme.palette.text.primary,
      },
      alert: {
        width: '100%',
      },
      dangerZone: {
        display: 'flex',
        flexDirection: 'column',
        gap: theme.spacing(2),
        borderTop: `1px solid ${theme.palette.error.light}`,
        paddingTop: theme.spacing(3),
        marginTop: theme.spacing(2),
      },
    }),
    [theme],
  );
