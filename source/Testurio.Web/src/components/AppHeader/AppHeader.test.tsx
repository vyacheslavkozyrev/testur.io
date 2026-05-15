import { render, screen } from '@testing-library/react';
import { I18nextProvider } from 'react-i18next';
import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import { ThemeProvider, createTheme } from '@mui/material/styles';
import AppHeader from './AppHeader';
import type { AuthUser } from '@/types/layout.types';

// Minimal i18n setup
const i18nInstance = i18n.createInstance();
i18nInstance.use(initReactI18next).init({
  lng: 'en',
  resources: {
    en: {
      layout: {
        'header.logoText': 'Testurio',
        'header.logoAriaLabel': 'Testurio — go to dashboard',
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

const mockUser: AuthUser = {
  id: 'user-1',
  displayName: 'Jane Smith',
  email: 'jane.smith@example.com',
};

const mockUserWithAvatar: AuthUser = {
  id: 'user-2',
  displayName: 'John Doe',
  email: 'john.doe@example.com',
  avatarUrl: 'https://example.com/avatar.png',
};

describe('AppHeader', () => {
  it('renders the logo with a link to /dashboard', () => {
    render(
      <Wrapper>
        <AppHeader user={mockUser} />
      </Wrapper>,
    );
    const logoLink = screen.getByRole('link', { name: /testurio — go to dashboard/i });
    expect(logoLink).toBeInTheDocument();
    expect(logoLink).toHaveAttribute('href', '/dashboard');
  });

  it('renders logo text', () => {
    render(
      <Wrapper>
        <AppHeader user={mockUser} />
      </Wrapper>,
    );
    expect(screen.getByText('Testurio')).toBeInTheDocument();
  });

  it('renders avatar with user initials when no picture URL is set', () => {
    render(
      <Wrapper>
        <AppHeader user={mockUser} />
      </Wrapper>,
    );
    // Avatar shows first letter of displayName
    expect(screen.getByText('J')).toBeInTheDocument();
  });

  it('renders the display name', () => {
    render(
      <Wrapper>
        <AppHeader user={mockUser} />
      </Wrapper>,
    );
    expect(screen.getByText('Jane Smith')).toBeInTheDocument();
  });

  it('truncates display name exceeding 24 characters with ellipsis', () => {
    const longNameUser: AuthUser = {
      id: 'user-3',
      displayName: 'Alexandrina Konstantinova',
      email: 'ak@example.com',
    };
    render(
      <Wrapper>
        <AppHeader user={longNameUser} />
      </Wrapper>,
    );
    // 'Alexandrina Konstantinova' is 25 chars; should be truncated to 24 + '…'
    expect(screen.getByText('Alexandrina Konstantinov…')).toBeInTheDocument();
  });

  it('falls back to email prefix when displayName is empty', () => {
    const noNameUser: AuthUser = {
      id: 'user-4',
      displayName: '',
      email: 'no.name@example.com',
    };
    render(
      <Wrapper>
        <AppHeader user={noNameUser} />
      </Wrapper>,
    );
    expect(screen.getByText('no.name')).toBeInTheDocument();
  });

  it('renders avatar image src when avatarUrl is provided', () => {
    render(
      <Wrapper>
        <AppHeader user={mockUserWithAvatar} />
      </Wrapper>,
    );
    const img = screen.getByRole('img');
    expect(img).toHaveAttribute('src', 'https://example.com/avatar.png');
  });

  it('renders nothing in the user section when user is null', () => {
    render(
      <Wrapper>
        <AppHeader user={null} />
      </Wrapper>,
    );
    expect(screen.queryByRole('img')).not.toBeInTheDocument();
  });
});
