import { render, screen } from '@testing-library/react';
import { I18nextProvider } from 'react-i18next';
import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import { ThemeProvider } from '@mui/material/styles';
import { theme } from '@/theme/theme';
import UiStepList from './UiStepList';
import type { StepSummary } from '@/types/history.types';

// ─── i18n setup ───────────────────────────────────────────────────────────────

const i18nInstance = i18n.createInstance();
i18nInstance.use(initReactI18next).init({
  lng: 'en',
  resources: {
    en: {
      history: {
        'uiStepList.screenshotAlt': 'Screenshot at step {{index}}',
      },
    },
  },
});

// ─── Test helpers ─────────────────────────────────────────────────────────────

function makeStep(overrides: Partial<StepSummary> = {}): StepSummary {
  return {
    stepIndex: 1,
    action: 'navigate',
    passed: true,
    errorMessage: null,
    screenshotBlobUri: null,
    ...overrides,
  };
}

function renderComponent(steps: StepSummary[]) {
  return render(
    <I18nextProvider i18n={i18nInstance}>
      <ThemeProvider theme={theme}>
        <UiStepList steps={steps} />
      </ThemeProvider>
    </I18nextProvider>,
  );
}

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('UiStepList', () => {
  it('renders each step row with action label', () => {
    const steps = [
      makeStep({ stepIndex: 1, action: 'navigate', passed: true }),
      makeStep({ stepIndex: 2, action: 'click', passed: true }),
      makeStep({ stepIndex: 3, action: 'assert_visible', passed: true }),
    ];
    renderComponent(steps);

    expect(screen.getByText('navigate')).toBeInTheDocument();
    expect(screen.getByText('click')).toBeInTheDocument();
    expect(screen.getByText('assert_visible')).toBeInTheDocument();
  });

  it('passed step shows success icon and no error message', () => {
    const steps = [makeStep({ stepIndex: 1, action: 'click', passed: true, errorMessage: null })];
    renderComponent(steps);

    // No error text rendered for a passed step
    expect(screen.queryByText(/error/i)).not.toBeInTheDocument();
    // No screenshot thumbnail for passed step
    expect(screen.queryByRole('img')).not.toBeInTheDocument();
  });

  it('failed step shows error message inline', () => {
    const steps = [
      makeStep({
        stepIndex: 1,
        action: 'assert_text',
        passed: false,
        errorMessage: 'Expected "Welcome" but found "Error"',
        screenshotBlobUri: null,
      }),
    ];
    renderComponent(steps);

    expect(screen.getByText('Expected "Welcome" but found "Error"')).toBeInTheDocument();
  });

  it('failed assertion step with screenshotBlobUri shows thumbnail and anchor with target="_blank"', () => {
    const blobUri = 'https://blob.example.com/screenshots/step-2.png';
    const steps = [
      makeStep({
        stepIndex: 2,
        action: 'assert_visible',
        passed: false,
        errorMessage: 'Element not found',
        screenshotBlobUri: blobUri,
      }),
    ];
    renderComponent(steps);

    const img = screen.getByAltText('Screenshot at step 2');
    expect(img).toBeInTheDocument();
    expect(img).toHaveAttribute('src', blobUri);
    expect(img).toHaveAttribute('loading', 'lazy');

    const anchor = img.closest('a');
    expect(anchor).not.toBeNull();
    expect(anchor).toHaveAttribute('href', blobUri);
    expect(anchor).toHaveAttribute('target', '_blank');
  });

  it('passed step shows no screenshot even when screenshotBlobUri is set', () => {
    const steps = [
      makeStep({
        stepIndex: 1,
        action: 'assert_text',
        passed: true,
        errorMessage: null,
        screenshotBlobUri: 'https://blob.example.com/screenshots/step-1.png',
      }),
    ];
    renderComponent(steps);

    expect(screen.queryByRole('img')).not.toBeInTheDocument();
  });

  it('non-assertion failed step with screenshotBlobUri shows no screenshot', () => {
    // Only assertion actions (assert_visible, assert_text, assert_url) show screenshots
    const steps = [
      makeStep({
        stepIndex: 1,
        action: 'click',
        passed: false,
        errorMessage: 'Click failed',
        screenshotBlobUri: 'https://blob.example.com/screenshots/step-1.png',
      }),
    ];
    renderComponent(steps);

    // Error message is shown but no screenshot thumbnail for non-assertion steps
    expect(screen.getByText('Click failed')).toBeInTheDocument();
    expect(screen.queryByRole('img')).not.toBeInTheDocument();
  });

  it('skipped step renders with muted action style (actionMuted) and muted error style', () => {
    const steps = [
      makeStep({
        stepIndex: 3,
        action: 'fill',
        passed: false,
        errorMessage: 'Skipped — previous step failed',
        screenshotBlobUri: null,
      }),
    ];
    const { container } = renderComponent(steps);

    // Action label is rendered as a code element
    const codeEl = container.querySelector('code');
    expect(codeEl).not.toBeNull();
    expect(codeEl?.textContent).toBe('fill');

    // The error message for a skipped step is still visible
    expect(screen.getByText('Skipped — previous step failed')).toBeInTheDocument();

    // No screenshot thumbnail
    expect(screen.queryByRole('img')).not.toBeInTheDocument();
  });

  it('renders multiple steps in order with correct step indices', () => {
    const steps = [
      makeStep({ stepIndex: 1, action: 'navigate', passed: true }),
      makeStep({ stepIndex: 2, action: 'click', passed: false, errorMessage: 'Click timed out' }),
      makeStep({ stepIndex: 3, action: 'assert_url', passed: false, errorMessage: 'Skipped — previous step failed' }),
    ];
    renderComponent(steps);

    // All three action labels are rendered
    expect(screen.getByText('navigate')).toBeInTheDocument();
    expect(screen.getByText('click')).toBeInTheDocument();
    expect(screen.getByText('assert_url')).toBeInTheDocument();

    // Only step 2 shows a non-skipped error
    expect(screen.getByText('Click timed out')).toBeInTheDocument();
    // Step 3 shows skipped error
    expect(screen.getByText('Skipped — previous step failed')).toBeInTheDocument();
  });
});
