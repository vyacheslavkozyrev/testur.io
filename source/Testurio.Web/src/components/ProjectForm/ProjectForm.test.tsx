import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { I18nextProvider } from 'react-i18next';
import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import { ThemeProvider, createTheme } from '@mui/material/styles';
import ProjectForm from './ProjectForm';
import type { ProjectDto } from '@/types/project.types';

// Minimal i18n setup for tests
const i18nInstance = i18n.createInstance();
i18nInstance.use(initReactI18next).init({
  lng: 'en',
  resources: {
    en: {
      project: {
        'form.titleCreate': 'Create Project',
        'form.titleEdit': 'Edit Project',
        'form.fields.name': 'Project Name',
        'form.fields.productUrl': 'Product URL',
        'form.fields.testingStrategy': 'Testing Strategy',
        'form.validation.nameRequired': 'Project name is required.',
        'form.validation.nameMaxLength': 'Project name must not exceed 200 characters.',
        'form.validation.productUrlRequired': 'Product URL is required.',
        'form.validation.productUrlInvalid': 'Product URL must be a valid URL starting with http:// or https://.',
        'form.validation.testingStrategyRequired': 'Testing strategy is required.',
        'form.validation.testingStrategyMaxLength': 'Testing strategy must not exceed 500 characters.',
        'form.actions.create': 'Create Project',
        'form.actions.save': 'Save Changes',
        'form.actions.cancel': 'Cancel',
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

const mockProject: ProjectDto = {
  projectId: 'proj-1',
  name: 'Existing Project',
  productUrl: 'https://existing.example.com',
  testingStrategy: 'Smoke tests.',
  customPrompt: null,
  createdAt: '2026-05-10T00:00:00Z',
  updatedAt: '2026-05-10T00:00:00Z',
};

describe('ProjectForm', () => {
  it('renders the create button in create mode', () => {
    const onSubmit = jest.fn();
    render(
      <Wrapper>
        <ProjectForm isSubmitting={false} onSubmit={onSubmit} />
      </Wrapper>,
    );
    expect(screen.getByRole('button', { name: /create project/i })).toBeInTheDocument();
  });

  it('pre-fills fields and hides the create button when project is provided', () => {
    const onSubmit = jest.fn();
    render(
      <Wrapper>
        <ProjectForm project={mockProject} isSubmitting={false} onSubmit={onSubmit} />
      </Wrapper>,
    );
    expect(screen.getByDisplayValue('Existing Project')).toBeInTheDocument();
    expect(screen.getByDisplayValue('https://existing.example.com')).toBeInTheDocument();
    expect(screen.getByDisplayValue('Smoke tests.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /create project/i })).not.toBeInTheDocument();
  });

  it('shows validation error when name is empty on submit', async () => {
    const onSubmit = jest.fn();
    render(
      <Wrapper>
        <ProjectForm isSubmitting={false} onSubmit={onSubmit} />
      </Wrapper>,
    );

    fireEvent.click(screen.getByRole('button', { name: /create project/i }));

    await waitFor(() => {
      expect(screen.getByText('Project name is required.')).toBeInTheDocument();
    });
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it('shows validation error when productUrl is invalid', async () => {
    const user = userEvent.setup();
    const onSubmit = jest.fn();
    render(
      <Wrapper>
        <ProjectForm isSubmitting={false} onSubmit={onSubmit} />
      </Wrapper>,
    );

    await user.type(screen.getByLabelText('Project Name *'), 'My Project');
    await user.type(screen.getByLabelText('Product URL *'), 'not-a-url');
    await user.type(screen.getByLabelText('Testing Strategy *'), 'Some strategy');
    fireEvent.click(screen.getByRole('button', { name: /create project/i }));

    await waitFor(() => {
      expect(screen.getByText('Product URL must be a valid URL starting with http:// or https://.'))
        .toBeInTheDocument();
    });
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it('calls onSubmit with form values when all fields are valid', async () => {
    const user = userEvent.setup();
    const onSubmit = jest.fn();
    render(
      <Wrapper>
        <ProjectForm isSubmitting={false} onSubmit={onSubmit} />
      </Wrapper>,
    );

    await user.type(screen.getByLabelText('Project Name *'), 'New Project');
    await user.type(screen.getByLabelText('Product URL *'), 'https://app.example.com');
    await user.type(screen.getByLabelText('Testing Strategy *'), 'API contracts only.');
    fireEvent.click(screen.getByRole('button', { name: /create project/i }));

    await waitFor(() => {
      expect(onSubmit).toHaveBeenCalledWith({
        name: 'New Project',
        productUrl: 'https://app.example.com',
        testingStrategy: 'API contracts only.',
      });
    });
  });

  it('disables submit button while isSubmitting is true', () => {
    const onSubmit = jest.fn();
    render(
      <Wrapper>
        <ProjectForm isSubmitting={true} onSubmit={onSubmit} />
      </Wrapper>,
    );
    const button = screen.getByRole('button', { name: /create project/i });
    expect(button).toBeDisabled();
  });

  it('calls onCancel when cancel button is clicked', async () => {
    const user = userEvent.setup();
    const onSubmit = jest.fn();
    const onCancel = jest.fn();
    render(
      <Wrapper>
        <ProjectForm isSubmitting={false} onSubmit={onSubmit} onCancel={onCancel} />
      </Wrapper>,
    );

    await user.click(screen.getByRole('button', { name: /cancel/i }));
    expect(onCancel).toHaveBeenCalledTimes(1);
  });
});
