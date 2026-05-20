'use client';

import { useCallback, useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useTranslation } from 'react-i18next';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import { useTheme, type Theme } from '@mui/material/styles';
import ProjectForm from '@/components/ProjectForm/ProjectForm';
import UpgradeModal from '@/components/UpgradeModal/UpgradeModal';
import { useCreateProject } from '@/hooks/useProject';
import { useSubscriptionStatus } from '@/hooks/useBilling';
import type { CreateProjectRequest } from '@/types/project.types';

export default function ProjectCreatePage() {
  const { t } = useTranslation('project');
  const router = useRouter();
  const theme = useTheme();
  const styles = getStyles(theme);

  const [upgradeModalOpen, setUpgradeModalOpen] = useState(false);
  const createProject = useCreateProject();
  const { data: subscription } = useSubscriptionStatus();

  const isGated =
    subscription?.status === 'None' || subscription?.status === 'Expired';

  const handleSubmit = useCallback(
    (data: CreateProjectRequest) => {
      if (isGated) {
        setUpgradeModalOpen(true);
        return;
      }
      createProject.mutate(data, {
        onSuccess: (project) => {
          router.push(`/projects/${project.projectId}/settings`);
        },
      });
    },
    [createProject, isGated, router],
  );

  const handleFormInteract = useCallback(() => {
    if (isGated) setUpgradeModalOpen(true);
  }, [isGated]);

  const handleCloseUpgradeModal = useCallback(() => setUpgradeModalOpen(false), []);

  return (
    <Box sx={styles.root}>
      <Typography variant="h4" sx={styles.pageTitle}>
        {t('create.pageTitle')}
      </Typography>
      <Box onClick={handleFormInteract}>
        <ProjectForm isSubmitting={createProject.isPending} onSubmit={handleSubmit} />
      </Box>
      <UpgradeModal
        open={upgradeModalOpen}
        onClose={handleCloseUpgradeModal}
      />
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
        color: theme.palette.text.primary,
      },
    }),
    [theme],
  );
