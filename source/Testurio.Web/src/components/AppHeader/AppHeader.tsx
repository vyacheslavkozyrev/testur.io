'use client';

import { useMemo } from 'react';
import Link from 'next/link';
import AppBar from '@mui/material/AppBar';
import Avatar from '@mui/material/Avatar';
import Box from '@mui/material/Box';
import Toolbar from '@mui/material/Toolbar';
import Typography from '@mui/material/Typography';
import { useTheme, type Theme } from '@mui/material/styles';
import { useTranslation } from 'react-i18next';
import type { AuthUser } from '@/types/layout.types';

const MAX_DISPLAY_NAME_LENGTH = 24;

function getInitial(displayName: string): string {
  return displayName.trim().charAt(0).toUpperCase();
}

function getDisplayLabel(user: AuthUser | null): string {
  if (!user) return '';
  if (user.displayName) return user.displayName;
  return user.email.split('@')[0];
}

function getTruncatedLabel(label: string): string {
  if (label.length <= MAX_DISPLAY_NAME_LENGTH) return label;
  return label.slice(0, MAX_DISPLAY_NAME_LENGTH) + '…';
}

export interface AppHeaderProps {
  user: AuthUser | null;
}

export default function AppHeader({ user }: AppHeaderProps) {
  const { t } = useTranslation('layout');
  const theme = useTheme();
  const styles = getStyles(theme);

  const displayLabel = getDisplayLabel(user);
  const truncatedLabel = getTruncatedLabel(displayLabel);
  const avatarInitial = displayLabel ? getInitial(displayLabel) : '';

  return (
    <AppBar position="sticky" elevation={0} sx={styles.appBar} component="header">
      <Toolbar sx={styles.toolbar}>
        {/* Logo */}
        <Box component={Link} href="/dashboard" sx={styles.logoLink} aria-label={t('header.logoAriaLabel')}>
          <Typography variant="h6" sx={styles.logoText}>
            {t('header.logoText')}
          </Typography>
        </Box>

        {/* Spacer */}
        <Box sx={{ flex: 1 }} />

        {/* User identity */}
        {user && (
          <Box sx={styles.userSection}>
            <Typography sx={styles.displayName} title={displayLabel.length > MAX_DISPLAY_NAME_LENGTH ? displayLabel : undefined}>
              {truncatedLabel}
            </Typography>
            <Avatar
              src={user.avatarUrl}
              alt={displayLabel}
              sx={styles.avatar}
            >
              {!user.avatarUrl && avatarInitial}
            </Avatar>
          </Box>
        )}
      </Toolbar>
    </AppBar>
  );
}

// co-located at the bottom of the file
const getStyles = (theme: Theme) =>
  // eslint-disable-next-line react-hooks/rules-of-hooks
  useMemo(
    () => ({
      appBar: {
        backgroundColor: theme.palette.background.paper,
        color: theme.palette.text.primary,
        borderBottom: `1px solid ${theme.palette.divider}`,
        zIndex: theme.zIndex.drawer + 1,
        height: 64,
      },
      toolbar: {
        height: 64,
        minHeight: '64px !important',
        px: theme.spacing(2),
      },
      logoLink: {
        display: 'flex',
        alignItems: 'center',
        textDecoration: 'none',
        color: 'inherit',
      },
      logoText: {
        ...theme.typography.h6,
        fontWeight: 700,
        color: theme.palette.primary.main,
      },
      userSection: {
        display: 'flex',
        alignItems: 'center',
        gap: theme.spacing(1),
      },
      displayName: {
        ...theme.typography.body2,
        color: theme.palette.text.primary,
        maxWidth: 180,
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
      },
      avatar: {
        width: 36,
        height: 36,
        fontSize: 14,
        bgcolor: theme.palette.primary.main,
        color: theme.palette.primary.contrastText,
      },
    }),
    [theme],
  );
