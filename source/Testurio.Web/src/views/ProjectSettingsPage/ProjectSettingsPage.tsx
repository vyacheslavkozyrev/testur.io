'use client';

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useTranslation } from 'react-i18next';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import CircularProgress from '@mui/material/CircularProgress';
import Tab from '@mui/material/Tab';
import Tabs from '@mui/material/Tabs';
import Typography from '@mui/material/Typography';
import { useTheme, type Theme } from '@mui/material/styles';
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
  // Capture form data from ProjectForm's onSubmit callback before firing mutateAsync
  const capturedFormData = useRef<UpdateProjectRequest | null>(null);

  // Sync customPrompt with server data on load.
  useEffect(() => {
    if (project) {
      const serverValue = project.customPrompt ?? '';
      setCustomPrompt(serverValue);
      setSavedCustomPrompt(serverValue);
    }
  }, [project?.projectId]); // eslint-disable-line react-hooks/exhaustive-deps

  const computeDirty = useCallback((): boolean => {
    const formDirty = projectFormRef.current?.isDirty ?? false;
    const reportDirty = reportSettingsRef.current?.isDirty ?? false;
    const promptDirty = customPrompt !== savedCustomPrompt;
    return formDirty || reportDirty || promptDirty;
  }, [customPrompt, savedCustomPrompt]);

  // Recompute save bar state after each render (outside saving/saved lock).
  useEffect(() => {
    if (saveBarState === 'saving' || saveBarState === 'saved') return;
    const dirty = computeDirty();
    setSaveBarState(dirty ? 'dirty' : 'clean');
  });

  const handleTabChange = useCallback((_: React.SyntheticEvent, value: TabValue) => {
    setActiveTab(value);
  }, []);

  const handleCustomPromptChange = useCallback((value: string) => {
    setCustomPrompt(value);
  }, []);

  // Called by ProjectForm's onSubmit; captures validated data for use in handleSaveAll.
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
      // triggerSubmit validates and calls handleCaptureFormData(data) if valid
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
      setTimeout(() => setSaveBarState('clean'), 2000);
    }
  }, [pendingSections, customPrompt, updateProject, updateReportSettings]);

  const handleDeleteConfirm = useCallback(() => {
    deleteProject.mutate(projectId ?? '', {
      onSuccess: () => {
        setDeleteDialogOpen(false);
        router.push('/projects');
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

  const hasDirtySettings = computeDirty();

  return (
    <Box sx={styles.root}>
      <Typography variant="h4" sx={styles.pageTitle}>
        {t('settings.title', { name: project.name })}
      </Typography>

      {/* Unsaved-changes banner — shown on Integration tab when Settings are dirty */}
      {activeTab === 'integration' && hasDirtySettings && (
        <Alert
          severity="warning"
          action={
            <Button color="inherit" size="small" onClick={() => setActiveTab('settings')}>
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
          {sectionErrors.projectInfo && (
            <Alert severity="error" sx={styles.sectionAlert}>
              {t('settings.saveError')}
            </Alert>
          )}

          <ProjectForm
            ref={projectFormRef}
            project={project}
            isSubmitting={updateProject.isPending}
            onSubmit={handleCaptureFormData}
          />

          <Box sx={styles.customPromptSection}>
            <Typography variant="h6" sx={styles.sectionTitle}>
              {t('customPrompt.section.title')}
            </Typography>
            <Typography variant="body2" sx={styles.sectionDescription}>
              {t('customPrompt.section.description')}
            </Typography>
            <CustomPromptField
              projectId={project.projectId}
              testingStrategy={project.testingStrategy}
              value={customPrompt}
              onChange={handleCustomPromptChange}
            />
          </Box>

          <Box sx={styles.reportSettingsSection}>
            {sectionErrors.reportSettings && (
              <Alert severity="error" sx={styles.sectionAlert}>
                {t('settings.saveError')}
              </Alert>
            )}
            <ReportSettingsSection
              ref={reportSettingsRef}
              projectId={project.projectId}
              testType={project.testingStrategy}
            />
          </Box>

          <SaveBar state={saveBarState} onClick={handleSaveAll} />

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
        </Box>
      )}

      {activeTab === 'integration' && <IntegrationPage />}

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
        gap: theme.spacing(3),
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
      tabs: {
        borderBottom: `1px solid ${theme.palette.divider}`,
      },
      settingsContent: {
        display: 'flex',
        flexDirection: 'column',
        gap: theme.spacing(4),
      },
      sectionAlert: {
        width: '100%',
      },
      customPromptSection: {
        display: 'flex',
        flexDirection: 'column',
        gap: theme.spacing(2),
        borderTop: `1px solid ${theme.palette.divider}`,
        paddingTop: theme.spacing(3),
      },
      sectionTitle: {
        ...theme.typography.h6,
        color: theme.palette.text.primary,
      },
      sectionDescription: {
        color: theme.palette.text.secondary,
      },
      reportSettingsSection: {
        display: 'flex',
        flexDirection: 'column',
        gap: theme.spacing(2),
        borderTop: `1px solid ${theme.palette.divider}`,
        paddingTop: theme.spacing(3),
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
