'use client';

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useTranslation } from 'react-i18next';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Breadcrumbs from '@mui/material/Breadcrumbs';
import Button from '@mui/material/Button';
import CircularProgress from '@mui/material/CircularProgress';
import Link from '@mui/material/Link';
import Paper from '@mui/material/Paper';
import Tab from '@mui/material/Tab';
import Tabs from '@mui/material/Tabs';
import Typography from '@mui/material/Typography';
import NextLink from 'next/link';
import { useTheme, type Theme } from '@mui/material/styles';
import { PROJECTS_ROUTE } from '@/routes/routes';
import CustomPromptField from '@/components/CustomPromptField/CustomPromptField';
import ProjectDeleteDialog from '@/components/ProjectDeleteDialog/ProjectDeleteDialog';
import ProjectForm, { type ProjectFormHandle } from '@/components/ProjectForm/ProjectForm';
import SaveBar, { type SaveBarState } from '@/components/SaveBar/SaveBar';
import { useProject, useUpdateProject, useDeleteProject } from '@/hooks/useProject';
import { useUpdateReportSettings } from '@/hooks/useReportSettings';
import IntegrationPage from '@/views/IntegrationPage/IntegrationPage';
import ReportSettingsSection, {
  type ReportSettingsSectionHandle,
} from '@/views/ProjectSettings/ReportSettingsSection';
import type { CreateProjectRequest, UpdateProjectRequest } from '@/types/project.types';

type TabValue = 'settings' | 'integration';

interface SectionErrors {
  projectInfo: boolean;
  reportSettings: boolean;
}

interface PendingSections {
  projectInfo: boolean;
  reportSettings: boolean;
}

