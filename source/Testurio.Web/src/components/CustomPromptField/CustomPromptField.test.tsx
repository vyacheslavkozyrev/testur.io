import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { I18nextProvider } from 'react-i18next';
import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import { ThemeProvider } from '@mui/material/styles';
import { theme } from '@/theme/theme';
import CustomPromptField from './CustomPromptField';
import type { PromptCheckFeedback } from '@/types/project.types';

// ─── Mock the usePromptCheck hook ─────────────────────────────────────────────

const mockMutate = jest.fn();
const mockPromptCheckState = {
  mutate: mockMutate,
  isPending: false,
  isError: false,
  data: undefined as PromptCheckFeedback | undefined,
};

jest.mock('@/hooks/useProject', () => ({
  usePromptCheck: () => mockPromptCheckState,
}));

// ─── i18n setup ───────────────────────────────────────────────────────────────

const i18nInstance = i18n.createInstance();
i18nInstance.use(initReactI18next).init({
  lng: 'en',
  resources: {
    en: {
      project: {
        'customPrompt.field.label': 'Custom Prompt',
        'customPrompt.field.placeholder': 'E.g. Focus on edge cases...',
        'customPrompt.field.overLimit': 'Custom prompt must not exceed 5,000 characters.',
        'customPrompt.field.conflictWarning': 'Your custom prompt may conflict with the selected testing strategy.',
        'customPrompt.check.button': 'Check Prompt',
        'customPrompt.check.error': 'Prompt quality check failed. Please try again.',
        'customPrompt.check.resultsTitle': 'AI Prompt Quality Feedback',
        'customPrompt.check.clarity': 'Clarity',
        'customPrompt.check.specificity': 'Specificity',
        'customPrompt.check.potentialConflicts': 'Potential Conflicts',
        'customPrompt.preview.title': 'Prompt Preview',
        'customPrompt.preview.readOnly': 'Read-only',
        'customPrompt.preview.basePrompt': '[Base System Prompt]',
        'customPrompt.preview.strategyLabel': '[Testing Strategy]',
        'customPrompt.preview.customLabel': '[Your Custom Prompt]',
        'customPrompt.preview.noCustomPrompt': '[No custom prompt added]',
      },
    },
  },
});

function Wrapper({ children }: { children: React.ReactNode }) {
  return (
    <ThemeProvider theme={theme}>
      <I18nextProvider i18n={i18nInstance}>{children}</I18nextProvider>
    </ThemeProvider>
  );
}

function renderField(overrides?: {
  value?: string;
  onChange?: (v: string) => void;
}) {
  const onChange = overrides?.onChange ?? jest.fn();
  render(
    <Wrapper>
      <CustomPromptField
        projectId="proj-1"
        testingStrategy="Focus on API contracts."
        value={overrides?.value ?? ''}
        onChange={onChange}
      />
    </Wrapper>,
  );
  return { onChange };
}

beforeEach(() => {
  jest.clearAllMocks();
  mockPromptCheckState.isPending = false;
  mockPromptCheckState.isError = false;
  mockPromptCheckState.data = undefined;
});

