import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { I18nextProvider } from 'react-i18next';
import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import { ThemeProvider, createTheme } from '@mui/material/styles';
import JiraConnectionForm from './JiraConnectionForm';

const i18nInstance = i18n.createInstance();
i18nInstance.use(initReactI18next).init({
  lng: 'en',
  resources: {
    en: {
      pmTool: {
        'jira.formTitle': 'Connect Jira',
        'jira.fields.baseUrl': 'Base URL',
        'jira.fields.projectKey': 'Project Key',
        'jira.fields.inTestingStatus': '"In Testing" Status Name',
        'jira.fields.authMethod': 'Auth Method',
        'jira.fields.email': 'Email Address',
        'jira.fields.apiToken': 'API Token',
        'jira.fields.pat': 'Personal Access Token',
        'jira.authMethods.apiToken': 'API Token + Email',
        'jira.authMethods.pat': 'Personal Access Token (PAT)',
        'jira.validation.baseUrlRequired': 'Base URL is required.',
        'jira.validation.baseUrlInvalid': 'Base URL must be a valid URL starting with http:// or https://.',
        'jira.validation.projectKeyRequired': 'Project Key is required.',
        'jira.validation.inTestingStatusRequired': '"In Testing" status name is required.',
        'jira.validation.authMethodRequired': 'Auth method is required.',
        'jira.validation.emailRequired': 'Email is required when API Token auth is selected.',
        'jira.validation.emailInvalid': 'Please enter a valid email address.',
        'jira.validation.apiTokenRequired': 'API Token is required.',
        'jira.validation.patRequired': 'Personal Access Token is required.',
        'common.save': 'Save',
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

describe('JiraConnectionForm', () => {
  it('renders all required fields', () => {
    render(
      <Wrapper>
        <JiraConnectionForm isSubmitting={false} onSubmit={jest.fn()} />
      </Wrapper>,
    );
    expect(screen.getByLabelText(/Base URL/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/Project Key/i)).toBeInTheDocument();
    expect(screen.getByText('Auth Method')).toBeInTheDocument();
  });

  it('shows email and api token fields when API Token auth method is selected (default)', () => {
    render(
      <Wrapper>
        <JiraConnectionForm isSubmitting={false} onSubmit={jest.fn()} />
      </Wrapper>,
    );
    expect(screen.getByLabelText(/Email Address/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/API Token/i)).toBeInTheDocument();
  });

  it('shows validation error when Base URL is missing on submit', async () => {
    render(
      <Wrapper>
        <JiraConnectionForm isSubmitting={false} onSubmit={jest.fn()} />
      </Wrapper>,
    );

    fireEvent.click(screen.getByRole('button', { name: /save/i }));

    await waitFor(() => {
      expect(screen.getByText('Base URL is required.')).toBeInTheDocument();
    });
  });

  it('shows validation error when Base URL is invalid', async () => {
    const user = userEvent.setup();
    render(
      <Wrapper>
        <JiraConnectionForm isSubmitting={false} onSubmit={jest.fn()} />
      </Wrapper>,
    );

    await user.type(screen.getByLabelText(/Base URL/i), 'not-a-url');
    fireEvent.click(screen.getByRole('button', { name: /save/i }));

    await waitFor(() => {
      expect(
        screen.getByText('Base URL must be a valid URL starting with http:// or https://.'),
      ).toBeInTheDocument();
    });
  });

  it('shows validation error when email is missing for API Token auth', async () => {
    const user = userEvent.setup();
    render(
      <Wrapper>
        <JiraConnectionForm isSubmitting={false} onSubmit={jest.fn()} />
      </Wrapper>,
    );

    await user.type(screen.getByLabelText(/Base URL/i), 'https://myorg.atlassian.net');
    await user.type(screen.getByLabelText(/Project Key/i), 'PROJ');
    // Leave email empty
    await user.type(screen.getByLabelText(/API Token/i), 'my-token');
    fireEvent.click(screen.getByRole('button', { name: /save/i }));

    await waitFor(() => {
      expect(screen.getByText('Email is required when API Token auth is selected.')).toBeInTheDocument();
    });
  });

  it('calls onSubmit with correct values when form is valid', async () => {
    const user = userEvent.setup();
    const onSubmit = jest.fn();
    render(
      <Wrapper>
        <JiraConnectionForm isSubmitting={false} onSubmit={onSubmit} />
      </Wrapper>,
    );

    await user.type(screen.getByLabelText(/Base URL/i), 'https://myorg.atlassian.net');
    await user.type(screen.getByLabelText(/Project Key/i), 'PROJ');
    const textboxes = screen.getAllByRole('textbox');
    await user.type(textboxes[2], 'In Testing'); // inTestingStatus
    await user.type(screen.getByLabelText(/Email Address/i), 'user@example.com');
    await user.type(screen.getByLabelText(/API Token/i), 'my-token');

    fireEvent.click(screen.getByRole('button', { name: /save/i }));

    await waitFor(() => {
      expect(onSubmit).toHaveBeenCalledWith(
        expect.objectContaining({
          baseUrl: 'https://myorg.atlassian.net',
          projectKey: 'PROJ',
          authMethod: 'apiToken',
          email: 'user@example.com',
          apiToken: 'my-token',
        }),
      );
    });
  });

  it('disables submit button while isSubmitting is true', () => {
    render(
      <Wrapper>
        <JiraConnectionForm isSubmitting={true} onSubmit={jest.fn()} />
      </Wrapper>,
    );
    expect(screen.getByRole('button', { name: /save/i })).toBeDisabled();
  });
});
