'use client';

import { useState, useCallback, useMemo } from 'react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';
import AppBar from '@mui/material/AppBar';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Drawer from '@mui/material/Drawer';
import IconButton from '@mui/material/IconButton';
import List from '@mui/material/List';
import ListItem from '@mui/material/ListItem';
import ListItemButton from '@mui/material/ListItemButton';
import ListItemText from '@mui/material/ListItemText';
import Toolbar from '@mui/material/Toolbar';
import Typography from '@mui/material/Typography';
import MenuIcon from '@mui/icons-material/Menu';
import CloseIcon from '@mui/icons-material/Close';
import { useTheme, type Theme } from '@mui/material/styles';
import { useTranslation } from 'react-i18next';
import { DASHBOARD_ROUTE, SIGN_IN_ROUTE, SIGN_UP_ROUTE } from '@/routes/routes';
import { useAuthUser } from '@/hooks/useAuthUser';

const NAV_LINKS = [
  { labelKey: 'publicHeader.nav.home', href: '/' },
  { labelKey: 'publicHeader.nav.pricing', href: '/pricing' },
];

export default function PublicHeader() {
  const { t } = useTranslation('landing');
  const theme = useTheme();
  const pathname = usePathname();
  const [drawerOpen, setDrawerOpen] = useState(false);
  const user = useAuthUser();
  const styles = getStyles(theme);

  const handleOpenDrawer = useCallback(() => setDrawerOpen(true), []);
  const handleCloseDrawer = useCallback(() => setDrawerOpen(false), []);

  const isActive = (href: string) => {
    if (href === '/') return pathname === '/';
    return pathname.startsWith(href);
  };

  return (
    <>
      <AppBar position="sticky" elevation={0} sx={styles.appBar} component="header">
        <Toolbar sx={styles.toolbar}>
          {/* Logo */}
          <Box component={Link} href="/" sx={styles.logoLink} aria-label={t('publicHeader.logoAriaLabel')}>
            <Typography variant="h6" sx={styles.logoText}>
              Testurio
            </Typography>
          </Box>

          {/* Desktop nav links — hidden on mobile via CSS */}
          <Box sx={styles.navLinks}>
            {NAV_LINKS.map(({ labelKey, href }) => (
              <Box
                key={href}
                component={Link}
                href={href}
                sx={isActive(href) ? styles.navLinkActive : styles.navLink}
                aria-current={isActive(href) ? 'page' : undefined}
              >
                {t(labelKey)}
              </Box>
            ))}
          </Box>

          {/* Spacer */}
          <Box sx={{ flex: 1 }} />

          {/* Action area — right */}
          {user ? (
            <Button
              component={Link}
              href={DASHBOARD_ROUTE}
              variant="contained"
              size="small"
              sx={styles.ctaButton}
            >
              {t('publicHeader.action.dashboard')}
            </Button>
          ) : (
            <Box sx={styles.authActions}>
              {/* Sign In — hidden on mobile via CSS */}
              <Box component={Link} href={SIGN_IN_ROUTE} sx={styles.signInLink}>
                {t('publicHeader.action.signIn')}
              </Box>
              <Button
                component={Link}
                href={SIGN_UP_ROUTE}
                variant="contained"
                size="small"
                sx={styles.ctaButton}
              >
                {t('publicHeader.action.getStarted')}
              </Button>
            </Box>
          )}

          {/* Hamburger — hidden on desktop via CSS */}
          <IconButton
            edge="end"
            aria-label={t('publicHeader.action.openMenu')}
            onClick={handleOpenDrawer}
            sx={styles.hamburger}
          >
            <MenuIcon />
          </IconButton>
        </Toolbar>
      </AppBar>

      {/* Mobile drawer */}
      <Drawer anchor="right" open={drawerOpen} onClose={handleCloseDrawer}>
        <Box sx={styles.drawer} role="presentation">
          <Box sx={styles.drawerHeader}>
            <IconButton
              aria-label={t('publicHeader.action.closeMenu')}
              onClick={handleCloseDrawer}
            >
              <CloseIcon />
            </IconButton>
          </Box>
          <List>
            {NAV_LINKS.map(({ labelKey, href }) => (
              <ListItem key={href} disablePadding>
                <ListItemButton
                  component={Link}
                  href={href}
                  onClick={handleCloseDrawer}
                  selected={isActive(href)}
                >
                  <ListItemText primary={t(labelKey)} />
                </ListItemButton>
              </ListItem>
            ))}
          </List>
        </Box>
      </Drawer>
    </>
  );
}

// co-located at the bottom of the file
const getStyles = (theme: Theme) =>
  // eslint-disable-next-line react-hooks/rules-of-hooks
  useMemo(
    () => ({
      appBar: {
        backgroundColor: '#ffffff',
        color: theme.palette.text.primary,
        borderBottom: `1px solid ${theme.palette.divider}`,
      },
      toolbar: {
        px: theme.spacing(3),
        gap: theme.spacing(2),
      },
      logoLink: {
        display: 'flex',
        alignItems: 'center',
        textDecoration: 'none',
        color: 'inherit',
      },
      logoText: {
        fontWeight: 700,
        color: theme.palette.primary.main,
      },
      navLinks: {
        display: { xs: 'none', md: 'flex' },
        gap: theme.spacing(3),
        ml: theme.spacing(4),
      },
      navLink: {
        textDecoration: 'none',
        color: theme.palette.text.secondary,
        fontWeight: 500,
        '&:hover': { color: theme.palette.primary.main },
        transition: 'color 0.15s',
      },
      navLinkActive: {
        textDecoration: 'none',
        color: theme.palette.primary.main,
        fontWeight: 600,
        borderBottom: `2px solid ${theme.palette.primary.main}`,
        pb: '2px',
      },
      authActions: {
        display: 'flex',
        alignItems: 'center',
        gap: theme.spacing(2),
      },
      signInLink: {
        display: { xs: 'none', md: 'block' },
        textDecoration: 'none',
        color: theme.palette.text.secondary,
        fontWeight: 500,
        '&:hover': { color: theme.palette.primary.main },
        transition: 'color 0.15s',
      },
      ctaButton: {
        minWidth: { xs: 90, sm: 120 },
        minHeight: 44,
      },
      hamburger: {
        display: { xs: 'flex', md: 'none' },
        ml: theme.spacing(1),
        color: theme.palette.text.primary,
      },
      drawer: {
        width: 260,
        pt: theme.spacing(1),
      },
      drawerHeader: {
        display: 'flex',
        justifyContent: 'flex-end',
        px: theme.spacing(1),
        pb: theme.spacing(1),
      },
    }),
    [theme],
  );
