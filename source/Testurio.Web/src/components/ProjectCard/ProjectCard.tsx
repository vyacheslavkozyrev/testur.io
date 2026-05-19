'use client';

import { useCallback, useMemo, useState } from 'react';
import Link from 'next/link';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Card from '@mui/material/Card';
import CardActionArea from '@mui/material/CardActionArea';
import CardContent from '@mui/material/CardContent';
import Typography from '@mui/material/Typography';
import PlayArrowOutlinedIcon from '@mui/icons-material/PlayArrowOutlined';
import { useTheme, type Theme } from '@mui/material/styles';
import { useTranslation } from 'react-i18next';
import RunStatusBadge from '@/components/RunStatusBadge/RunStatusBadge';
import UpgradeModal from '@/components/UpgradeModal/UpgradeModal';
import { useSubscriptionStatus } from '@/hooks/useBilling';
import { PROJECT_HISTORY_ROUTE } from '@/routes/routes';
import type { DashboardProjectSummary } from '@/types/dashboard.types';

export interface ProjectCardProps {
  project: DashboardProjectSummary;
}

export default function ProjectCard({ project }: ProjectCardProps) {
  const { t } = useTranslation('dashboard');
  const theme = useTheme();
  const styles = getStyles(theme);

  const [upgradeModalOpen, setUpgradeModalOpen] = useState(false);
  const { data: subscription } = useSubscriptionStatus();

  const isGated =
    subscription?.status === 'None' || subscription?.status === 'Expired';

  const startedAtFormatted = useMemo(() => {
    if (!project.latestRun) return null;
    try {
      return new Date(project.latestRun.startedAt).toLocaleString();
    } catch {
      return project.latestRun.startedAt;
    }
  }, [project.latestRun]);

  const handleRunClick = useCallback(
    (e: React.MouseEvent<HTMLButtonElement>) => {
      e.stopPropagation();
      e.preventDefault();
      if (isGated) {
        setUpgradeModalOpen(true);
      }
      // Non-gated run triggering is handled outside this card (PM tool webhooks).
    },
    [isGated],
  );

  const handleCloseModal = useCallback(() => setUpgradeModalOpen(false), []);

  return (
    <>
      <Card sx={styles.card}>
        <CardActionArea
          component={Link}
          href={PROJECT_HISTORY_ROUTE(project.projectId)}
          sx={styles.actionArea}
        >
          <CardContent sx={styles.content}>
            <Box sx={styles.header}>
              <Typography variant="h6" sx={styles.name} noWrap>
                {project.name}
              </Typography>
              <RunStatusBadge status={project.latestRun?.status ?? 'NeverRun'} />
            </Box>

            <Typography variant="body2" sx={styles.url} noWrap>
              {project.productUrl}
            </Typography>

            {startedAtFormatted ? (
              <Typography variant="caption" sx={styles.timestamp}>
                {t('card.lastRun', { time: startedAtFormatted })}
              </Typography>
            ) : (
              <Typography variant="caption" sx={styles.timestampMuted}>
                {t('card.neverRun')}
              </Typography>
            )}

            {/* Run test button is always visible; clicking while gated opens the UpgradeModal.
                Non-gated runs are triggered via PM tool webhooks (no manual trigger in MVP). */}
            <Button
              size="small"
              variant="outlined"
              startIcon={<PlayArrowOutlinedIcon />}
              onClick={handleRunClick}
              sx={styles.runButton}
            >
              {t('card.runTest')}
            </Button>
          </CardContent>
        </CardActionArea>
      </Card>
      <UpgradeModal open={upgradeModalOpen} onClose={handleCloseModal} />
    </>
  );
}

// co-located at the bottom of the file
const getStyles = (theme: Theme) =>
  // eslint-disable-next-line react-hooks/rules-of-hooks
  useMemo(
    () => ({
      card: {
        height: '100%',
        display: 'flex',
        flexDirection: 'column' as const,
        border: `1px solid ${theme.palette.divider}`,
        borderRadius: `${theme.shape.borderRadius}px`,
        transition: 'box-shadow 150ms ease',
        '&:hover': {
          boxShadow: theme.shadows[4],
        },
      },
      actionArea: {
        height: '100%',
        alignItems: 'flex-start' as const,
      },
      content: {
        display: 'flex',
        flexDirection: 'column' as const,
        gap: theme.spacing(1),
        width: '100%',
      },
      header: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        gap: theme.spacing(1),
        minWidth: 0,
      },
      name: {
        ...theme.typography.h6,
        color: theme.palette.text.primary,
        flex: 1,
        minWidth: 0,
      },
      url: {
        ...theme.typography.body2,
        color: theme.palette.text.secondary,
      },
      timestamp: {
        ...theme.typography.caption,
        color: theme.palette.text.secondary,
      },
      timestampMuted: {
        ...theme.typography.caption,
        color: theme.palette.text.disabled,
      },
      runButton: {
        alignSelf: 'flex-start' as const,
        mt: theme.spacing(0.5),
      },
    }),
    [theme],
  );
