'use client';

import { useMemo, type ReactNode } from 'react';
import Box from '@mui/material/Box';
import { useTheme, type Theme } from '@mui/material/styles';
import AppHeader from '@/components/AppHeader/AppHeader';
import AppSidebar from '@/components/AppSidebar/AppSidebar';
import { useAuthUser } from '@/hooks/useAuthUser';

const HEADER_HEIGHT = 64;

export interface PrivateCabinetLayoutProps {
  children: ReactNode;
}

export default function PrivateCabinetLayout({ children }: PrivateCabinetLayoutProps) {
  const theme = useTheme();
  const styles = getStyles(theme);
  const user = useAuthUser();

  return (
    <Box sx={styles.root}>
      <AppHeader user={user} />
      <Box sx={styles.body}>
        <AppSidebar />
        <Box component="main" sx={styles.main}>
          {children}
        </Box>
      </Box>
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
      },
      body: {
        display: 'flex',
        flex: 1,
        minHeight: 0,
        pt: `${HEADER_HEIGHT}px`,
      },
      main: {
        flex: 1,
        overflow: 'auto',
        minWidth: 0,
        p: theme.spacing(3),
      },
    }),
    [theme],
  );
