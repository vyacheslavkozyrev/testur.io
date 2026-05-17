'use client';

import { useMemo } from 'react';
import Box from '@mui/material/Box';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import CheckCircleOutlineIcon from '@mui/icons-material/CheckCircleOutline';
import ErrorOutlineIcon from '@mui/icons-material/ErrorOutline';
import RemoveCircleOutlineIcon from '@mui/icons-material/RemoveCircleOutline';
import { useTheme, type Theme } from '@mui/material/styles';
import { useTranslation } from 'react-i18next';
import type { StepSummary } from '@/types/history.types';

export interface UiStepListProps {
  steps: StepSummary[];
}

const ASSERTION_ACTIONS = new Set(['assert_visible', 'assert_text', 'assert_url']);

function isSkipped(step: StepSummary): boolean {
  return !step.passed && step.errorMessage?.startsWith('Skipped') === true;
}

function hasScreenshot(step: StepSummary): boolean {
  return (
    !step.passed &&
    ASSERTION_ACTIONS.has(step.action) &&
    step.screenshotBlobUri !== null
  );
}

export default function UiStepList({ steps }: UiStepListProps) {
  const { t } = useTranslation('history');
  const theme = useTheme();
  const styles = getStyles(theme);

  return (
    <Box sx={styles.root}>
      {steps.map((step) => {
        const skipped = isSkipped(step);
        const showScreenshot = hasScreenshot(step);

        return (
          <Box key={step.stepIndex} sx={styles.row}>
            <Stack direction="row" alignItems="flex-start" spacing={1}>
              <Typography variant="caption" sx={styles.index}>
                {step.stepIndex}
              </Typography>

              {step.passed ? (
                <CheckCircleOutlineIcon color="success" sx={styles.icon} />
              ) : skipped ? (
                <RemoveCircleOutlineIcon sx={styles.skippedIcon} />
              ) : (
                <ErrorOutlineIcon color="error" sx={styles.icon} />
              )}

              <Box sx={styles.detail}>
                <Typography
                  variant="caption"
                  component="code"
                  sx={skipped ? styles.actionMuted : styles.action}
                >
                  {step.action}
                </Typography>

                {!step.passed && step.errorMessage && (
                  <Typography variant="caption" sx={skipped ? styles.errorMuted : styles.error}>
                    {step.errorMessage}
                  </Typography>
                )}

                {showScreenshot && (
                  <Box
                    component="a"
                    href={step.screenshotBlobUri!}
                    target="_blank"
                    rel="noopener noreferrer"
                    sx={styles.thumbnailLink}
                  >
                    <Box
                      component="img"
                      src={step.screenshotBlobUri!}
                      alt={t('uiStepList.screenshotAlt', { index: step.stepIndex })}
                      loading="lazy"
                      sx={styles.thumbnail}
                    />
                  </Box>
                )}
              </Box>
            </Stack>
          </Box>
        );
      })}
    </Box>
  );
}

// co-located at the bottom of the file
const getStyles = (theme: Theme) =>
  // eslint-disable-next-line react-hooks/rules-of-hooks
  useMemo(
    () => ({
      root: {
        mt: theme.spacing(1),
        borderLeft: `2px solid ${theme.palette.divider}`,
        pl: theme.spacing(1),
      },
      row: {
        py: theme.spacing(0.5),
      },
      index: {
        minWidth: 20,
        color: theme.palette.text.secondary,
        fontVariantNumeric: 'tabular-nums',
        lineHeight: 1.6,
      },
      icon: {
        fontSize: theme.typography.body2.fontSize,
        mt: '2px',
      },
      skippedIcon: {
        fontSize: theme.typography.body2.fontSize,
        mt: '2px',
        color: theme.palette.text.disabled,
      },
      detail: {
        display: 'flex',
        flexDirection: 'column' as const,
        gap: theme.spacing(0.5),
        flex: 1,
      },
      action: {
        fontFamily: 'monospace',
        color: theme.palette.text.primary,
        lineHeight: 1.6,
      },
      actionMuted: {
        fontFamily: 'monospace',
        color: theme.palette.text.disabled,
        lineHeight: 1.6,
      },
      error: {
        color: theme.palette.error.main,
        fontStyle: 'italic' as const,
      },
      errorMuted: {
        color: theme.palette.text.disabled,
        fontStyle: 'italic' as const,
      },
      thumbnailLink: {
        display: 'inline-block',
        borderRadius: theme.shape.borderRadius,
        overflow: 'hidden',
        border: `1px solid ${theme.palette.divider}`,
        mt: theme.spacing(0.5),
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
