'use client';

import { useMemo } from 'react';
import Link from 'next/link';
import Box from '@mui/material/Box';
import Divider from '@mui/material/Divider';
import Typography from '@mui/material/Typography';
import { useTheme, type Theme } from '@mui/material/styles';
import { useTranslation } from 'react-i18next';

export default function PublicFooter() {
  const { t } = useTranslation('landing');
  const theme = useTheme();
  const styles = getStyles(theme);

  return (
    <Box component="footer" sx={styles.root}>
      <Divider />
      <Box sx={styles.inner}>
        {/* Logo */}
        <Box component={Link} href="/" sx={styles.logoLink} aria-label={t('publicFooter.logoAriaLabel')}>
          <Typography variant="h6" sx={styles.logoText}>
            Testurio
          </Typography>
        </Box>

        {/* Nav links */}
        <Box sx={styles.navLinks}>
          <Box component={Link} href="/" sx={styles.navLink}>
            {t('publicFooter.nav.home')}
          </Box>
          <Box component={Link} href="/pricing" sx={styles.navLink}>
            {t('publicFooter.nav.pricing')}
          </Box>
        </Box>

        {/* Legal links */}
        <Box sx={styles.legalLinks}>
          <Box component={Link} href="#" sx={styles.navLink}>
            {t('publicFooter.legal.privacy')}
          </Box>
          <Box component={Link} href="#" sx={styles.navLink}>
            {t('publicFooter.legal.terms')}
          </Box>
        </Box>

        {/* Copyright */}
        <Typography sx={styles.copyright}>
          {t('publicFooter.copyright')}
        </Typography>
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
        mt: 'auto',
        backgroundColor: '#ffffff',
      },
      inner: {
        maxWidth: 1200,
        mx: 'auto',
        px: theme.spacing(3),
        py: theme.spacing(4),
        display: 'flex',
        flexWrap: 'wrap',
        alignItems: 'center',
        gap: theme.spacing(3),
        [theme.breakpoints.down('sm')]: {
          flexDirection: 'column',
          alignItems: 'flex-start',
        },
      },
      logoLink: {
        display: 'flex',
        alignItems: 'center',
        textDecoration: 'none',
        color: 'inherit',
        mr: 'auto',
        [theme.breakpoints.down('sm')]: {
          mr: 0,
        },
      },
      logoText: {
        ...theme.typography.h6,
        fontWeight: 700,
        color: theme.palette.primary.main,
      },
      navLinks: {
        display: 'flex',
        gap: theme.spacing(3),
        flexWrap: 'wrap',
      },
      legalLinks: {
        display: 'flex',
        gap: theme.spacing(3),
        flexWrap: 'wrap',
      },
      navLink: {
        ...theme.typography.body2,
        textDecoration: 'none',
        color: theme.palette.text.secondary,
        minHeight: 44,
        display: 'flex',
        alignItems: 'center',
        '&:hover': { color: theme.palette.primary.main },
        transition: 'color 0.15s',
      },
      copyright: {
        ...theme.typography.caption,
        color: theme.palette.text.disabled,
        [theme.breakpoints.down('sm')]: {
          width: '100%',
        },
      },
    }),
    [theme],
  );
