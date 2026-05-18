import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { I18nextProvider } from 'react-i18next';
import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import { ThemeProvider } from '@mui/material/styles';
import { theme } from '@/theme/theme';
import ScenarioCard from './ScenarioCard';
import type { ScenarioSummary } from '@/types/history.types';

// ─── i18n setup ───────────────────────────────────────────────────────────────

const i18nInstance = i18n.createInstance();
i18nInstance.use(initReactI18next).init({
  lng: 'en',
  resources: {
    en: {
      history: {
        'scenarioCard.screenshotAlt': 'Test screenshot',
        'scenarioCard.expandSteps': 'Expand step details',
        'scenarioCard.collapseSteps': 'Collapse step details',
        'uiStepList.screenshotAlt': 'Screenshot at step {{index}}',
      },
    },
  },
});

// ─── Test helpers ─────────────────────────────────────────────────────────────

function makeScenario(overrides: Partial<ScenarioSummary> = {}): ScenarioSummary {
  return {
    scenarioId: 'scenario-1',
    title: 'POST /auth — returns 200',
    passed: true,
    durationMs: 320,
    errorSummary: null,
    testType: 'api',
    screenshotUris: [],
    steps: null,
    ...overrides,
  };
}

function renderComponent(scenario: ScenarioSummary) {
  return render(
    <I18nextProvider i18n={i18nInstance}>
      <ThemeProvider theme={theme}>
        <ScenarioCard scenario={scenario} />
      </ThemeProvider>
    </I18nextProvider>,
  );
}

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('ScenarioCard', () => {
  it('renders scenario title', () => {
    renderComponent(makeScenario());
    expect(screen.getByText('POST /auth — returns 200')).toBeInTheDocument();
  });

  it('passed scenario shows no error block', () => {
    renderComponent(makeScenario({ passed: true, errorSummary: null }));
    expect(screen.queryByRole('code')).not.toBeInTheDocument();
  });

  it('passed scenario shows no thumbnails', () => {
    renderComponent(
      makeScenario({ passed: true, testType: 'ui_e2e', screenshotUris: ['https://blob.example.com/shot.png'] }),
    );
    // Thumbnails should NOT appear for passed scenarios even if URIs are populated
    expect(screen.queryByAltText('Test screenshot')).not.toBeInTheDocument();
  });

  it('failed API scenario shows errorSummary block', () => {
    const { container } = renderComponent(
      makeScenario({ passed: false, testType: 'api', errorSummary: 'Expected 200 but got 404' }),
    );
    const pre = container.querySelector('pre');
    expect(pre).not.toBeNull();
    expect(pre?.textContent).toContain('Expected 200 but got 404');
  });

  it('failed API scenario shows no thumbnails', () => {
    renderComponent(
      makeScenario({
        passed: false,
        testType: 'api',
        errorSummary: 'Error',
        screenshotUris: ['https://blob.example.com/shot.png'],
      }),
    );
    // API scenarios never show screenshots
    expect(screen.queryByAltText('Test screenshot')).not.toBeInTheDocument();
  });

  it('failed UI E2E scenario shows thumbnails with loading=lazy and href to blob URL', () => {
    const uri = 'https://blob.example.com/step-1.png';
    renderComponent(
      makeScenario({ passed: false, testType: 'ui_e2e', screenshotUris: [uri] }),
    );
    const img = screen.getByAltText('Test screenshot');
    expect(img).toBeInTheDocument();
    expect(img).toHaveAttribute('loading', 'lazy');
    expect(img).toHaveAttribute('src', uri);

    const anchor = img.closest('a');
    expect(anchor).not.toBeNull();
    expect(anchor).toHaveAttribute('href', uri);
    expect(anchor).toHaveAttribute('target', '_blank');
  });

  it('renders duration', () => {
    renderComponent(makeScenario({ durationMs: 320 }));
    expect(screen.getByText('0.32 s')).toBeInTheDocument();
  });

  // ─── Expand / collapse (ui_e2e scenarios) ─────────────────────────────────

  it('api scenario — no chevron expand button present', () => {
    renderComponent(makeScenario({ testType: 'api', steps: null }));
    expect(screen.queryByLabelText('Expand step details')).not.toBeInTheDocument();
    expect(screen.queryByLabelText('Collapse step details')).not.toBeInTheDocument();
  });

  it('api scenario — no UiStepList rendered', () => {
    renderComponent(
      makeScenario({
        testType: 'api',
        steps: null,
      }),
    );
    // Step action labels from UiStepList must not appear
    expect(screen.queryByText('navigate')).not.toBeInTheDocument();
  });

  it('ui_e2e scenario — chevron present with aria-label "Expand step details"', () => {
    renderComponent(
      makeScenario({
        testType: 'ui_e2e',
        steps: [
          { stepIndex: 1, action: 'navigate', passed: true, errorMessage: null, screenshotBlobUri: null },
        ],
      }),
    );
    expect(screen.getByLabelText('Expand step details')).toBeInTheDocument();
  });

  it('ui_e2e scenario — UiStepList is hidden initially before clicking chevron', () => {
    renderComponent(
      makeScenario({
        testType: 'ui_e2e',
        steps: [
          { stepIndex: 1, action: 'navigate', passed: true, errorMessage: null, screenshotBlobUri: null },
        ],
      }),
    );
    // Step action text is not visible until expanded
    expect(screen.queryByText('navigate')).not.toBeInTheDocument();
  });

  it('ui_e2e scenario — UiStepList visible after clicking the chevron button', async () => {
    const user = userEvent.setup();
    renderComponent(
      makeScenario({
        testType: 'ui_e2e',
        steps: [
          { stepIndex: 1, action: 'navigate', passed: true, errorMessage: null, screenshotBlobUri: null },
          { stepIndex: 2, action: 'click', passed: true, errorMessage: null, screenshotBlobUri: null },
        ],
      }),
    );

    const expandBtn = screen.getByLabelText('Expand step details');
    await user.click(expandBtn);

    expect(screen.getByText('navigate')).toBeInTheDocument();
    expect(screen.getByText('click')).toBeInTheDocument();
  });

  it('re-closing collapses the UiStepList again', async () => {
    const user = userEvent.setup();
    renderComponent(
      makeScenario({
        testType: 'ui_e2e',
        steps: [
          { stepIndex: 1, action: 'fill', passed: true, errorMessage: null, screenshotBlobUri: null },
        ],
      }),
    );

    // Expand
    await user.click(screen.getByLabelText('Expand step details'));
    expect(screen.getByText('fill')).toBeInTheDocument();

    // Collapse — MUI Collapse with unmountOnExit removes children after animation
    await user.click(screen.getByLabelText('Collapse step details'));
    await waitFor(() => {
      expect(screen.queryByText('fill')).not.toBeInTheDocument();
    });
  });
});
