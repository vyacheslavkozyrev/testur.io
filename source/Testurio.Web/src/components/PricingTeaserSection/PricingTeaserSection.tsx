'use client';

import { useMemo } from 'react';
import Link from 'next/link';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Container from '@mui/material/Container';
import Grid from '@mui/material/Grid';
import Typography from '@mui/material/Typography';
import { useTheme, type Theme } from '@mui/material/styles';
import { useTranslation } from 'react-i18next';

interface PlanTeaser {
  nameKey: string;
  priceKey: string;
  featureKey: string;
}

const PLAN_TEASERS: PlanTeaser[] = [
  { nameKey: 'pricingTeaser.plans.testJunior.name', priceKey: 'pricingTeaser.plans.testJunior.price', featureKey: 'pricingTeaser.plans.testJunior.feature' },
  { nameKey: 'pricingTeaser.plans.testPro.name', priceKey: 'pricingTeaser.plans.testPro.price', featureKey: 'pricingTeaser.plans.testPro.feature' },
  { nameKey: 'pricingTeaser.plans.team.name', priceKey: 'pricingTeaser.plans.team.price', featureKey: 'pricingTeaser.plans.team.feature' },
  { nameKey: 'pricingTeaser.plans.centurio.name', priceKey: 'pricingTeaser.plans.centurio.price', featureKey: 'pricingTeaser.plans.centurio.feature' },
];

export default function PricingTeaserSection() {
  const { t } = useTranslation('landing');
  const theme = useTheme();
  const styles = getStyles(theme);

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
          {PLAN_TEASERS.map((plan) => (
            <Grid key={plan.nameKey} size={{ xs: 12, sm: 6, md: 3 }}>
              <Box sx={styles.planCard}>
                <Typography sx={styles.planName}>
                  {t(plan.nameKey)}
                </Typography>
                <Typography sx={styles.planPrice}>
                  {t(plan.priceKey)}
                </Typography>
                <Typography sx={styles.planFeature}>
                  {t(plan.featureKey)}
                </Typography>
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
        backgroundColor: '#ffffff',
      },
      sectionTitle: {
        ...theme.typography.h4,
        fontWeight: 700,
        textAlign: 'center',
        mb: theme.spacing(2),
        color: theme.palette.text.primary,
      },
      summary: {
        ...theme.typography.body1,
        color: theme.palette.text.secondary,
        textAlign: 'center',
        mb: theme.spacing(5),
      },
      grid: {
        mb: theme.spacing(5),
      },
      planCard: {
        p: theme.spacing(3),
        borderRadius: theme.shape.borderRadius,
        border: `1px solid ${theme.palette.divider}`,
        display: 'flex',
        flexDirection: 'column',
        gap: theme.spacing(1),
        height: '100%',
        textAlign: 'center',
      },
      planName: {
        ...theme.typography.h6,
        fontWeight: 700,
        color: theme.palette.text.primary,
      },
      planPrice: {
        ...theme.typography.h5,
        fontWeight: 800,
        color: theme.palette.primary.main,
      },
      planFeature: {
        ...theme.typography.body2,
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
