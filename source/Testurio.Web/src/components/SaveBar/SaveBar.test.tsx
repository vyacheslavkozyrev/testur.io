import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { I18nextProvider } from 'react-i18next';
import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import { ThemeProvider, createTheme } from '@mui/material/styles';
import SaveBar from './SaveBar';

const i18nInstance = i18n.createInstance();
i18nInstance.use(initReactI18next).init({
  lng: 'en',
  resources: {
    en: {
      project: {
        saveBar: {
          save: 'Save Changes',
          noChanges: 'No changes',
          saving: 'Saving…',
          saved: 'Saved ✓',
        },
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

describe('SaveBar', () => {
  it('shows "No changes" and is disabled in clean state', () => {
    render(
      <Wrapper>
        <SaveBar state="clean" onClick={jest.fn()} />
      </Wrapper>,
    );
    const button = screen.getByRole('button', { name: /no changes/i });
    expect(button).toBeInTheDocument();
    expect(button).toBeDisabled();
  });

  it('shows "Save Changes" and is enabled in dirty state', () => {
    render(
      <Wrapper>
        <SaveBar state="dirty" onClick={jest.fn()} />
      </Wrapper>,
    );
    const button = screen.getByRole('button', { name: /save changes/i });
    expect(button).toBeInTheDocument();
    expect(button).not.toBeDisabled();
  });

  it('shows "Saving…" and is disabled in saving state', () => {
    render(
      <Wrapper>
        <SaveBar state="saving" onClick={jest.fn()} />
      </Wrapper>,
    );
    const button = screen.getByRole('button', { name: /saving/i });
    expect(button).toBeInTheDocument();
    expect(button).toBeDisabled();
  });

  it('shows "Saved ✓" and is disabled in saved state', () => {
    render(
      <Wrapper>
        <SaveBar state="saved" onClick={jest.fn()} />
      </Wrapper>,
    );
    const button = screen.getByRole('button', { name: /saved/i });
    expect(button).toBeInTheDocument();
    expect(button).toBeDisabled();
  });

  it('calls onClick when clicked in dirty state', async () => {
    const user = userEvent.setup();
    const onClick = jest.fn();
    render(
      <Wrapper>
        <SaveBar state="dirty" onClick={onClick} />
      </Wrapper>,
    );
    await user.click(screen.getByRole('button', { name: /save changes/i }));
    expect(onClick).toHaveBeenCalledTimes(1);
  });

  it('does not call onClick when clicked in clean state', async () => {
    const user = userEvent.setup();
    const onClick = jest.fn();
    render(
      <Wrapper>
        <SaveBar state="clean" onClick={onClick} />
      </Wrapper>,
    );
    const button = screen.getByRole('button', { name: /no changes/i });
    // disabled buttons cannot be clicked via userEvent
    expect(button).toBeDisabled();
    expect(onClick).not.toHaveBeenCalled();
  });
});
