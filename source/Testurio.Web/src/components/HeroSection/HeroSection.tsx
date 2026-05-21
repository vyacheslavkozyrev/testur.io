'use client';

import { useCallback, useMemo } from 'react';
import Link from 'next/link';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Container from '@mui/material/Container';
import Typography from '@mui/material/Typography';
import { alpha, useTheme, type Theme } from '@mui/material/styles';
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
        backgroundImage: [
          `url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='24' height='24'%3E%3Ccircle cx='1' cy='1' r='1' fill='${encodeURIComponent(theme.palette.primary.main)}' fill-opacity='0.12'/%3E%3C/svg%3E")`,
          `linear-gradient(135deg, ${alpha(theme.palette.primary.main, 0.094)} 0%, ${theme.palette.background.default} 100%)`,
        ].join(', '),
        backgroundRepeat: 'repeat, no-repeat',
        backgroundSize: '24px 24px, cover',
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
        fontWeight: 800,
        color: theme.palette.text.primary,
        maxWidth: 800,
        [theme.breakpoints.down('sm')]: {
          fontSize: '1.75rem',
        },
      },
      subheadline: {
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
