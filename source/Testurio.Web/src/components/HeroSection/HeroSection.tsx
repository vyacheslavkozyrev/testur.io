'use client';

import { useCallback, useMemo } from 'react';
import Link from 'next/link';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Container from '@mui/material/Container';
import Typography from '@mui/material/Typography';
import { useTheme, type Theme } from '@mui/material/styles';
import { useTranslation } from 'react-i18next';
import { SIGN_UP_ROUTE } from '@/routes/routes';

export default function HeroSection() {
  const { t } = useTranslation('landing');
  const theme = useTheme();
  const styles = getStyles(theme);

  const handleScrollToHowItWorks = useCallback((e: React.MouseEvent<HTMLButtonElement>) => {
    e.preventDefault();
    const section = document.getElementById('how-it-works');
    if (section) {
      section.scrollIntoView({ behavior: 'smooth' });
    }
  }, []);

  return (
    <Box sx={styles.root}>
      <Container maxWidth="lg" sx={styles.container}>
        <Typography variant="h1" sx={styles.headline}>
          {t('hero.headline')}
        </Typography>
        <Typography variant="h5" sx={styles.subheadline}>
          {t('hero.subheadline')}
        </Typography>
        <Box sx={styles.actions}>
          <Button
            component={Link}
            href={SIGN_UP_ROUTE}
            variant="contained"
            size="large"
            sx={styles.primaryCta}
          >
            {t('hero.primaryCta')}
          </Button>
          <Button
            variant="outlined"
            size="large"
            sx={styles.secondaryCta}
            onClick={handleScrollToHowItWorks}
          >
            {t('hero.secondaryCta')}
          </Button>
        </Box>
      </Container>
    </Box>
  );
}

// co-located at the bottom of the file
const getStyles = (theme: Theme) =>
  // eslint-disable-next-line react-hooks/rules-of-hooks
  useMemo(
    () => ({
      root: {
        width: '100%',
        background: `linear-gradient(135deg, ${theme.palette.primary.main}15 0%, ${theme.palette.background.default} 100%)`,
        py: { xs: theme.spacing(8), md: theme.spacing(14) },
        overflow: 'hidden',
      },
      container: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        textAlign: 'center',
        gap: theme.spacing(3),
      },
      headline: {
        ...theme.typography.h2,
        fontWeight: 800,
        color: theme.palette.text.primary,
        maxWidth: 800,
        [theme.breakpoints.down('sm')]: {
          fontSize: '1.75rem',
        },
      },
      subheadline: {
        ...theme.typography.h6,
        color: theme.palette.text.secondary,
        maxWidth: 600,
        fontWeight: 400,
        [theme.breakpoints.down('sm')]: {
          fontSize: '1rem',
        },
      },
      actions: {
        display: 'flex',
        gap: theme.spacing(2),
        flexWrap: 'wrap',
        justifyContent: 'center',
        mt: theme.spacing(1),
      },
      primaryCta: {
        minWidth: { xs: 140, sm: 180 },
        minHeight: 52,
        fontSize: '1rem',
        fontWeight: 600,
      },
      secondaryCta: {
        minWidth: { xs: 140, sm: 180 },
        minHeight: 52,
        fontSize: '1rem',
        fontWeight: 600,
        color: theme.palette.primary.main,
        borderColor: theme.palette.primary.main,
      },
    }),
    [theme],
  );
