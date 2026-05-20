'use client';

import { useMemo, type ReactNode } from 'react';
import Box from '@mui/material/Box';
import { useTheme, type Theme } from '@mui/material/styles';
import PublicHeader from '@/components/PublicHeader/PublicHeader';
import PublicFooter from '@/components/PublicFooter/PublicFooter';

export interface PublicLayoutProps {
  children: ReactNode;
}

/**
 * Wrapper layout for all public (unauthenticated) pages.
 * Renders the sticky PublicHeader on top, page content in the middle,
 * and PublicFooter at the bottom. No sidebar.
 */
export default function PublicLayout({ children }: PublicLayoutProps) {
  const theme = useTheme();
  const styles = getStyles(theme);

  return (
    <Box sx={styles.root}>
      <PublicHeader />
      <Box component="main" sx={styles.main}>
        {children}
      </Box>
      <PublicFooter />
    </Box>
  );
}

// co-located at the bottom of the file
const getStyles = (theme: Theme) =>
  // eslint-disable-next-line react-hooks/rules-of-hooks
  useMemo(
    () => ({
      root: {
        display: 'flex',
        flexDirection: 'column',
        minHeight: '100vh',
        backgroundColor: theme.palette.background.default,
      },
      main: {
        flex: 1,
        display: 'flex',
        flexDirection: 'column',
      },
    }),
    [theme],
  );
