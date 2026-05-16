import { render, screen, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { I18nextProvider } from 'react-i18next';
import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import { ThemeProvider } from '@mui/material/styles';
import { theme } from '@/theme/theme';
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

describe('WorkItemTypeFilter', () => {
  it('renders the current types as chips', () => {
    render(
      <Wrapper>
        <WorkItemTypeFilter currentTypes={['Story', 'Bug']} onChange={jest.fn()} />
      </Wrapper>,
    );

    expect(screen.getByText('Story')).toBeInTheDocument();
    expect(screen.getByText('Bug')).toBeInTheDocument();
  });

  it('calls onChange with the new list when a type is added', async () => {
    const onChange = jest.fn();
    const user = userEvent.setup();
    render(
      <Wrapper>
        <WorkItemTypeFilter currentTypes={[]} onChange={onChange} />
      </Wrapper>,
    );

    await user.type(screen.getByLabelText(/Add issue type/i), 'Epic');
    fireEvent.click(screen.getByRole('button', { name: /Add/i }));

    expect(screen.getByText('Epic')).toBeInTheDocument();
    expect(onChange).toHaveBeenCalledWith(['Epic']);
  });

  it('calls onChange with the updated list when a type is removed', async () => {
    const onChange = jest.fn();
    const user = userEvent.setup();
    render(
      <Wrapper>
        <WorkItemTypeFilter currentTypes={['Story', 'Bug']} onChange={onChange} />
      </Wrapper>,
    );

    const deleteButtons = screen.getAllByTestId('CancelIcon');
    await user.click(deleteButtons[0]);

    expect(screen.queryByText('Story')).not.toBeInTheDocument();
    expect(onChange).toHaveBeenCalledWith(['Bug']);
  });

  it('does not add a duplicate type', async () => {
    const onChange = jest.fn();
    const user = userEvent.setup();
    render(
      <Wrapper>
        <WorkItemTypeFilter currentTypes={['Story']} onChange={onChange} />
      </Wrapper>,
    );

    await user.type(screen.getByLabelText(/Add issue type/i), 'Story');
    fireEvent.click(screen.getByRole('button', { name: /Add/i }));

    expect(onChange).not.toHaveBeenCalled();
    expect(screen.getAllByText('Story')).toHaveLength(1);
  });
});