export default function ProjectSettingsPage() {
  const { projectId } = useParams<{ projectId: string }>();
  const { t } = useTranslation('project');
  const router = useRouter();
  const theme = useTheme();
  const styles = getStyles(theme);

  const { data: project, isPending, isError } = useProject(projectId ?? '');
  const updateProject = useUpdateProject(projectId ?? '');
  const deleteProject = useDeleteProject();
  const updateReportSettings = useUpdateReportSettings(projectId ?? '');

  const [activeTab, setActiveTab] = useState<TabValue>('settings');
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
  const [customPrompt, setCustomPrompt] = useState<string>('');
  const [savedCustomPrompt, setSavedCustomPrompt] = useState<string>('');
  const [saveBarState, setSaveBarState] = useState<SaveBarState>('clean');
  const [sectionErrors, setSectionErrors] = useState<SectionErrors>({ projectInfo: false, reportSettings: false });
  const [pendingSections, setPendingSections] = useState<PendingSections>({ projectInfo: true, reportSettings: true });

  const projectFormRef = useRef<ProjectFormHandle>(null);
  const reportSettingsRef = useRef<ReportSettingsSectionHandle>(null);
  const capturedFormData = useRef<UpdateProjectRequest | null>(null);
  const saveTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    if (project) {
      const serverValue = project.customPrompt ?? '';
      setCustomPrompt(serverValue);
      setSavedCustomPrompt(serverValue);
    }
  }, [project?.projectId]); // eslint-disable-line react-hooks/exhaustive-deps

  const handleCustomPromptChange = useCallback((value: string) => {
    setCustomPrompt(value);
  }, []);

  const computeDirty = useCallback((): boolean => {
    const formDirty = projectFormRef.current?.isDirty ?? false;
    const reportDirty = reportSettingsRef.current?.isDirty ?? false;
    const promptDirty = customPrompt !== savedCustomPrompt;
    return formDirty || reportDirty || promptDirty;
  }, [customPrompt, savedCustomPrompt]);

  // Poll computeDirty() after every render — form isDirty lives in a ref and
  // cannot be a dep, so no deps array is intentional. Guard prevents setState
  // when nothing changed, which avoids triggering a follow-up render.
  useEffect(() => {
    if (saveBarState === 'saving' || saveBarState === 'saved') return;
    const next = computeDirty() ? 'dirty' : 'clean';
    if (next !== saveBarState) setSaveBarState(next);
  });

  useEffect(() => () => { if (saveTimerRef.current) window.clearTimeout(saveTimerRef.current); }, []);

  const handleTabChange = useCallback((_: React.SyntheticEvent, value: TabValue) => {
    setActiveTab(value);
  }, []);

  const handleGoToSettings = useCallback(() => {
    setActiveTab('settings');
  }, []);

  const handleCaptureFormData = useCallback(
    (data: CreateProjectRequest | UpdateProjectRequest) => {
      capturedFormData.current = { ...data, customPrompt: customPrompt || null };
    },
    [customPrompt],
  );

  const handleSaveAll = useCallback(async () => {
    setSaveBarState('saving');
    setSectionErrors({ projectInfo: false, reportSettings: false });

    let projectInfoOk = !pendingSections.projectInfo;
    let reportSettingsOk = !pendingSections.reportSettings;

    if (pendingSections.projectInfo) {
      const valid = await projectFormRef.current?.triggerSubmit();
      if (!valid) {
        setSaveBarState('dirty');
        return;
      }
      try {
        await updateProject.mutateAsync(capturedFormData.current!);
        projectInfoOk = true;
      } catch {
        projectInfoOk = false;
      }
    }

    if (pendingSections.reportSettings) {
      const values = reportSettingsRef.current?.getValues();
      if (values) {
        try {
          await updateReportSettings.mutateAsync(values);
          // Clear child pending state immediately so isDirty returns false
          // before the next React Query invalidation renders.
          reportSettingsRef.current?.clearDirty();
          reportSettingsOk = true;
        } catch {
          reportSettingsOk = false;
        }
      } else {
        reportSettingsOk = true;
      }
    }

    const newErrors: SectionErrors = {
      projectInfo: !projectInfoOk,
      reportSettings: !reportSettingsOk,
    };
    setSectionErrors(newErrors);

    const anyError = newErrors.projectInfo || newErrors.reportSettings;
    if (anyError) {
      setPendingSections({ projectInfo: newErrors.projectInfo, reportSettings: newErrors.reportSettings });
      setSaveBarState('dirty');
    } else {
      setSavedCustomPrompt(customPrompt);
      setPendingSections({ projectInfo: true, reportSettings: true });
      setSaveBarState('saved');
      if (saveTimerRef.current) window.clearTimeout(saveTimerRef.current);
      saveTimerRef.current = window.setTimeout(() => setSaveBarState('clean'), 2000);
    }
  }, [pendingSections, customPrompt, updateProject, updateReportSettings]);

  const handleDeleteConfirm = useCallback(() => {
    deleteProject.mutate(projectId ?? '', {
      onSuccess: () => {
        setDeleteDialogOpen(false);
        router.push(PROJECTS_ROUTE);
      },
    });
  }, [deleteProject, projectId, router]);

  const handleDeleteCancel = useCallback(() => setDeleteDialogOpen(false), []);
  const handleOpenDeleteDialog = useCallback(() => setDeleteDialogOpen(true), []);

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

  const hasDirtySettings = saveBarState === 'dirty';

  return (
    <Box sx={styles.root}>
      <Breadcrumbs sx={styles.breadcrumbs}>
        <Link component={NextLink} href={PROJECTS_ROUTE} underline="hover" color="text.secondary" variant="body2">
          {t('settings.breadcrumbProjects')}
        </Link>
        <Typography variant="body2" color="text.primary">
          {t('settings.breadcrumbSettings')}
        </Typography>
      </Breadcrumbs>

      <Typography variant="h5" sx={styles.pageTitle}>
        {t('settings.title', { name: project.name })}
      </Typography>

      {activeTab === 'integration' && hasDirtySettings && (
        <Alert
          severity="warning"
          action={
            <Button color="inherit" size="small" onClick={handleGoToSettings}>
              {t('unsavedBanner.link')}
            </Button>
          }
        >
          {t('unsavedBanner.message')}
        </Alert>
      )}

      <Tabs value={activeTab} onChange={handleTabChange} sx={styles.tabs}>
        <Tab label={t('tabs.settings')} value="settings" />
        <Tab label={t('tabs.integration')} value="integration" />
      </Tabs>

      {activeTab === 'settings' && (
        <Box sx={styles.settingsContent}>
          {/* Project info card */}
          <Paper variant="outlined" sx={styles.card}>
            <Typography variant="h6" sx={styles.cardTitle}>
              {t('form.titleEdit')}
            </Typography>
            {sectionErrors.projectInfo && (
              <Alert severity="error" sx={styles.cardAlert}>
                {t('settings.saveError')}
              </Alert>
            )}
            <ProjectForm
              ref={projectFormRef}
              project={project}
              isSubmitting={updateProject.isPending}
              onSubmit={handleCaptureFormData}
            />
          </Paper>

          {/* Custom prompt card */}
          <Paper variant="outlined" sx={styles.card}>
            <CustomPromptField
              projectId={project.projectId}
              testingStrategy={project.testingStrategy}
              value={customPrompt}
              onChange={handleCustomPromptChange}
            />
          </Paper>

          {/* Report settings card */}
          <Paper variant="outlined" sx={styles.card}>
            {sectionErrors.reportSettings && (
              <Alert severity="error" sx={styles.cardAlert}>
                {t('settings.saveError')}
              </Alert>
            )}
            <ReportSettingsSection
              ref={reportSettingsRef}
              projectId={project.projectId}
              testType={project.testingStrategy}
            />
          </Paper>

          <SaveBar state={saveBarState} onClick={handleSaveAll} />

          {/* Danger zone card */}
          <Paper variant="outlined" sx={styles.dangerCard}>
            <Box sx={styles.dangerContent}>
              <Box>
                <Typography variant="subtitle1" sx={styles.dangerTitle}>
                  {t('settings.dangerZone.title')}
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  {t('settings.dangerZone.description')}
                </Typography>
              </Box>
              <Button
                variant="outlined"
                color="error"
                size="small"
                onClick={handleOpenDeleteDialog}
                disabled={deleteProject.isPending}
                sx={styles.dangerButton}
              >
                {t('settings.dangerZone.deleteButton')}
              </Button>
            </Box>
          </Paper>
        </Box>
      )}

      {activeTab === 'integration' && <IntegrationPage embedded />}

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

const getStyles = (theme: Theme) =>
  // eslint-disable-next-line react-hooks/rules-of-hooks
  useMemo(
    () => ({
      root: {
        padding: theme.spacing(4),
        maxWidth: 860,
        margin: '0 auto',
        display: 'flex',
        flexDirection: 'column',
        gap: theme.spacing(2),
      },
      centered: {
        display: 'flex',
        justifyContent: 'center',
        padding: theme.spacing(8),
      },
      breadcrumbs: {
        marginBottom: theme.spacing(-1),
      },
      pageTitle: {
        ...theme.typography.h5,
        color: theme.palette.text.primary,
        fontWeight: 600,
      },
      tabs: {
        borderBottom: `1px solid ${theme.palette.divider}`,
        marginBottom: theme.spacing(1),
      },
      settingsContent: {
        display: 'flex',
        flexDirection: 'column',
        gap: theme.spacing(2),
      },
      card: {
        padding: theme.spacing(3),
        borderRadius: 1,
        display: 'flex',
        flexDirection: 'column' as const,
        gap: theme.spacing(2),
      },
      cardTitle: {
        ...theme.typography.h6,
        color: theme.palette.text.primary,
        fontWeight: 600,
      },
      cardAlert: {
        marginBottom: theme.spacing(2),
      },
      dangerCard: {
        borderRadius: 1,
        borderColor: theme.palette.error.light,
        padding: theme.spacing(3),
      },
      dangerContent: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        gap: theme.spacing(2),
      },
      dangerTitle: {
        ...theme.typography.subtitle1,
        color: theme.palette.error.main,
      },
      dangerButton: {
        flexShrink: 0,
      },
    }),
    [theme],
  );
