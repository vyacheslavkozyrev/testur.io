import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { I18nextProvider } from 'react-i18next';
import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import { ThemeProvider, createTheme } from '@mui/material/styles';
import ADOConnectionForm from './ADOConnectionForm';

const i18nInstance = i18n.createInstance();
i18nInstance.use(initReactI18next).init({
  lng: 'en',
  resources: {
    en: {
      pmTool: {
        'ado.formTitle': 'Connect Azure DevOps',
        'ado.fields.orgUrl': 'Organization URL',
        'ado.fields.projectName': 'Project Name',
        'ado.fields.team': 'Team',
        'ado.fields.inTestingStatus': '"In Testing" Status Name',
        'ado.fields.authMethod': 'Auth Method',
        'ado.fields.pat': 'Personal Access Token',
        'ado.fields.oAuthToken': 'OAuth Token',
        'ado.authMethods.pat': 'Personal Access Token (PAT)',
        'ado.authMethods.oAuth': 'OAuth',
        'ado.validation.orgUrlRequired': 'Organization URL is required.',
        'ado.validation.orgUrlInvalid': 'Organization URL must be a valid URL starting with http:// or https://.',
        'ado.validation.projectNameRequired': 'Project Name is required.',
        'ado.validation.teamRequired': 'Team is required.',
        'ado.validation.inTestingStatusRequired': '"In Testing" status name is required.',
        'ado.validation.authMethodRequired': 'Auth method is required.',
        'ado.validation.patRequired': 'Personal Access Token is required.',
        'ado.validation.oAuthTokenRequired': 'OAuth token is required.',
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

describe('ADOConnectionForm', () => {
  it('renders all required fields', () => {
    render(
      <Wrapper>
        <ADOConnectionForm isSubmitting={false} onSubmit={jest.fn()} />
      </Wrapper>,
    );
    expect(screen.getByLabelText(/Organization URL/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/Project Name/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/Team/i)).toBeInTheDocument();
    expect(screen.getByText(/In Testing/i)).toBeInTheDocument();
    expect(screen.getByText('Auth Method')).toBeInTheDocument();
  });

  it('shows PAT field when PAT auth method is selected (default)', () => {
    render(
      <Wrapper>
        <ADOConnectionForm isSubmitting={false} onSubmit={jest.fn()} />
      </Wrapper>,
    );
    expect(screen.getByLabelText(/Personal Access Token/i)).toBeInTheDocument();
  });

  it('shows validation error when Organization URL is missing on submit', async () => {
    render(
      <Wrapper>
        <ADOConnectionForm isSubmitting={false} onSubmit={jest.fn()} />
      </Wrapper>,
    );

    fireEvent.click(screen.getByRole('button', { name: /save/i }));

    await waitFor(() => {
      expect(screen.getByText('Organization URL is required.')).toBeInTheDocument();
    });
  });

  it('shows validation error when Organization URL is invalid', async () => {
    const user = userEvent.setup();
    render(
      <Wrapper>
        <ADOConnectionForm isSubmitting={false} onSubmit={jest.fn()} />
      </Wrapper>,
    );

    await user.type(screen.getByLabelText(/Organization URL/i), 'not-a-url');
    fireEvent.click(screen.getByRole('button', { name: /save/i }));

    await waitFor(() => {
      expect(
        screen.getByText('Organization URL must be a valid URL starting with http:// or https://.'),
      ).toBeInTheDocument();
    });
  });

  it('calls onSubmit with correct values when form is valid', async () => {
    const user = userEvent.setup();
    const onSubmit = jest.fn();
    render(
      <Wrapper>
        <ADOConnectionForm isSubmitting={false} onSubmit={onSubmit} />
      </Wrapper>,
    );

    await user.type(screen.getByLabelText(/Organization URL/i), 'https://dev.azure.com/myorg');
    await user.type(screen.getByLabelText(/Project Name/i), 'My Project');
    await user.type(screen.getByLabelText(/Team/i), 'My Team');
    // Find the "In Testing" Status Name field by placeholder or label
    const inTestingFields = screen.getAllByRole('textbox');
    await user.type(inTestingFields[3], 'In Testing');
    await user.type(screen.getByLabelText(/Personal Access Token/i), 'my-pat');

    fireEvent.click(screen.getByRole('button', { name: /save/i }));

    await waitFor(() => {
      expect(onSubmit).toHaveBeenCalledWith(
        expect.objectContaining({
          orgUrl: 'https://dev.azure.com/myorg',
          projectName: 'My Project',
          team: 'My Team',
          authMethod: 'pat',
          pat: 'my-pat',
        }),
      );
    });
  });

  it('disables submit button while isSubmitting is true', () => {
    render(
      <Wrapper>
        <ADOConnectionForm isSubmitting={true} onSubmit={jest.fn()} />
      </Wrapper>,
    );
    expect(screen.getByRole('button', { name: /save/i })).toBeDisabled();
  });

  it('calls onCancel when cancel button is clicked', async () => {
    const user = userEvent.setup();
    const onCancel = jest.fn();
    render(
      <Wrapper>
        <ADOConnectionForm isSubmitting={false} onSubmit={jest.fn()} onCancel={onCancel} />
      </Wrapper>,
    );

    await user.click(screen.getByRole('button', { name: /cancel/i }));
    expect(onCancel).toHaveBeenCalledTimes(1);
  });
});
