import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { I18nextProvider } from 'react-i18next';
import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import { ThemeProvider, createTheme } from '@mui/material/styles';
import WorkItemTypeFilter from './WorkItemTypeFilter';

const i18nInstance = i18n.createInstance();
i18nInstance.use(initReactI18next).init({
  lng: 'en',
  resources: {
    en: {
      pmTool: {
        'workItemTypeFilter.title': 'Work Item Type Filter',
        'workItemTypeFilter.description': 'Only webhook events for the listed issue types will trigger a test run.',
        'workItemTypeFilter.inputLabel': 'Add issue type',
        'workItemTypeFilter.addButton': 'Add',
        'workItemTypeFilter.saveError': 'Failed to save the work item type filter. Please try again.',
        'workItemTypeFilter.validation.atLeastOne': 'At least one work item type must be selected',
        'common.save': 'Save',
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

describe('WorkItemTypeFilter', () => {
  it('renders the current types as chips', () => {
    render(
      <Wrapper>
        <WorkItemTypeFilter
          currentTypes={['Story', 'Bug']}
          isSaving={false}
          isError={false}
          onSave={jest.fn()}
        />
      </Wrapper>,
    );

    expect(screen.getByText('Story')).toBeInTheDocument();
    expect(screen.getByText('Bug')).toBeInTheDocument();
  });

  it('adds a type when the Add button is clicked', async () => {
    const user = userEvent.setup();
    render(
      <Wrapper>
        <WorkItemTypeFilter
          currentTypes={[]}
          isSaving={false}
          isError={false}
          onSave={jest.fn()}
        />
      </Wrapper>,
    );

    await user.type(screen.getByLabelText(/Add issue type/i), 'Epic');
    fireEvent.click(screen.getByRole('button', { name: /Add/i }));

    expect(screen.getByText('Epic')).toBeInTheDocument();
  });

  it('removes a type when its delete button is clicked', async () => {
    const user = userEvent.setup();
    render(
      <Wrapper>
        <WorkItemTypeFilter
          currentTypes={['Story', 'Bug']}
          isSaving={false}
          isError={false}
          onSave={jest.fn()}
        />
      </Wrapper>,
    );

    const deleteButtons = screen.getAllByTestId('CancelIcon');
    await user.click(deleteButtons[0]);

    expect(screen.queryByText('Story')).not.toBeInTheDocument();
    expect(screen.getByText('Bug')).toBeInTheDocument();
  });

  it('shows validation error and does not call onSave when list is empty', async () => {
    const onSave = jest.fn();
    render(
      <Wrapper>
        <WorkItemTypeFilter
          currentTypes={[]}
          isSaving={false}
          isError={false}
          onSave={onSave}
        />
      </Wrapper>,
    );

    fireEvent.click(screen.getByRole('button', { name: /save/i }));

    await waitFor(() => {
      expect(screen.getByText('At least one work item type must be selected')).toBeInTheDocument();
    });
    expect(onSave).not.toHaveBeenCalled();
  });

  it('calls onSave with the current type list when valid', async () => {
    const onSave = jest.fn();
    render(
      <Wrapper>
        <WorkItemTypeFilter
          currentTypes={['Story', 'Bug']}
          isSaving={false}
          isError={false}
          onSave={onSave}
        />
      </Wrapper>,
    );

    fireEvent.click(screen.getByRole('button', { name: /save/i }));

    expect(onSave).toHaveBeenCalledWith(['Story', 'Bug']);
  });
});
