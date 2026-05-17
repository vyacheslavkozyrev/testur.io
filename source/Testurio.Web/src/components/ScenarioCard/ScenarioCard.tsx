'use client';

import { useMemo } from 'react';
import Box from '@mui/material/Box';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import CheckCircleOutlineIcon from '@mui/icons-material/CheckCircleOutline';
import ErrorOutlineIcon from '@mui/icons-material/ErrorOutline';
import { useTheme, type Theme } from '@mui/material/styles';
import { useTranslation } from 'react-i18next';
import type { ScenarioSummary } from '@/types/history.types';

export interface ScenarioCardProps {
  scenario: ScenarioSummary;
}

function formatDuration(ms: number): string {
  return (ms / 1000).toFixed(2) + ' s';
}

/**
 * Screenshots are shown only for failed ui_e2e scenarios.
 */
function shouldShowScreenshots(scenario: ScenarioSummary): boolean {
  return scenario.testType === 'ui_e2e' && !scenario.passed && scenario.screenshotUris.length > 0;
}

export default function ScenarioCard({ scenario }: ScenarioCardProps) {
  const { t } = useTranslation('history');
  const theme = useTheme();
  const styles = getStyles(theme);

  const showScreenshots = shouldShowScreenshots(scenario);

  return (
    <Card variant="outlined" sx={styles.card}>
      <CardContent>
        <Stack direction="row" alignItems="center" spacing={1} sx={styles.header}>
          {scenario.passed ? (
            <CheckCircleOutlineIcon color="success" fontSize="small" />
          ) : (
            <ErrorOutlineIcon color="error" fontSize="small" />
          )}
          <Typography variant="subtitle2" sx={styles.title}>
            {scenario.title}
          </Typography>
          <Typography variant="body2" color="text.secondary" sx={styles.duration}>
            {formatDuration(scenario.durationMs)}
          </Typography>
        </Stack>

        {!scenario.passed && scenario.errorSummary && (
          <Box component="pre" sx={styles.errorBlock}>
            {scenario.errorSummary}
          </Box>
        )}

        {showScreenshots && (
          <Stack direction="row" flexWrap="wrap" gap={1} sx={styles.screenshots}>
            {scenario.screenshotUris.map((uri) => (
              <Box
                key={uri}
                component="a"
                href={uri}
                target="_blank"
                rel="noopener noreferrer"
                sx={styles.thumbnailLink}
              >
                <Box
                  component="img"
                  src={uri}
                  alt={t('scenarioCard.screenshotAlt')}
                  loading="lazy"
                  sx={styles.thumbnail}
                />
              </Box>
            ))}
          </Stack>
        )}
      </CardContent>
    </Card>
  );
}

// co-located at the bottom of the file
const getStyles = (theme: Theme) =>
  // eslint-disable-next-line react-hooks/rules-of-hooks
  useMemo(
    () => ({
      card: {
        width: '100%',
      },
      header: {
        alignItems: 'flex-start',
      },
      title: {
        flex: 1,
        color: theme.palette.text.primary,
      },
      duration: {
        whiteSpace: 'nowrap' as const,
      },
      errorBlock: {
        mt: theme.spacing(1),
        p: theme.spacing(1),
        backgroundColor: theme.palette.grey[100],
        borderRadius: theme.shape.borderRadius,
        fontFamily: 'monospace',
        fontSize: theme.typography.caption.fontSize,
        color: theme.palette.error.dark,
        overflowX: 'auto' as const,
        whiteSpace: 'pre-wrap' as const,
        wordBreak: 'break-word' as const,
      },
      screenshots: {
        mt: theme.spacing(1),
      },
      thumbnailLink: {
        display: 'block',
        borderRadius: theme.shape.borderRadius,
        overflow: 'hidden',
        border: `1px solid ${theme.palette.divider}`,
      },
      thumbnail: {
        width: 120,
        height: 80,
        objectFit: 'cover' as const,
        display: 'block',
      },
    }),
    [theme],
  );
