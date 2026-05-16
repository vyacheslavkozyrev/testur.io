'use client';

import { useMemo, useState } from 'react';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Divider from '@mui/material/Divider';
import Drawer from '@mui/material/Drawer';
import IconButton from '@mui/material/IconButton';
import Skeleton from '@mui/material/Skeleton';
import Stack from '@mui/material/Stack';
import Tooltip from '@mui/material/Tooltip';
import Typography from '@mui/material/Typography';
import CloseIcon from '@mui/icons-material/Close';
import SettingsIcon from '@mui/icons-material/Settings';
import { useTheme, type Theme } from '@mui/material/styles';
import { useTranslation } from 'react-i18next';
import ScenarioCard from '@/components/ScenarioCard/ScenarioCard';
import { useRunDetail } from '@/hooks/useProjectHistory';
import { PROJECT_SETTINGS_ROUTE } from '@/routes/routes';

export interface RunDetailPanelProps {
  projectId: string;
  runId: string | null;
  onClose: () => void;
}

const DRAWER_WIDTH = 600;

export default function RunDetailPanel({ projectId, runId, onClose }: RunDetailPanelProps) {
  const { t } = useTranslation('history');
  const theme = useTheme();
  const styles = getStyles(theme);

  const [showRaw, setShowRaw] = useState(false);

  const { data: run, isPending } = useRunDetail(projectId, runId);

  const isOpen = runId !== null;

  const settingsHref = PROJECT_SETTINGS_ROUTE(projectId);

  const rawDisabled = !run?.rawCommentMarkdown;

  return (
    <Drawer
      anchor="right"
      open={isOpen}
      onClose={onClose}
      PaperProps={{ sx: styles.paper }}
    >
      <Box sx={styles.header}>
        <Stack direction="row" alignItems="center" spacing={1} sx={styles.headerContent}>
          <Typography variant="h6" sx={styles.headerTitle}>
            {isPending
              ? <Skeleton width={200} />
              : run
                ? t('panel.title', { storyTitle: run.storyTitle })
                : t('panel.noData')}
          </Typography>
          <Stack direction="row" spacing={0.5}>
            <Tooltip title={t('panel.projectSettingsTooltip')}>
              <IconButton
                component="a"
                href={settingsHref}
                aria-label={t('panel.projectSettingsAriaLabel')}
                size="small"
              >
                <SettingsIcon fontSize="small" />
              </IconButton>
            </Tooltip>
            <IconButton onClick={onClose} aria-label={t('panel.closeAriaLabel')} size="small">
              <CloseIcon fontSize="small" />
            </IconButton>
          </Stack>
        </Stack>

        {isPending ? (
          <Skeleton width={300} height={24} />
        ) : run ? (
          <Stack direction="row" alignItems="center" spacing={2}>
            <Typography variant="body2" color="text.secondary">
              {t(`verdict.${run.verdict.toLowerCase()}`)} — {t(`recommendation.${run.recommendation}`)}
            </Typography>
            <Typography variant="body2" color="text.secondary">
              {(run.totalDurationMs / 1000).toFixed(2)} s
            </Typography>
            <Typography variant="body2" color="text.secondary">
              {new Date(run.createdAt).toLocaleString()}
            </Typography>
          </Stack>
        ) : null}

        <Stack direction="row" spacing={1} sx={styles.toggleRow}>
          <Button
            size="small"
            variant={showRaw ? 'outlined' : 'contained'}
            onClick={() => setShowRaw(false)}
          >
            {t('panel.structuredView')}
          </Button>
          <Tooltip title={rawDisabled ? t('panel.rawReportDisabledTooltip') : ''}>
            <span>
              <Button
                size="small"
                variant={showRaw ? 'contained' : 'outlined'}
                onClick={() => setShowRaw(true)}
                disabled={rawDisabled}
              >
                {t('panel.rawReport')}
              </Button>
            </span>
          </Tooltip>
        </Stack>
      </Box>

      <Divider />

      <Box sx={styles.body}>
        {isPending ? (
          <Stack spacing={2}>
            {[1, 2, 3].map((i) => (
              <Skeleton key={i} variant="rectangular" height={80} />
            ))}
          </Stack>
        ) : run ? (
          showRaw ? (
            <Box component="pre" sx={styles.rawMarkdown}>
              {run.rawCommentMarkdown}
            </Box>
          ) : (
            <Stack spacing={2}>
              {run.scenarioResults.map((scenario, i) => (
                <ScenarioCard key={i} scenario={scenario} />
              ))}
            </Stack>
          )
        ) : null}
      </Box>
    </Drawer>
  );
}

// co-located at the bottom of the file
const getStyles = (theme: Theme) =>
  // eslint-disable-next-line react-hooks/rules-of-hooks
  useMemo(
    () => ({
      paper: {
        width: DRAWER_WIDTH,
        display: 'flex',
        flexDirection: 'column',
      },
      header: {
        p: theme.spacing(2),
        flexShrink: 0,
      },
      headerContent: {
        justifyContent: 'space-between',
        mb: theme.spacing(1),
      },
      headerTitle: {
        flex: 1,
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
      },
      toggleRow: {
        mt: theme.spacing(1.5),
      },
      body: {
        p: theme.spacing(2),
        flex: 1,
        overflowY: 'auto',
      },
      rawMarkdown: {
        fontFamily: 'monospace',
        fontSize: theme.typography.caption.fontSize,
        whiteSpace: 'pre-wrap',
        wordBreak: 'break-word',
        color: theme.palette.text.primary,
        m: 0,
      },
    }),
    [theme],
  );
