import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { I18nextProvider } from 'react-i18next';
import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import { ThemeProvider, createTheme } from '@mui/material/styles';
import WebhookSetupPanel from './WebhookSetupPanel';
import type { WebhookSetupInfo } from '@/types/pmTool.types';

const i18nInstance = i18n.createInstance();
i18nInstance.use(initReactI18next).init({
  lng: 'en',
  resources: {
    en: {
      pmTool: {
        'webhookSetup.title': 'Webhook Setup',
        'webhookSetup.urlLabel': 'Webhook URL',
        'webhookSetup.secretLabel': 'Webhook Secret',
        'webhookSetup.copyUrl': 'Copy webhook URL',
        'webhookSetup.copySecret': 'Copy webhook secret',
        'webhookSetup.copied': 'Copied!',
        'webhookSetup.secretMaskedInfo':
          'The webhook secret is masked after the first view. Use "Regenerate" to create a new one.',
        'webhookSetup.regenerate': 'Regenerate Secret',
        'webhookSetup.instructionsAdo': 'ADO instructions here.',
        'webhookSetup.instructionsJira': 'Jira instructions here.',
        'webhookSetup.regenerateDialog.title': 'Regenerate Webhook Secret',
        'webhookSetup.regenerateDialog.message': 'This will invalidate the current secret.',
        'webhookSetup.regenerateDialog.confirm': 'Regenerate',
        'common.cancel': 'Cancel',
      },
    },
  },
});

const theme = createTheme();

function Wrapper({ children }: { children: React.ReactNode }) {
  return (
    <ThemeProvider theme={theme}>
      <I18nextProvider i18n={i18nInstance}>{children}</I18nextProvider>
    </ThemeProvider>
  );
}

const plainSetup: WebhookSetupInfo = {
  webhookUrl: 'https://api.testur.io/webhooks/ado',
  webhookSecret: 'abc123secret',
  isMasked: false,
};

const maskedSetup: WebhookSetupInfo = {
  webhookUrl: 'https://api.testur.io/webhooks/ado',
  webhookSecret: '••••••••',
  isMasked: true,
};

describe('WebhookSetupPanel', () => {
  it('renders the webhook URL', () => {
    render(
      <Wrapper>
        <WebhookSetupPanel
          pmTool="ado"
          setup={plainSetup}
          isRegenerating={false}
          onRegenerate={jest.fn()}
        />
      </Wrapper>,
    );
    expect(screen.getByDisplayValue('https://api.testur.io/webhooks/ado')).toBeInTheDocument();
  });

  it('renders the plaintext secret when not masked', () => {
    render(
      <Wrapper>
        <WebhookSetupPanel
          pmTool="ado"
          setup={plainSetup}
          isRegenerating={false}
          onRegenerate={jest.fn()}
        />
      </Wrapper>,
    );
    expect(screen.getByDisplayValue('abc123secret')).toBeInTheDocument();
  });

  it('renders the masked secret when isMasked is true', () => {
    render(
      <Wrapper>
        <WebhookSetupPanel
          pmTool="ado"
          setup={maskedSetup}
          isRegenerating={false}
          onRegenerate={jest.fn()}
        />
      </Wrapper>,
    );
    // The masked value field is a password input — check it's present
    expect(screen.getByDisplayValue('••••••••')).toBeInTheDocument();
    expect(
      screen.getByText('The webhook secret is masked after the first view. Use "Regenerate" to create a new one.'),
    ).toBeInTheDocument();
  });

  it('shows copy button for URL', () => {
    render(
      <Wrapper>
        <WebhookSetupPanel
          pmTool="ado"
          setup={plainSetup}
          isRegenerating={false}
          onRegenerate={jest.fn()}
        />
      </Wrapper>,
    );
    expect(screen.getByRole('button', { name: /copy webhook url/i })).toBeInTheDocument();
  });

  it('shows regenerate button', () => {
    render(
      <Wrapper>
        <WebhookSetupPanel
          pmTool="ado"
          setup={plainSetup}
          isRegenerating={false}
          onRegenerate={jest.fn()}
        />
      </Wrapper>,
    );
    expect(screen.getByRole('button', { name: /regenerate secret/i })).toBeInTheDocument();
  });

  it('opens confirmation dialog when regenerate is clicked', async () => {
    const user = userEvent.setup();
    render(
      <Wrapper>
        <WebhookSetupPanel
          pmTool="ado"
          setup={plainSetup}
          isRegenerating={false}
          onRegenerate={jest.fn()}
        />
      </Wrapper>,
    );

    await user.click(screen.getByRole('button', { name: /regenerate secret/i }));

    await waitFor(() => {
      expect(screen.getByText('Regenerate Webhook Secret')).toBeInTheDocument();
      expect(screen.getByText('This will invalidate the current secret.')).toBeInTheDocument();
    });
  });

  it('calls onRegenerate when dialog confirm is clicked', async () => {
    const user = userEvent.setup();
    const onRegenerate = jest.fn();
    render(
      <Wrapper>
        <WebhookSetupPanel
          pmTool="ado"
          setup={plainSetup}
          isRegenerating={false}
          onRegenerate={onRegenerate}
        />
      </Wrapper>,
    );

    await user.click(screen.getByRole('button', { name: /regenerate secret/i }));
    await waitFor(() => screen.getByText('Regenerate Webhook Secret'));
    await user.click(screen.getByRole('button', { name: /^regenerate$/i }));

    expect(onRegenerate).toHaveBeenCalledTimes(1);
  });

  it('does not call onRegenerate when dialog cancel is clicked', async () => {
    const user = userEvent.setup();
    const onRegenerate = jest.fn();
    render(
      <Wrapper>
        <WebhookSetupPanel
          pmTool="ado"
          setup={plainSetup}
          isRegenerating={false}
          onRegenerate={onRegenerate}
        />
      </Wrapper>,
    );

    await user.click(screen.getByRole('button', { name: /regenerate secret/i }));
    await waitFor(() => screen.getByText('Regenerate Webhook Secret'));
    await user.click(screen.getByRole('button', { name: /cancel/i }));

    expect(onRegenerate).not.toHaveBeenCalled();
  });
});
