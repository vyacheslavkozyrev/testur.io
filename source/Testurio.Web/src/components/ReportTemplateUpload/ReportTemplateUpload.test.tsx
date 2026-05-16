import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { I18nextProvider } from 'react-i18next';
import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import { ThemeProvider } from '@mui/material/styles';
import { theme } from '@/theme/theme';
import ReportTemplateUpload from './ReportTemplateUpload';
import type { ReportTemplateUploadResponse } from '@/types/reportSettings.types';

// ─── Mock hooks ───────────────────────────────────────────────────────────────

const mockUploadMutateAsync = jest.fn();
const mockRemoveMutateAsync = jest.fn();

const mockUploadState = {
  mutateAsync: mockUploadMutateAsync,
  isPending: false,
  isError: false,
};

const mockRemoveState = {
  mutateAsync: mockRemoveMutateAsync,
  isPending: false,
  isError: false,
};

jest.mock('@/hooks/useReportSettings', () => ({
  useUploadReportTemplate: () => mockUploadState,
  useRemoveReportTemplate: () => mockRemoveState,
}));

// ─── i18n setup ───────────────────────────────────────────────────────────────

const i18nInstance = i18n.createInstance();
i18nInstance.use(initReactI18next).init({
  lng: 'en',
  resources: {
    en: {
      reportSettings: {
        'template.title': 'Report Template',
        'template.noTemplate': 'No custom template — the built-in default will be used.',
        'template.upload': 'Upload Template',
        'template.replace': 'Replace',
        'template.remove': 'Remove',
        'template.removeConfirmTitle': 'Remove Report Template',
        'template.removeConfirmBody':
          'Are you sure you want to remove the report template?',
        'template.removeConfirm': 'Remove',
        'template.cancel': 'Cancel',
        'template.uploadFailed': 'Template upload failed. Please try again.',
        'template.removeFailed': 'Failed to delete the template file.',
        'template.errorExtension': 'Only Markdown (.md) files are accepted.',
        'template.errorSize': 'Template file must be 100 KB or smaller.',
        'template.unknownToken': 'Unknown token {{token}} — it will appear as-is in the report.',
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

function renderComponent(currentFileName: string | null = null) {
  render(
    <Wrapper>
      <ReportTemplateUpload projectId="proj-001" currentFileName={currentFileName} />
    </Wrapper>,
  );
}

beforeEach(() => {
  jest.clearAllMocks();
  mockUploadState.isPending = false;
  mockUploadState.isError = false;
  mockRemoveState.isPending = false;
  mockRemoveState.isError = false;
});

describe('ReportTemplateUpload', () => {
  // ─── Rendering ───────────────────────────────────────────────────────────────

  it('renders section title', () => {
    renderComponent();
    expect(screen.getByText('Report Template')).toBeInTheDocument();
  });

  it('renders no-template message when no file is set', () => {
    renderComponent(null);
    expect(
      screen.getByText('No custom template — the built-in default will be used.'),
    ).toBeInTheDocument();
  });

  it('renders Upload Template button when no file is set', () => {
    renderComponent(null);
    expect(
      screen.getByRole('button', { name: /upload template/i }),
    ).toBeInTheDocument();
  });

  it('renders the existing file name when a template is set', () => {
    renderComponent('custom-report.md');
    expect(screen.getByText('custom-report.md')).toBeInTheDocument();
  });

  it('renders Replace and Remove buttons when a template is set', () => {
    renderComponent('custom-report.md');
    expect(screen.getByRole('button', { name: /replace/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /remove/i })).toBeInTheDocument();
  });

  it('does not render no-template message when a file is set', () => {
    renderComponent('custom-report.md');
    expect(
      screen.queryByText('No custom template — the built-in default will be used.'),
    ).not.toBeInTheDocument();
  });

  // ─── File validation: extension (AC-002) ─────────────────────────────────────

  it('shows extension error when a non-.md file is selected', async () => {
    renderComponent(null);

    const input = document.querySelector('input[type="file"]') as HTMLInputElement;
    const file = new File(['content'], 'report.txt', { type: 'text/plain' });

    Object.defineProperty(input, 'files', {
      value: [file],
      configurable: true,
    });
    fireEvent.change(input);

    await waitFor(() => {
      expect(
        screen.getByText('Only Markdown (.md) files are accepted.'),
      ).toBeInTheDocument();
    });
    expect(mockUploadMutateAsync).not.toHaveBeenCalled();
  });

  // ─── File validation: size (AC-003) ──────────────────────────────────────────

  it('shows size error when file exceeds 100 KB', async () => {
    renderComponent(null);

    const input = document.querySelector('input[type="file"]') as HTMLInputElement;
    const bigContent = 'A'.repeat(100 * 1024 + 1);
    const file = new File([bigContent], 'big-template.md', { type: 'text/markdown' });
    await userEvent.upload(input, file);

    expect(
      screen.getByText('Template file must be 100 KB or smaller.'),
    ).toBeInTheDocument();
    expect(mockUploadMutateAsync).not.toHaveBeenCalled();
  });

  it('does not show size error when file is exactly 100 KB', async () => {
    renderComponent(null);

    const input = document.querySelector('input[type="file"]') as HTMLInputElement;
    const exactContent = 'A'.repeat(100 * 1024);
    const file = new File([exactContent], 'ok-template.md', { type: 'text/markdown' });

    mockUploadMutateAsync.mockResolvedValueOnce({
      blobUri: 'https://blob.example.com/tmpl.md',
      warnings: [],
    } satisfies ReportTemplateUploadResponse);

    await userEvent.upload(input, file);

    expect(
      screen.queryByText('Template file must be 100 KB or smaller.'),
    ).not.toBeInTheDocument();
  });

  // ─── Successful upload ────────────────────────────────────────────────────────

  it('calls uploadMutateAsync with the selected file when valid', async () => {
    renderComponent(null);

    const input = document.querySelector('input[type="file"]') as HTMLInputElement;
    const file = new File(['# My template\n{{overall_result}}'], 'template.md', {
      type: 'text/markdown',
    });

    mockUploadMutateAsync.mockResolvedValueOnce({
      blobUri: 'https://blob.example.com/tmpl.md',
      warnings: [],
    } satisfies ReportTemplateUploadResponse);

    await userEvent.upload(input, file);

    expect(mockUploadMutateAsync).toHaveBeenCalledWith(file);
  });

  // ─── Token warnings (AC-038) ─────────────────────────────────────────────────

  it('shows token warnings returned from the server', async () => {
    renderComponent(null);

    const input = document.querySelector('input[type="file"]') as HTMLInputElement;
    const file = new File(['# {{bad_token}}'], 'template.md', { type: 'text/markdown' });

    mockUploadMutateAsync.mockResolvedValueOnce({
      blobUri: 'https://blob.example.com/tmpl.md',
      warnings: ['{{bad_token}}'],
    } satisfies ReportTemplateUploadResponse);

    await userEvent.upload(input, file);

    await waitFor(() => {
      expect(
        screen.getByText(/Unknown token {{bad_token}}/),
      ).toBeInTheDocument();
    });
  });

  it('does not show warning messages when no warnings are returned', async () => {
    renderComponent(null);

    const input = document.querySelector('input[type="file"]') as HTMLInputElement;
    const file = new File(['# {{overall_result}}'], 'template.md', {
      type: 'text/markdown',
    });

    mockUploadMutateAsync.mockResolvedValueOnce({
      blobUri: 'https://blob.example.com/tmpl.md',
      warnings: [],
    } satisfies ReportTemplateUploadResponse);

    await userEvent.upload(input, file);

    expect(screen.queryByText(/Unknown token/)).not.toBeInTheDocument();
  });

  // ─── Upload error state ───────────────────────────────────────────────────────

  it('shows upload error alert when upload mutation is in error state', () => {
    mockUploadState.isError = true;
    renderComponent(null);

    expect(
      screen.getByText('Template upload failed. Please try again.'),
    ).toBeInTheDocument();
  });

  // ─── Remove confirmation dialog (AC-008) ─────────────────────────────────────

  it('opens remove confirmation dialog when Remove button is clicked', async () => {
    const user = userEvent.setup();
    renderComponent('custom-report.md');

    await user.click(screen.getByRole('button', { name: /remove/i }));

    expect(screen.getByText('Remove Report Template')).toBeInTheDocument();
    expect(
      screen.getByText('Are you sure you want to remove the report template?'),
    ).toBeInTheDocument();
  });

  it('closes dialog without removing when Cancel is clicked', async () => {
    const user = userEvent.setup();
    renderComponent('custom-report.md');

    await user.click(screen.getByRole('button', { name: /^remove$/i }));
    await user.click(screen.getByRole('button', { name: /cancel/i }));

    await waitFor(() => {
      expect(screen.queryByText('Remove Report Template')).not.toBeInTheDocument();
    });
    expect(mockRemoveMutateAsync).not.toHaveBeenCalled();
  });

  it('calls removeMutateAsync when confirm Remove is clicked in dialog', async () => {
    const user = userEvent.setup();
    mockRemoveMutateAsync.mockResolvedValueOnce(undefined);
    renderComponent('custom-report.md');

    // Open dialog
    await user.click(screen.getByRole('button', { name: /^remove$/i }));
    // Confirm in dialog — multiple Remove buttons exist; target the one inside dialog actions
    const dialogRemoveButton = screen.getAllByRole('button', { name: /^remove$/i }).at(-1)!;
    await user.click(dialogRemoveButton);

    expect(mockRemoveMutateAsync).toHaveBeenCalledTimes(1);
  });

  // ─── Remove error state ───────────────────────────────────────────────────────

  it('shows remove error alert when remove mutation is in error state', () => {
    mockRemoveState.isError = true;
    renderComponent('custom-report.md');

    expect(
      screen.getByText('Failed to delete the template file.'),
    ).toBeInTheDocument();
  });

  // ─── Disabled states during pending ──────────────────────────────────────────

  it('disables Upload button while upload is pending', () => {
    mockUploadState.isPending = true;
    renderComponent(null);

    expect(screen.getByRole('button', { name: /upload template/i })).toBeDisabled();
  });

  it('disables Remove button while remove is pending', () => {
    mockRemoveState.isPending = true;
    renderComponent('custom-report.md');

    expect(screen.getByRole('button', { name: /^remove$/i })).toBeDisabled();
  });
});
