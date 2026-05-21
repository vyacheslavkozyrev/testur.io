'use client';

import { useState, useCallback, useMemo } from 'react';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Container from '@mui/material/Container';
import Grid from '@mui/material/Grid2';
import Skeleton from '@mui/material/Skeleton';
import Typography from '@mui/material/Typography';
import { useTheme, type Theme } from '@mui/material/styles';
import { useTranslation } from 'react-i18next';
import PublicLayout from '@/components/PublicLayout/PublicLayout';
import BillingIntervalToggle from '@/components/BillingIntervalToggle/BillingIntervalToggle';
import PlanCard from '@/components/PlanCard/PlanCard';
import { usePlans } from '@/hooks/usePlans';
import { useAuthUser } from '@/hooks/useAuthUser';
import { useSubscriptionStatus } from '@/hooks/useBilling';
import type { BillingInterval } from '@/types/plan.types';

const SKELETON_COUNT = 4;

export default function PricingPage() {
  const { t } = useTranslation('pricing');
  const theme = useTheme();
  const styles = getStyles(theme);

  const [interval, setBillingInterval] = useState<BillingInterval>('monthly');
  const { data: plans, isPending, isError, refetch } = usePlans();
  const user = useAuthUser();
  const isAuthenticated = user !== null;
  const { data: subscription } = useSubscriptionStatus();

  const handleIntervalChange = useCallback((newInterval: BillingInterval) => {
    setBillingInterval(newInterval);
  }, []);

  const handleRetry = useCallback(() => {
    void refetch();
  }, [refetch]);

  // Derive the annual discount percent from the first plan that has one (for the toggle badge)
  const commonDiscountPercent = useMemo(() => {
    if (!plans) return undefined;
    return plans.find((p) => p.annualDiscountPercent > 0)?.annualDiscountPercent;
  }, [plans]);

  // Map the subscription plan name (e.g. "TestPro") to a kebab-case id (e.g. "test-pro")
  // then find its index in the sorted plans array — used to block downgrades.
  const currentPlanRank = useMemo(() => {
    if (!plans || !subscription?.plan) return null;
    const kebab = subscription.plan.replace(/([A-Z])/g, (m, l: string, o: number) =>
      o === 0 ? l.toLowerCase() : `-${l.toLowerCase()}`
    );
    const idx = plans.findIndex((p) => p.id === kebab);
    return idx >= 0 ? idx : null;
  }, [plans, subscription?.plan]);

  return (
    <PublicLayout>
      <Box sx={styles.root}>
        <Container maxWidth="lg">
          {/* Page heading */}
          <Typography variant="h2" sx={styles.title}>
            {t('page.title')}
          </Typography>
          <Typography sx={styles.subtitle}>
            {t('page.subtitle')}
          </Typography>

          {/* Billing interval toggle */}
          <BillingIntervalToggle
            value={interval}
            annualDiscountPercent={commonDiscountPercent}
            onChange={handleIntervalChange}
          />

          {/* Error state */}
          {isError && (
            <Box sx={styles.errorWrapper}>
              <Alert
                severity="error"
                action={
                  <Button color="inherit" size="small" onClick={handleRetry}>
                    {t('page.retry')}
                  </Button>
                }
              >
                {t('page.error')}
              </Alert>
            </Box>
          )}

          {/* Plan cards grid */}
          <Grid container spacing={3} alignItems="stretch">
            {isPending
              ? Array.from({ length: SKELETON_COUNT }).map((_, i) => (
                  <Grid key={i} size={{ xs: 12, sm: 6, lg: 3 }}>
                    <Skeleton
                      variant="rectangular"
                      height={500}
                      sx={{ borderRadius: '10px' }}
                    />
                  </Grid>
                ))
              : plans?.map((plan, index) => (
                  <Grid key={plan.id} size={{ xs: 12, sm: 6, lg: 3 }}>
                    <PlanCard
                      plan={plan}
                      interval={interval}
                      isAuthenticated={isAuthenticated}
                      planRank={index}
                      currentPlanRank={currentPlanRank}
                    />
                  </Grid>
                ))}
          </Grid>
        </Container>
      </Box>
    </PublicLayout>
  );
}

// co-located at the bottom of the file
const getStyles = (theme: Theme) =>
  // eslint-disable-next-line react-hooks/rules-of-hooks
  useMemo(
    () => ({
      root: {
        py: { xs: theme.spacing(6), md: theme.spacing(10) },
      },
      title: {
        fontWeight: 800,
        textAlign: 'center',
        mb: theme.spacing(2),
        color: theme.palette.text.primary,
      },
      subtitle: {
        color: theme.palette.text.secondary,
        textAlign: 'center',
        mb: theme.spacing(5),
      },
      errorWrapper: {
        mb: theme.spacing(3),
      },
    }),
    [theme],
  );
