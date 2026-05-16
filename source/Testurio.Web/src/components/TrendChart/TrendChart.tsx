'use client';

import { useMemo, useState } from 'react';
import Box from '@mui/material/Box';
import Stack from '@mui/material/Stack';
import ToggleButton from '@mui/material/ToggleButton';
import ToggleButtonGroup from '@mui/material/ToggleButtonGroup';
import Typography from '@mui/material/Typography';
import { useTheme, type Theme } from '@mui/material/styles';
import { BarChart } from '@mui/x-charts/BarChart';
import { useTranslation } from 'react-i18next';
import type { TrendPoint } from '@/types/history.types';

type TimeRange = 7 | 30 | 90;

export interface TrendChartProps {
  trendPoints: TrendPoint[];
  range?: TimeRange;
}

export default function TrendChart({ trendPoints, range: initialRange = 30 }: TrendChartProps) {
  const { t } = useTranslation('history');
  const theme = useTheme();
  const styles = getStyles(theme);

  const [range, setRange] = useState<TimeRange>(initialRange);

  const filteredPoints = useMemo(() => {
    return trendPoints.slice(trendPoints.length - range);
  }, [trendPoints, range]);

  const allZero = useMemo(
    () => filteredPoints.every((p) => p.passed === 0 && p.failed === 0),
    [filteredPoints],
  );

  const xLabels = useMemo(
    () => filteredPoints.map((p) => p.date),
    [filteredPoints],
  );

  const passedData = useMemo(
    () => filteredPoints.map((p) => p.passed),
    [filteredPoints],
  );

  const failedData = useMemo(
    () => filteredPoints.map((p) => p.failed),
    [filteredPoints],
  );

  const handleRangeChange = (_: React.MouseEvent<HTMLElement>, value: TimeRange | null) => {
    if (value !== null) setRange(value);
  };

  return (
    <Box sx={styles.root}>
      <Stack direction="row" justifyContent="space-between" alignItems="center" sx={styles.header}>
        <Typography variant="h6">{t('chart.title')}</Typography>
        <ToggleButtonGroup
          value={range}
          exclusive
          onChange={handleRangeChange}
          size="small"
          aria-label={t('chart.rangeToggleAriaLabel')}
        >
          <ToggleButton value={7}>{t('chart.range7')}</ToggleButton>
          <ToggleButton value={30}>{t('chart.range30')}</ToggleButton>
          <ToggleButton value={90}>{t('chart.range90')}</ToggleButton>
        </ToggleButtonGroup>
      </Stack>

      {allZero ? (
        <Box sx={styles.emptyState}>
          <Typography color="text.secondary">{t('chart.emptyState')}</Typography>
        </Box>
      ) : (
        <BarChart
          height={260}
          xAxis={[{ scaleType: 'band', data: xLabels, label: t('chart.xAxisLabel') }]}
          series={[
            {
              data: passedData,
              label: t('chart.passedLabel'),
              color: theme.palette.success.main,
              stack: 'total',
            },
            {
              data: failedData,
              label: t('chart.failedLabel'),
              color: theme.palette.error.main,
              stack: 'total',
            },
          ]}
        />
      )}
    </Box>
  );
}

// co-located at the bottom of the file
const getStyles = (theme: Theme) =>
  // eslint-disable-next-line react-hooks/rules-of-hooks
  useMemo(
    () => ({
      root: {
        width: '100%',
      },
      header: {
        mb: theme.spacing(2),
      },
      emptyState: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        height: 260,
      },
    }),
    [theme],
  );
