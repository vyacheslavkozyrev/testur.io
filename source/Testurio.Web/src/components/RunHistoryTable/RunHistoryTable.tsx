'use client';

import { useMemo, useCallback } from 'react';
import Table from '@mui/material/Table';
import TableBody from '@mui/material/TableBody';
import TableCell from '@mui/material/TableCell';
import TableContainer from '@mui/material/TableContainer';
import TableHead from '@mui/material/TableHead';
import TableRow from '@mui/material/TableRow';
import Paper from '@mui/material/Paper';
import Typography from '@mui/material/Typography';
import { useTheme, type Theme } from '@mui/material/styles';
import { useTranslation } from 'react-i18next';
import RunStatusBadge from '@/components/RunStatusBadge/RunStatusBadge';
import type { RunHistoryItem } from '@/types/history.types';
import type { RunStatus } from '@/types/dashboard.types';

export interface RunHistoryTableProps {
  runs: RunHistoryItem[];
  onRowClick: (runId: string) => void;
}

function verdictToStatus(verdict: string): RunStatus {
  return verdict === 'PASSED' ? 'Passed' : 'Failed';
}

function formatDuration(totalMs: number): string {
  return (totalMs / 1000).toFixed(2) + ' s';
}

function formatDate(iso: string): string {
  return new Date(iso).toLocaleString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

export default function RunHistoryTable({ runs, onRowClick }: RunHistoryTableProps) {
  const { t } = useTranslation('history');
  const theme = useTheme();
  const styles = getStyles(theme);

  const handleRowClick = useCallback(
    (runId: string) => () => {
      onRowClick(runId);
    },
    [onRowClick],
  );

  return (
    <TableContainer component={Paper} variant="outlined">
      <Table aria-label={t('table.ariaLabel')}>
        <TableHead>
          <TableRow>
            <TableCell>{t('table.columnStory')}</TableCell>
            <TableCell>{t('table.columnStatus')}</TableCell>
            <TableCell>{t('table.columnDate')}</TableCell>
            <TableCell>{t('table.columnDuration')}</TableCell>
            <TableCell>{t('table.columnScenarios')}</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {runs.map((run) => (
            <TableRow
              key={run.id}
              hover
              onClick={handleRowClick(run.runId)}
              sx={styles.row}
            >
              <TableCell>
                <Typography variant="body2" noWrap sx={styles.storyTitle}>
                  {run.storyTitle}
                </Typography>
              </TableCell>
              <TableCell>
                <RunStatusBadge status={verdictToStatus(run.verdict)} />
              </TableCell>
              <TableCell>
                <Typography variant="body2">{formatDate(run.createdAt)}</Typography>
              </TableCell>
              <TableCell>
                <Typography variant="body2">{formatDuration(run.totalDurationMs)}</Typography>
              </TableCell>
              <TableCell>
                <Typography variant="body2">
                  {t('table.scenarioCount', {
                    passed: run.passedApiScenarios + run.passedUiE2eScenarios,
                    total: run.totalApiScenarios + run.totalUiE2eScenarios,
                  })}
                </Typography>
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </TableContainer>
  );
}

// co-located at the bottom of the file
const getStyles = (theme: Theme) =>
  // eslint-disable-next-line react-hooks/rules-of-hooks
  useMemo(
    () => ({
      row: {
        cursor: 'pointer',
      },
      storyTitle: {
        maxWidth: 320,
        color: theme.palette.text.primary,
      },
    }),
    [theme],
  );
