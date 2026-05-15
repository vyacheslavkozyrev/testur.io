'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import CircularProgress from '@mui/material/CircularProgress';
import Divider from '@mui/material/Divider';
import Typography from '@mui/material/Typography';
import { useTheme, type Theme } from '@mui/material/styles';
import ReportAttachmentToggles from '@/components/ReportAttachmentToggles/ReportAttachmentToggles';
import ReportTemplateUpload from '@/components/ReportTemplateUpload/ReportTemplateUpload';
import {
  useReportSettings,
  useUpdateReportSettings,
} from '@/hooks/useReportSettings';

export interface ReportSettingsSectionProps {
  projectId: string;
  testType: string;
}

export default function ReportSettingsSection({
  projectId,
  testType,
}: ReportSettingsSectionProps) {
  const { t } = useTranslation('reportSettings');
  const theme = useTheme();
  const styles = getStyles(theme);

  const { data: settings, isPending, isError } = useReportSettings(projectId);
  const updateSettings = useUpdateReportSettings(projectId);

  // Pending edits — undefined means "no local override; use server value".
  const [pendingLogs, setPendingLogs] = useState<boolean | undefined>(undefined);
  const [pendingScreenshots, setPendingScreenshots] = useState<boolean | undefined>(undefined);

  // Read toggle values directly from server data; local pending edits shadow them.
  const effectiveLogs = pendingLogs ?? settings?.reportIncludeLogs ?? true;
  const effectiveScreenshots = pendingScreenshots ?? settings?.reportIncludeScreenshots ?? true;

  // Reset pending edits after a successful save so the next render reads fresh server data.
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

  const handleSave = useCallback(() => {
    updateSettings.mutate({
      reportIncludeLogs: effectiveLogs,
      reportIncludeScreenshots: effectiveScreenshots,
    });
  }, [updateSettings, effectiveLogs, effectiveScreenshots]);

  if (isPending) {
    return (
      <Box sx={styles.loading}>
        <CircularProgress size={24} />
      </Box>
    );
  }

  if (isError || !settings) {
    return (
      <Alert severity="error">{t('loadError')}</Alert>
    );
  }

  return (
    <Box sx={styles.root}>
      <Typography variant="h6" sx={styles.sectionTitle}>
        {t('sectionTitle')}
      </Typography>

      {updateSettings.isError && (
        <Alert severity="error">{t('saveError')}</Alert>
      )}
      {updateSettings.isSuccess && (
        <Alert severity="success">{t('saveSuccess')}</Alert>
      )}

      {/* Report Template Upload (US-001 – US-003) */}
      <ReportTemplateUpload
        projectId={projectId}
        currentFileName={settings.reportTemplateFileName}
      />

      <Divider />

      {/* Attachment Toggles (US-005) */}
      <ReportAttachmentToggles
        testType={testType}
        includeLogs={effectiveLogs}
        includeScreenshots={effectiveScreenshots}
        onChange={handleToggleChange}
      />

      <Box>
        <Button
          variant="contained"
          onClick={handleSave}
          disabled={updateSettings.isPending}
        >
          {updateSettings.isPending ? (
            <CircularProgress size={16} sx={{ mr: 1 }} />
          ) : null}
          {t('saveButton')}
        </Button>
      </Box>
    </Box>
  );
}

const getStyles = (theme: Theme) =>
  // eslint-disable-next-line react-hooks/rules-of-hooks
  useMemo(
    () => ({
      root: {
        display: 'flex',
        flexDirection: 'column' as const,
        gap: theme.spacing(3),
      },
      sectionTitle: {
        ...theme.typography.h6,
        color: theme.palette.text.primary,
      },
      loading: {
        display: 'flex',
        justifyContent: 'center',
        padding: theme.spacing(3),
      },
    }),
    [theme],
  );
