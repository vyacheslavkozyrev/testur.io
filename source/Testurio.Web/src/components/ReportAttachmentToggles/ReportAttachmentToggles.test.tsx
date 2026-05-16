import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { I18nextProvider } from 'react-i18next';
import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import { ThemeProvider } from '@mui/material/styles';
import { theme } from '@/theme/theme';
import ReportAttachmentToggles, {
  type ReportAttachmentTogglesProps,
} from './ReportAttachmentToggles';

// ─── i18n setup ───────────────────────────────────────────────────────────────

const i18nInstance = i18n.createInstance();
i18nInstance.use(initReactI18next).init({
  lng: 'en',
  resources: {
    en: {
      reportSettings: {
        'attachments.title': 'Report Attachments',
        'attachments.includeLogs': 'Include step-by-step logs',
        'attachments.includeScreenshots': 'Include screenshots',
        'attachments.screenshotsDisabledTooltip':
          'Screenshots are only available for UI E2E tests.',
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

function renderToggles(
  props: Partial<ReportAttachmentTogglesProps> & { onChange?: jest.Mock } = {},
) {
  const onChange = props.onChange ?? jest.fn();
  render(
    <Wrapper>
      <ReportAttachmentToggles
        testType={props.testType ?? 'ui_e2e'}
        includeLogs={props.includeLogs ?? false}
        includeScreenshots={props.includeScreenshots ?? false}
        onChange={onChange}
      />
    </Wrapper>,
  );
  return { onChange };
}

describe('ReportAttachmentToggles', () => {
  // ─── Rendering ───────────────────────────────────────────────────────────────

  it('renders section title', () => {
    renderToggles();
    expect(screen.getByText('Report Attachments')).toBeInTheDocument();
  });

  it('renders Include step-by-step logs toggle', () => {
    renderToggles();
    expect(
      screen.getByRole('checkbox', { name: /include step-by-step logs/i }),
    ).toBeInTheDocument();
  });

  it('renders Include screenshots toggle', () => {
    renderToggles();
    expect(
      screen.getByRole('checkbox', { name: /include screenshots/i }),
    ).toBeInTheDocument();
  });

  // ─── Checked state ───────────────────────────────────────────────────────────

  it('reflects includeLogs=true in the logs toggle', () => {
    renderToggles({ includeLogs: true });
    expect(
      screen.getByRole('checkbox', { name: /include step-by-step logs/i }),
    ).toBeChecked();
  });

  it('reflects includeLogs=false in the logs toggle', () => {
    renderToggles({ includeLogs: false });
    expect(
      screen.getByRole('checkbox', { name: /include step-by-step logs/i }),
    ).not.toBeChecked();
  });

  it('reflects includeScreenshots=true for ui_e2e project', () => {
    renderToggles({ testType: 'ui_e2e', includeScreenshots: true });
    expect(
      screen.getByRole('checkbox', { name: /include screenshots/i }),
    ).toBeChecked();
  });

  // ─── onChange propagation ────────────────────────────────────────────────────

  it('calls onChange with includeLogs toggled on when logs switch is clicked', async () => {
    const user = userEvent.setup();
    const onChange = jest.fn();
    renderToggles({ includeLogs: false, includeScreenshots: false, onChange });

    await user.click(
      screen.getByRole('checkbox', { name: /include step-by-step logs/i }),
    );

    expect(onChange).toHaveBeenCalledWith({
      includeLogs: true,
      includeScreenshots: false,
    });
  });

  it('calls onChange with includeLogs toggled off when logs switch is clicked again', async () => {
    const user = userEvent.setup();
    const onChange = jest.fn();
    renderToggles({ includeLogs: true, includeScreenshots: false, onChange });

    await user.click(
      screen.getByRole('checkbox', { name: /include step-by-step logs/i }),
    );

    expect(onChange).toHaveBeenCalledWith({
      includeLogs: false,
      includeScreenshots: false,
    });
  });

  it('calls onChange with includeScreenshots toggled on for ui_e2e project', async () => {
    const user = userEvent.setup();
    const onChange = jest.fn();
    renderToggles({
      testType: 'ui_e2e',
      includeLogs: false,
      includeScreenshots: false,
      onChange,
    });

    await user.click(
      screen.getByRole('checkbox', { name: /include screenshots/i }),
    );

    expect(onChange).toHaveBeenCalledWith({
      includeLogs: false,
      includeScreenshots: true,
    });
  });

  // ─── API-only test_type coercion (AC-023, AC-024) ────────────────────────────

  it('disables the screenshots toggle when testType is "api"', () => {
    renderToggles({ testType: 'api', includeScreenshots: false });
    expect(
      screen.getByRole('checkbox', { name: /include screenshots/i }),
    ).toBeDisabled();
  });

  it('shows screenshots toggle as unchecked when testType is "api" regardless of prop', () => {
    // Component coerces to false when api-only
    renderToggles({ testType: 'api', includeScreenshots: true });
    expect(
      screen.getByRole('checkbox', { name: /include screenshots/i }),
    ).not.toBeChecked();
  });

  it('enables screenshots toggle when testType is "ui_e2e"', () => {
    renderToggles({ testType: 'ui_e2e', includeScreenshots: false });
    expect(
      screen.getByRole('checkbox', { name: /include screenshots/i }),
    ).not.toBeDisabled();
  });

  it('enables screenshots toggle when testType is "both"', () => {
    renderToggles({ testType: 'both', includeScreenshots: false });
    expect(
      screen.getByRole('checkbox', { name: /include screenshots/i }),
    ).not.toBeDisabled();
  });

  it('coerces includeScreenshots to false and calls onChange when testType switches to api', () => {
    const onChange = jest.fn();
    const { rerender } = render(
      <Wrapper>
        <ReportAttachmentToggles
          testType="ui_e2e"
          includeLogs={false}
          includeScreenshots={true}
          onChange={onChange}
        />
      </Wrapper>,
    );

    onChange.mockClear();

    // Switch to api — should trigger coercion via useEffect
    rerender(
      <Wrapper>
        <ReportAttachmentToggles
          testType="api"
          includeLogs={false}
          includeScreenshots={true}
          onChange={onChange}
        />
      </Wrapper>,
    );

    expect(onChange).toHaveBeenCalledWith({
      includeLogs: false,
      includeScreenshots: false,
    });
  });

  it('does not call onChange on mount when includeScreenshots is already false for api project', () => {
    const onChange = jest.fn();
    renderToggles({ testType: 'api', includeScreenshots: false, onChange });
    expect(onChange).not.toHaveBeenCalled();
  });

  // ─── Logs toggle is always enabled regardless of testType ────────────────────

  it('does not disable the logs toggle for api-only projects', () => {
    renderToggles({ testType: 'api' });
    expect(
      screen.getByRole('checkbox', { name: /include step-by-step logs/i }),
    ).not.toBeDisabled();
  });
});
