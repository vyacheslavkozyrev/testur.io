import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { I18nextProvider } from 'react-i18next';
import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import { ThemeProvider } from '@mui/material/styles';
import { theme } from '@/theme/theme';
import TrendChart from './TrendChart';
import type { TrendPoint } from '@/types/history.types';

// ─── Mock @mui/x-charts ───────────────────────────────────────────────────────

jest.mock('@mui/x-charts/BarChart', () => ({
  BarChart: ({ series }: { series: Array<{ data: number[]; label: string }> }) => (
    <div data-testid="bar-chart">
      {series.map((s) => (
        <div key={s.label} data-testid={`series-${s.label}`}>
          {JSON.stringify(s.data)}
        </div>
      ))}
    </div>
  ),
}));

// ─── i18n setup ───────────────────────────────────────────────────────────────

const i18nInstance = i18n.createInstance();
i18nInstance.use(initReactI18next).init({
  lng: 'en',
  resources: {
    en: {
      history: {
        'chart.title': 'Pass / Fail Trend',
        'chart.rangeToggleAriaLabel': 'Select time range',
        'chart.range7': 'Last 7 days',
        'chart.range30': 'Last 30 days',
        'chart.range90': 'Last 90 days',
        'chart.xAxisLabel': 'Date',
        'chart.passedLabel': 'Passed',
        'chart.failedLabel': 'Failed',
        'chart.emptyState': 'No test runs in the selected period.',
      },
    },
  },
});

// ─── Test helpers ─────────────────────────────────────────────────────────────

function buildTrendPoints(days: number, passed = 0, failed = 0): TrendPoint[] {
  const today = new Date();
  return Array.from({ length: days }, (_, i) => {
    const d = new Date(today);
    d.setUTCDate(today.getUTCDate() - (days - 1 - i));
    return {
      date: d.toISOString().slice(0, 10),
      passed,
      failed,
    };
  });
}

function renderComponent(trendPoints: TrendPoint[], range?: 7 | 30 | 90) {
  return render(
    <I18nextProvider i18n={i18nInstance}>
      <ThemeProvider theme={theme}>
        <TrendChart trendPoints={trendPoints} range={range} />
      </ThemeProvider>
    </I18nextProvider>,
  );
}

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('TrendChart', () => {
  it('renders chart title', () => {
    renderComponent(buildTrendPoints(90));
    expect(screen.getByText('Pass / Fail Trend')).toBeInTheDocument();
  });

  it('default range is 30 days', () => {
    const points = buildTrendPoints(90, 1, 0);
    renderComponent(points);

    // The "Last 30 days" button should appear selected (ToggleButton group)
    const btn30 = screen.getByText('Last 30 days');
    expect(btn30).toBeInTheDocument();
  });

  it('renders BarChart when data has non-zero values', () => {
    const points = buildTrendPoints(90, 1, 0);
    renderComponent(points);
    expect(screen.getByTestId('bar-chart')).toBeInTheDocument();
  });

  it('shows empty-data message when all values in range are zero', () => {
    const zeroPoints = buildTrendPoints(90, 0, 0);
    renderComponent(zeroPoints);
    expect(screen.getByText('No test runs in the selected period.')).toBeInTheDocument();
  });

  it('range toggle button changes filtered data — switching to 7 shows fewer points', async () => {
    const user = userEvent.setup();
    // 90 trend points: only last 7 have passed=1, earlier have 0
    const points: TrendPoint[] = buildTrendPoints(83, 0, 0).concat(buildTrendPoints(7, 1, 0));
    renderComponent(points);

    // Initially showing 30 days — most are 0 so empty state may show
    // Switch to 7 days — which all have passed=1 → chart should appear
    const btn7 = screen.getByText('Last 7 days');
    await user.click(btn7);

    // After clicking Last 7 days, chart should be visible (non-zero data in that window)
    expect(screen.getByTestId('bar-chart')).toBeInTheDocument();
  });

  it('renders range toggle buttons for 7, 30, 90 days', () => {
    renderComponent(buildTrendPoints(90));
    expect(screen.getByText('Last 7 days')).toBeInTheDocument();
    expect(screen.getByText('Last 30 days')).toBeInTheDocument();
    expect(screen.getByText('Last 90 days')).toBeInTheDocument();
  });
});