describe('CustomPromptField', () => {
  // ─── Rendering ──────────────────────────────────────────────────────────────

  it('renders the textarea with the correct label', () => {
    renderField();
    expect(screen.getByLabelText('Custom Prompt')).toBeInTheDocument();
  });

  it('renders character counter showing 0 / 5,000 when empty', () => {
    renderField({ value: '' });
    expect(screen.getByText('0 / 5000')).toBeInTheDocument();
  });

  it('renders character counter with current length', () => {
    renderField({ value: 'Hello world' });
    expect(screen.getByText('11 / 5000')).toBeInTheDocument();
  });

  it('renders the Prompt Preview panel', () => {
    renderField();
    expect(screen.getByText('Prompt Preview')).toBeInTheDocument();
  });

  it('renders the "Check Prompt" button', () => {
    renderField();
    expect(screen.getByRole('button', { name: /check prompt/i })).toBeInTheDocument();
  });

  // ─── Character limit (AC-003, AC-004) ───────────────────────────────────────

  it('shows over-limit error message when value exceeds 5000 characters', () => {
    const longValue = 'A'.repeat(5001);
    renderField({ value: longValue });
    expect(screen.getByText('Custom prompt must not exceed 5,000 characters.')).toBeInTheDocument();
  });

  it('does not show over-limit error when value is exactly 5000 characters', () => {
    const exactValue = 'A'.repeat(5000);
    renderField({ value: exactValue });
    expect(screen.queryByText('Custom prompt must not exceed 5,000 characters.')).not.toBeInTheDocument();
  });

  // ─── Conflict warning (AC-016, AC-017, AC-018, AC-019) ──────────────────────

  it('shows conflict warning when prompt contains restrictive language', () => {
    renderField({ value: 'Never test authentication.' });
    expect(
      screen.getByText('Your custom prompt may conflict with the selected testing strategy.'),
    ).toBeInTheDocument();
  });

  it('does not show conflict warning when prompt has no conflicting terms', () => {
    renderField({ value: 'Always include edge case scenarios for authentication.' });
    expect(
      screen.queryByText('Your custom prompt may conflict with the selected testing strategy.'),
    ).not.toBeInTheDocument();
  });

  it('does not show conflict warning when the field is empty', () => {
    renderField({ value: '' });
    expect(
      screen.queryByText('Your custom prompt may conflict with the selected testing strategy.'),
    ).not.toBeInTheDocument();
  });

  // ─── Prompt preview (AC-011, AC-012, AC-013, AC-015) ────────────────────────

  it('shows no-custom-prompt indicator in preview when field is empty', () => {
    renderField({ value: '' });
    expect(screen.getByText(/No custom prompt added/)).toBeInTheDocument();
  });

  it('shows custom prompt text in preview when value is non-empty', () => {
    renderField({ value: 'Focus on auth flows.' });
    // The text appears in both the textarea and the preview panel; verify at least one element contains it.
    expect(screen.getAllByText(/Focus on auth flows./)[0]).toBeInTheDocument();
    expect(screen.queryByText(/No custom prompt added/)).not.toBeInTheDocument();
  });

  // ─── Check Prompt button (AC-021, AC-025) ────────────────────────────────────

  it('disables Check Prompt button when field is empty', () => {
    renderField({ value: '' });
    expect(screen.getByRole('button', { name: /check prompt/i })).toBeDisabled();
  });

  it('enables Check Prompt button when field has content', () => {
    renderField({ value: 'Test with expired tokens.' });
    expect(screen.getByRole('button', { name: /check prompt/i })).not.toBeDisabled();
  });

  it('calls promptCheck.mutate with the current value when Check Prompt is clicked', async () => {
    const user = userEvent.setup();
    renderField({ value: 'Test with expired tokens.' });

    await user.click(screen.getByRole('button', { name: /check prompt/i }));

    expect(mockMutate).toHaveBeenCalledWith(
      { customPrompt: 'Test with expired tokens.' },
      expect.any(Object),
    );
  });

  it('disables Check Prompt button and shows loading indicator while pending', () => {
    mockPromptCheckState.isPending = true;
    renderField({ value: 'Some prompt.' });

    const button = screen.getByRole('button', { name: /check prompt/i });
    expect(button).toBeDisabled();
  });

  // ─── Error state (AC-026) ────────────────────────────────────────────────────

  it('shows error message when prompt check fails', () => {
    mockPromptCheckState.isError = true;
    renderField({ value: 'Some prompt.' });
    expect(screen.getByText('Prompt quality check failed. Please try again.')).toBeInTheDocument();
  });

  // ─── onChange propagation ────────────────────────────────────────────────────

  it('calls onChange with new value when user types', async () => {
    const user = userEvent.setup();
    const onChange = jest.fn();
    render(
      <Wrapper>
        <CustomPromptField
          projectId="proj-1"
          testingStrategy="Strategy."
          value=""
          onChange={onChange}
        />
      </Wrapper>,
    );

    await user.type(screen.getByLabelText('Custom Prompt'), 'Hello');

    expect(onChange).toHaveBeenCalled();
  });
});
