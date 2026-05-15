import { render, screen } from '@testing-library/react';
import { ThemeProvider, createTheme } from '@mui/material/styles';
import DashboardOutlinedIcon from '@mui/icons-material/DashboardOutlined';
import NavItem from './NavItem';

const theme = createTheme();

function Wrapper({ children }: { children: React.ReactNode }) {
  return <ThemeProvider theme={theme}>{children}</ThemeProvider>;
}

describe('NavItem', () => {
  it('renders icon and label when not collapsed', () => {
    render(
      <Wrapper>
        <NavItem
          icon={<DashboardOutlinedIcon />}
          label="Dashboard"
          href="/dashboard"
          active={false}
          collapsed={false}
        />
      </Wrapper>,
    );
    expect(screen.getByText('Dashboard')).toBeInTheDocument();
  });

  it('does not render label text when collapsed', () => {
    render(
      <Wrapper>
        <NavItem
          icon={<DashboardOutlinedIcon />}
          label="Dashboard"
          href="/dashboard"
          active={false}
          collapsed={true}
        />
      </Wrapper>,
    );
    expect(screen.queryByText('Dashboard')).not.toBeInTheDocument();
  });

  it('has an aria-label when collapsed', () => {
    render(
      <Wrapper>
        <NavItem
          icon={<DashboardOutlinedIcon />}
          label="Dashboard"
          href="/dashboard"
          active={false}
          collapsed={true}
          tooltip="Dashboard"
        />
      </Wrapper>,
    );
    const button = screen.getByRole('link', { name: 'Dashboard' });
    expect(button).toBeInTheDocument();
  });

  it('renders a Next.js Link (anchor with href) for client-side routing', () => {
    render(
      <Wrapper>
        <NavItem
          icon={<DashboardOutlinedIcon />}
          label="Dashboard"
          href="/dashboard"
          active={false}
          collapsed={false}
        />
      </Wrapper>,
    );
    const link = screen.getByRole('link', { name: /dashboard/i });
    expect(link).toHaveAttribute('href', '/dashboard');
  });

  it('applies selected state when active is true', () => {
    render(
      <Wrapper>
        <NavItem
          icon={<DashboardOutlinedIcon />}
          label="Dashboard"
          href="/dashboard"
          active={true}
          collapsed={false}
        />
      </Wrapper>,
    );
    const button = screen.getByRole('link', { name: /dashboard/i });
    // MUI ListItemButton adds aria-selected or a selected class
    expect(button).toHaveClass('Mui-selected');
  });

  it('uses tooltip text when provided', () => {
    render(
      <Wrapper>
        <NavItem
          icon={<DashboardOutlinedIcon />}
          label="Dashboard"
          href="/dashboard"
          active={false}
          collapsed={true}
          tooltip="Go to Dashboard"
        />
      </Wrapper>,
    );
    const button = screen.getByRole('link');
    expect(button).toHaveAttribute('aria-label', 'Go to Dashboard');
  });
});
