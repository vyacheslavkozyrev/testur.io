'use client';

import { useCallback, useMemo } from 'react';
import { usePathname } from 'next/navigation';
import Box from '@mui/material/Box';
import CircularProgress from '@mui/material/CircularProgress';
import Divider from '@mui/material/Divider';
import Drawer from '@mui/material/Drawer';
import IconButton from '@mui/material/IconButton';
import List from '@mui/material/List';
import ListItemButton from '@mui/material/ListItemButton';
import ListItemIcon from '@mui/material/ListItemIcon';
import ListItemText from '@mui/material/ListItemText';
import Tooltip from '@mui/material/Tooltip';
import { useTheme, type Theme } from '@mui/material/styles';
import ChevronLeftIcon from '@mui/icons-material/ChevronLeft';
import DashboardOutlinedIcon from '@mui/icons-material/DashboardOutlined';
import FolderOutlinedIcon from '@mui/icons-material/FolderOutlined';
import LogoutOutlinedIcon from '@mui/icons-material/LogoutOutlined';
import SettingsOutlinedIcon from '@mui/icons-material/SettingsOutlined';
import { useTranslation } from 'react-i18next';
import Link from 'next/link';
import { useSidebarState } from '@/hooks/useSidebarState';
import { useSignOut } from '@/hooks/useAuth';
import { DASHBOARD_ROUTE, PROJECTS_ROUTE, SETTINGS_ROUTE } from '@/routes/routes';

const EXPANDED_WIDTH = 240;
const COLLAPSED_WIDTH = 64;
const HEADER_HEIGHT = 64;

interface NavLinkConfig {
  href: string;
  labelKey: string;
  icon: React.ReactNode;
}

const primaryLinks: NavLinkConfig[] = [
  { href: DASHBOARD_ROUTE, labelKey: 'sidebar.dashboard', icon: <DashboardOutlinedIcon /> },
  { href: PROJECTS_ROUTE, labelKey: 'sidebar.projects', icon: <FolderOutlinedIcon /> },
];

function isLinkActive(href: string, pathname: string): boolean {
  if (href === DASHBOARD_ROUTE) return pathname === DASHBOARD_ROUTE;
  return pathname === href || pathname.startsWith(href + '/');
}

export default function AppSidebar() {
  const { t } = useTranslation('layout');
  const theme = useTheme();
  const [collapsed, toggle] = useSidebarState();
  const pathname = usePathname();
  const signOut = useSignOut();
  const styles = getStyles(theme, collapsed);

  const handleSignOut = useCallback(() => {
    signOut.mutate();
  }, [signOut]);

  const signingOut = signOut.isPending;

  const signOutButton = (
    <ListItemButton
      onClick={handleSignOut}
      disabled={signingOut}
      aria-label={t('sidebar.signOutAriaLabel')}
      sx={styles.signOutButton}
    >
      <ListItemIcon sx={styles.navIcon}>
        {signingOut ? <CircularProgress size={20} color="inherit" /> : <LogoutOutlinedIcon />}
      </ListItemIcon>
      {!collapsed && (
        <ListItemText
          primary={t('sidebar.signOut')}
          primaryTypographyProps={{ sx: styles.navLabel }}
          sx={{ m: 0 }}
        />
      )}
    </ListItemButton>
  );

  return (
    <Drawer variant="permanent" sx={styles.drawer} PaperProps={{ sx: styles.paper }}>
      {/* Toggle button */}
      <Box sx={styles.toggleRow}>
        <Tooltip
          title={collapsed ? t('sidebar.expandAriaLabel') : t('sidebar.collapseAriaLabel')}
          placement="right"
        >
          <IconButton
            onClick={toggle}
            aria-label={collapsed ? t('sidebar.expandAriaLabel') : t('sidebar.collapseAriaLabel')}
            size="small"
            sx={styles.toggleButton}
          >
            <ChevronLeftIcon sx={styles.chevron} />
          </IconButton>
        </Tooltip>
      </Box>

      {/* Primary nav */}
      <List sx={styles.navList} disablePadding>
        {primaryLinks.map(({ href, labelKey, icon }) => {
          const label = t(labelKey);
          const active = isLinkActive(href, pathname);

          if (collapsed) {
            return (
              <Tooltip key={href} title={label} placement="right">
                <ListItemButton
                  component={Link}
                  href={href}
                  selected={active}
                  aria-label={label}
                  sx={active ? styles.activeNavButton : styles.navButton}
                >
                  <ListItemIcon sx={styles.navIcon}>{icon}</ListItemIcon>
                </ListItemButton>
              </Tooltip>
            );
          }

          return (
            <ListItemButton
              key={href}
              component={Link}
              href={href}
              selected={active}
              sx={active ? styles.activeNavButton : styles.navButton}
            >
              <ListItemIcon sx={styles.navIcon}>{icon}</ListItemIcon>
              <ListItemText
                primary={label}
                primaryTypographyProps={{ sx: styles.navLabel }}
                sx={{ m: 0 }}
              />
            </ListItemButton>
          );
        })}
      </List>

      <Divider sx={styles.divider} />

      {/* Settings */}
      <List sx={styles.navList} disablePadding>
        {(() => {
          const label = t('sidebar.settings');
          const active = isLinkActive(SETTINGS_ROUTE, pathname);

          if (collapsed) {
            return (
              <Tooltip title={label} placement="right">
                <ListItemButton
                  component={Link}
                  href={SETTINGS_ROUTE}
                  selected={active}
                  aria-label={label}
                  sx={active ? styles.activeNavButton : styles.navButton}
                >
                  <ListItemIcon sx={styles.navIcon}><SettingsOutlinedIcon /></ListItemIcon>
                </ListItemButton>
              </Tooltip>
            );
          }

          return (
            <ListItemButton
              component={Link}
              href={SETTINGS_ROUTE}
              selected={active}
              sx={active ? styles.activeNavButton : styles.navButton}
            >
              <ListItemIcon sx={styles.navIcon}><SettingsOutlinedIcon /></ListItemIcon>
              <ListItemText
                primary={label}
                primaryTypographyProps={{ sx: styles.navLabel }}
                sx={{ m: 0 }}
              />
            </ListItemButton>
          );
        })()}
      </List>

      {/* Spacer */}
      <Box sx={{ flex: 1 }} />

      <Divider sx={styles.divider} />

      {/* Sign Out */}
      <List sx={styles.navList} disablePadding>
        {collapsed ? (
          <Tooltip title={t('sidebar.signOutTooltip')} placement="right">
            {signOutButton}
          </Tooltip>
        ) : (
          signOutButton
        )}
      </List>
    </Drawer>
  );
}

