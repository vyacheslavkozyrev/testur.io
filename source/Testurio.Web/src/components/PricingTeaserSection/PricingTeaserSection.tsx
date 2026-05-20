'use client';

import { useMemo } from 'react';
import Link from 'next/link';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Container from '@mui/material/Container';
import Grid from '@mui/material/Grid2';
import Skeleton from '@mui/material/Skeleton';
import Typography from '@mui/material/Typography';
import { useTheme, type Theme } from '@mui/material/styles';
import { useTranslation } from 'react-i18next';
import { usePlans } from '@/hooks/usePlans';

export default function PricingTeaserSection() {
  const { t } = useTranslation('landing');
  const theme = useTheme();
  const styles = getStyles(theme);
  const { data: plans, isPending } = usePlans();

  const displayPlans = plans ?? [];

  return (
    <Box sx={styles.root}>
      <Container maxWidth="lg">
        <Typography variant="h3" sx={styles.sectionTitle}>
          {t('pricingTeaser.title')}
        </Typography>
        <Typography sx={styles.summary}>
          {t('pricingTeaser.summary')}
        </Typography>

        <Grid container spacing={3} sx={styles.grid}>
          {isPending
            ? Array.from({ length: 4 }).map((_, i) => (
                <Grid key={i} size={{ xs: 12, sm: 6, md: 3 }}>
                  <Skeleton variant="rectangular" height={120} sx={{ borderRadius: '10px' }} />
                </Grid>
              ))
            : displayPlans.map((plan) => (
                <Grid key={plan.id} size={{ xs: 12, sm: 6, md: 3 }}>
                  <Box sx={styles.planCard}>
                    <Typography sx={styles.planName}>{plan.name}</Typography>
                    <Typography sx={styles.planPrice}>
                      {plan.monthlyPrice === 0 ? t('pricingTeaser.free') : `$${plan.monthlyPrice} / mo`}
                    </Typography>
                    <Typography sx={styles.planFeature}>{plan.features[0]}</Typography>
                  </Box>
                </Grid>
              ))}
        </Grid>

        <Box sx={styles.ctaWrapper}>
          <Button
            component={Link}
            href="/pricing"
            variant="outlined"
            size="large"
            sx={styles.ctaButton}
          >
            {t('pricingTeaser.cta')}
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
        py: { xs: theme.spacing(8), md: theme.spacing(12) },
        backgroundColor: theme.palette.background.paper,
      },
      sectionTitle: {
        fontWeight: 700,
        textAlign: 'center',
        mb: theme.spacing(2),
        color: theme.palette.text.primary,
      },
      summary: {
        color: theme.palette.text.secondary,
        textAlign: 'center',
        mb: theme.spacing(5),
      },
      grid: {
        mb: theme.spacing(5),
      },
      planCard: {
        p: theme.spacing(3),
        borderRadius: '10px',
        border: `1px solid ${theme.palette.divider}`,
        display: 'flex',
        flexDirection: 'column',
        gap: theme.spacing(1),
        height: '100%',
        textAlign: 'center',
      },
      planName: {
        fontWeight: 700,
        color: theme.palette.text.primary,
      },
      planPrice: {
        fontWeight: 800,
        color: theme.palette.primary.main,
      },
      planFeature: {
        color: theme.palette.text.secondary,
      },
      ctaWrapper: {
        display: 'flex',
        justifyContent: 'center',
      },
      ctaButton: {
        minWidth: 200,
        minHeight: 52,
        fontSize: '1rem',
        fontWeight: 600,
      },
    }),
    [theme],
  );
