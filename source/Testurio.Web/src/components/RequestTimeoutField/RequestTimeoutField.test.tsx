import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { I18nextProvider } from 'react-i18next';
import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import { ThemeProvider } from '@mui/material/styles';
import { theme } from '@/theme/theme';
import RequestTimeoutField from './RequestTimeoutField';

// ─── i18n setup ───────────────────────────────────────────────────────────────

const i18nInstance = i18n.createInstance();
i18nInstance.use(initReactI18next).init({
  lng: 'en',
  resources: {
    en: {
      project: {
        'requestTimeout.field.label': 'Request Timeout (seconds)',
        'requestTimeout.field.helperText': 'Set a timeout between {{min}} and {{max}} seconds.',
        'requestTimeout.validation.required': 'Request timeout is required.',
        'requestTimeout.validation.range': 'Request timeout must be between {{min}} and {{max}} seconds.',
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
  value?: number;
  onChange?: (v: number) => void;
  error?: string | null;
}) {
  const onChange = overrides?.onChange ?? jest.fn();
  render(
    <Wrapper>
      <RequestTimeoutField
        value={overrides?.value ?? 30}
        onChange={onChange}
        error={overrides?.error}
      />
    </Wrapper>,
  );
  return { onChange };
}

describe('RequestTimeoutField', () => {
  // ─── Rendering ──────────────────────────────────────────────────────────────

  it('renders with the correct label', () => {
    renderField();
    expect(screen.getByLabelText(/request timeout/i)).toBeInTheDocument();
  });

  it('renders with the pre-filled value from props', () => {
    renderField({ value: 60 });
    const input = screen.getByLabelText(/request timeout/i) as HTMLInputElement;
    expect(input.value).toBe('60');
  });

  it('renders helper text when no validation error', () => {
    renderField({ value: 30 });
    expect(screen.getByText(/set a timeout between 5 and 120/i)).toBeInTheDocument();
  });

  // ─── Validation — out-of-range (AC-002) ──────────────────────────────────────

  it('shows validation error when value is below minimum (4)', () => {
    renderField({ value: 4 });
    expect(
      screen.getByText(/must be between 5 and 120/i),
    ).toBeInTheDocument();
  });

  it('shows validation error when value is above maximum (121)', () => {
    renderField({ value: 121 });
    expect(
      screen.getByText(/must be between 5 and 120/i),
    ).toBeInTheDocument();
  });

  it('does not show range error for the minimum value (5)', () => {
    renderField({ value: 5 });
    expect(screen.queryByText(/must be between 5 and 120/i)).not.toBeInTheDocument();
  });

  it('does not show range error for the maximum value (120)', () => {
    renderField({ value: 120 });
    expect(screen.queryByText(/must be between 5 and 120/i)).not.toBeInTheDocument();
  });

  // ─── Validation — empty / zero (AC-003) ──────────────────────────────────────

  it('shows required error when value is 0 (treated as empty)', () => {
    renderField({ value: 0 });
    expect(screen.getByText(/request timeout is required/i)).toBeInTheDocument();
  });

  // ─── External error prop ──────────────────────────────────────────────────────

  it('shows external error message when error prop is provided', () => {
    renderField({ value: 30, error: 'Server validation failed.' });
    expect(screen.getByText('Server validation failed.')).toBeInTheDocument();
  });

  // ─── onChange propagation ─────────────────────────────────────────────────────

  it('calls onChange with parsed number when user types a valid value', async () => {
    const user = userEvent.setup();
    const onChange = jest.fn();
    render(
      <Wrapper>
        <RequestTimeoutField value={30} onChange={onChange} />
      </Wrapper>,
    );

    const input = screen.getByLabelText(/request timeout/i);
    await user.clear(input);
    await user.type(input, '60');

    // Last call should be with 60
    const calls = onChange.mock.calls.map(([v]: [number]) => v);
    expect(calls[calls.length - 1]).toBe(60);
  });

  it('calls onChange with 0 when user clears the field', async () => {
    const user = userEvent.setup();
    const onChange = jest.fn();
    render(
      <Wrapper>
        <RequestTimeoutField value={30} onChange={onChange} />
      </Wrapper>,
    );

    const input = screen.getByLabelText(/request timeout/i);
    await user.clear(input);

    expect(onChange).toHaveBeenCalledWith(0);
  });

  // ─── Input attributes (AC-002) ────────────────────────────────────────────────

  it('has min=5, max=120, step=1 attributes', () => {
    renderField({ value: 30 });
    const input = screen.getByLabelText(/request timeout/i) as HTMLInputElement;
    expect(input.min).toBe('5');
    expect(input.max).toBe('120');
    expect(input.step).toBe('1');
  });

  it('is of type number', () => {
    renderField({ value: 30 });
    const input = screen.getByLabelText(/request timeout/i) as HTMLInputElement;
    expect(input.type).toBe('number');
  });

  it('is required', () => {
    renderField({ value: 30 });
    const input = screen.getByLabelText(/request timeout/i) as HTMLInputElement;
    expect(input.required).toBe(true);
  });
});