// co-located at the bottom of the file
const getStyles = (theme: Theme, collapsed: boolean) =>
  // eslint-disable-next-line react-hooks/rules-of-hooks
  useMemo(
    () => {
      const isDark = theme.palette.mode === 'dark';
      const width = collapsed ? COLLAPSED_WIDTH : EXPANDED_WIDTH;
      const hover = isDark ? 'rgba(255,255,255,0.06)' : 'rgba(0,0,0,0.05)';
      const activeBg = isDark ? 'rgba(96,165,250,0.18)' : 'rgba(59,130,246,0.1)';
      const activeText = isDark ? '#93c5fd' : '#2563eb';

      const navButtonBase = {
        borderRadius: `${theme.shape.borderRadius}px`,
        px: 1,
        py: 0.75,
        mb: 0.25,
        color: theme.palette.text.primary,
        '&:hover': { backgroundColor: hover },
        '&:focus-visible': {
          outline: `2px solid ${theme.palette.primary.main}`,
          outlineOffset: 2,
        },
      };

      return {
        drawer: {
          width,
          flexShrink: 0,
          '& .MuiDrawer-paper': {
            width,
            transition: 'width 200ms ease',
            overflowX: 'hidden',
          },
        },
        paper: {
          width,
          transition: 'width 200ms ease',
          overflowX: 'hidden',
          boxSizing: 'border-box' as const,
          top: HEADER_HEIGHT,
          height: `calc(100% - ${HEADER_HEIGHT}px)`,
          backgroundColor: theme.palette.background.paper,
          borderRight: `1px solid ${theme.palette.divider}`,
          display: 'flex',
          flexDirection: 'column' as const,
          pt: 1,
          pb: 1,
        },
        toggleRow: {
          display: 'flex',
          justifyContent: collapsed ? 'center' : 'flex-end',
          px: 1,
          pb: 1,
        },
        toggleButton: {
          transition: 'transform 200ms ease',
          color: theme.palette.text.secondary,
          '&:hover': { backgroundColor: hover, color: theme.palette.text.primary },
        },
        chevron: {
          transition: 'transform 200ms ease',
          transform: collapsed ? 'rotate(180deg)' : 'rotate(0deg)',
        },
        navList: {
          px: 1,
        },
        navButton: {
          ...navButtonBase,
          backgroundColor: 'transparent',
        },
        activeNavButton: {
          ...navButtonBase,
          backgroundColor: activeBg,
          color: activeText,
          '& .MuiListItemIcon-root': { color: activeText },
          '&:hover': { backgroundColor: activeBg },
          '&.Mui-selected': {
            backgroundColor: activeBg,
            '&:hover': { backgroundColor: activeBg },
          },
        },
        navIcon: {
          color: 'inherit',
          minWidth: 36,
        },
        navLabel: {
          color: 'inherit',
        },
        divider: {
          mx: 1,
          my: 0.5,
          borderColor: theme.palette.divider,
        },
        signOutButton: {
          ...navButtonBase,
          backgroundColor: 'transparent',
          color: theme.palette.text.secondary,
          '&:hover': {
            backgroundColor: hover,
            color: theme.palette.text.primary,
          },
        },
      };
    },
    [theme, collapsed],
  );
