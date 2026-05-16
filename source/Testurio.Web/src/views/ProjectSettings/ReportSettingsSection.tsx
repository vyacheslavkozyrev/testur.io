'use client';

import { forwardRef, useCallback, useEffect, useImperativeHandle, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import CircularProgress from '@mui/material/CircularProgress';
import Divider from '@mui/material/Divider';
import { useTheme, type Theme } from '@mui/material/styles';
import ReportAttachmentToggles from '@/components/ReportAttachmentToggles/ReportAttachmentToggles';
import ReportTemplateUpload from '@/components/ReportTemplateUpload/ReportTemplateUpload';
import {
  useReportSettings,
  useUpdateReportSettings,
} from '@/hooks/useReportSettings';

export interface ReportSettingsSectionHandle {
  getValues: () => { reportIncludeLogs: boolean; reportIncludeScreenshots: boolean };
  isDirty: boolean;
}

export interface ReportSettingsSectionProps {
  projectId: string;
  testType: string;
}

const ReportSettingsSection = forwardRef<ReportSettingsSectionHandle, ReportSettingsSectionProps>(
  function ReportSettingsSection({ projectId, testType }, ref) {
    const { t } = useTranslation('reportSettings');
    const theme = useTheme();
    const styles = getStyles(theme);

    const { data: settings, isPending, isError } = useReportSettings(projectId);
    const updateSettings = useUpdateReportSettings(projectId);

    const [pendingLogs, setPendingLogs] = useState<boolean | undefined>(undefined);
    const [pendingScreenshots, setPendingScreenshots] = useState<boolean | undefined>(undefined);

    const effectiveLogs = pendingLogs ?? settings?.reportIncludeLogs ?? true;
    const effectiveScreenshots = pendingScreenshots ?? settings?.reportIncludeScreenshots ?? true;

    const isDirty = pendingLogs !== undefined || pendingScreenshots !== undefined;

    useEffect(() => {
      if (updateSettings.isSuccess) {
        setPendingLogs(undefined);
        setPendingScreenshots(undefined);
        const timer = setTimeout(() => updateSettings.reset(), 3000);
        return () => clearTimeout(timer);
      }
    }, [updateSettings.isSuccess, updateSettings]);

    const handleToggleChange = useCallback(
      (values: { includeLogs: boolean; includeScreenshots: boolean }) => {
        setPendingLogs(values.includeLogs);
        setPendingScreenshots(values.includeScreenshots);
      },
      [],
    );

    useImperativeHandle(ref, () => ({
      getValues: () => ({
        reportIncludeLogs: effectiveLogs,
        reportIncludeScreenshots: effectiveScreenshots,
      }),
      get isDirty() {
        return isDirty;
      },
    }));

    if (isPending) {
      return (
        <Box sx={styles.loading}>
          <CircularProgress size={24} />
        </Box>
      );
    }

    if (isError || !settings) {
      return <Alert severity="error">{t('loadError')}</Alert>;
    }

    return (
      <Box sx={styles.root}>
        <ReportTemplateUpload
          projectId={projectId}
          currentFileName={settings.reportTemplateFileName}
        />

        <Divider />

        <ReportAttachmentToggles
          testType={testType}
          includeLogs={effectiveLogs}
          includeScreenshots={effectiveScreenshots}
          onChange={handleToggleChange}
        />
      </Box>
    );
  },
);

export default ReportSettingsSection;

const getStyles = (theme: Theme) =>
  // eslint-disable-next-line react-hooks/rules-of-hooks
  useMemo(
    () => ({
      root: {
        display: 'flex',
        flexDirection: 'column' as const,
        gap: theme.spacing(3),
      },
      loading: {
        display: 'flex',
        justifyContent: 'center',
        padding: theme.spacing(3),
      },
    }),
    [theme],
  );
